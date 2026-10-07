using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Silk.NET.Maths;
using Silk.NET.OpenGLES;
using VirtualPathCore.Graphics;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Graphics.OpenGL;
using GdPixelFormat = System.Drawing.Imaging.PixelFormat;
using GdRectangle = System.Drawing.Rectangle;
using Shader = VirtualPathCore.Graphics.OpenGL.Shader;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 第一段可行性验证：证明引擎的渲染栈能在无 UI 进程里跑通
/// （GL 上下文 → 着色器 → 网格 → 绘制 → 读回），
/// 并顺带检验一个具体假设：着色器写死 <c>#version 300 es</c>
/// 在桌面 GL 上下文（WGL/GLFW）上无法编译。
/// </summary>
internal static unsafe class Program
{
    private const int Width = 1280;
    private const int Height = 720;

    private static int Main(string[] args)
    {
        // --serve：常驻进程，把场景渲染成 MJPEG 供 virtual-path-vision 消费
        if (args.Contains("--serve"))
            return RunServe(args);

        return RunSpike();
    }

    /// <summary>解析 --port / --fps / --width / --height，允许缺省。</summary>
    private static int RunServe(string[] args)
    {
        int port = ArgInt(args, "--port", 8080);
        int fps = ArgInt(args, "--fps", 30);
        int w = ArgInt(args, "--width", 1280);
        int h = ArgInt(args, "--height", 720);

        return ServeProgram.Run(w, h, port, fps);
    }

    private static int ArgInt(string[] args, string name, int fallback)
    {
        int i = Array.IndexOf(args, name);
        if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int v))
            return v;
        return fallback;
    }

    /// <summary>第一/二段：一次性验证程序（上下文、着色器、离屏、引擎场景）。</summary>
    private static int RunSpike()
    {
        string shaderDir = Path.Combine(AppContext.BaseDirectory, "Resources", "Shaders");
        string outDir = Path.Combine(AppContext.BaseDirectory, "out");
        Directory.CreateDirectory(outDir);

        // ---------- [1] headless GL context ----------
        Console.WriteLine("=== [1] headless GL context ===");
        using var host = new HeadlessGraphicsHost(Width, Height);
        GL gl = host.GetContext();

        Console.WriteLine($"  GL_VERSION     : {GetGlString(gl, StringName.Version)}");
        Console.WriteLine($"  GL_RENDERER    : {GetGlString(gl, StringName.Renderer)}");
        Console.WriteLine($"  GL_VENDOR      : {GetGlString(gl, StringName.Vendor)}");
        Console.WriteLine($"  GLSL VERSION   : {GetGlString(gl, StringName.ShadingLanguageVersion)}");

        // ---------- [2] shader compilation ----------
        Console.WriteLine();
        Console.WriteLine("=== [2] shader compile check ===");

        string vertRaw = File.ReadAllText(Path.Combine(shaderDir, "PBR.vert"));
        string fragRaw = File.ReadAllText(Path.Combine(shaderDir, "PBR.frag"));

        TryCompile(host, "as-is    (#version 300 es)", vertRaw, fragRaw, out bool esOk);
        TryCompile(host, "desktop  (#version 330 core)", ToDesktopGls(vertRaw), ToDesktopGls(fragRaw), out bool desktopOk);

        if (!esOk && !desktopOk)
        {
            Console.WriteLine();
            Console.WriteLine("RESULT: FAIL - no usable GLSL variant; headless render is blocked.");
            return 2;
        }

        string useVert = desktopOk ? ToDesktopGls(vertRaw) : vertRaw;
        string useFrag = desktopOk ? ToDesktopGls(fragRaw) : fragRaw;

        // ---------- [3] offscreen render ----------
        Console.WriteLine();
        Console.WriteLine("=== [3] offscreen render ===");

        string pngPath = Path.Combine(outDir, "headless_frame.png");
        if (!RenderFrame(host, useVert, useFrag, pngPath))
            return 3;

        Console.WriteLine($"  wrote {pngPath}");
        Console.WriteLine();
        Console.WriteLine($"RESULT: PASS (es={esOk}, desktop={desktopOk}) - headless offscreen render works.");

        host.Dispose();   // 第一段已结束，先释放 GL 上下文，再跑第二段

        // ---------- [4] 矩阵内存布局逐元素实测 ----------
        int layoutResult = MatrixLayoutProgram.Run(Width, Height, outDir);

        // ---------- [5] 组合穷举：让 GPU 直接裁决 ----------
        int sweepResult = MatrixSweepProgram.Run(Width, Height);

        // ---------- [5] GL 符号解析检查 ----------
        int symbolResult = SymbolProbe.Run();

        // ---------- [5] GPU 真实投影回读（ground truth） ----------
        int gpuResult = GpuNdcProgram.Run(Width, Height, outDir);

        // ---------- [6] 最小可判别实验：引擎资源 + 手写绘制循环 ----------
        int minimalResult = MinimalRenderProgram.Run(Width, Height, outDir);
        if (minimalResult != 0)
            return minimalResult;   // 引擎资源本身有问题，后面的实验无意义

        // ---------- [6] A/B 对照：单个立方体 ----------
        string abPng = Path.Combine(outDir, "ab_cube.png");
        int abResult = AbCompareProgram.Run(Width, Height, abPng);

        // ---------- [5] camera math probe ----------
        int probeResult = CameraProbe.Run();

        // ---------- [6] engine scene system ----------
        string scenePng = Path.Combine(outDir, "headless_scene.png");
        int sceneResult = SceneProgram.Run(Width, Height, scenePng, "factory");

        return abResult != 0 ? abResult : (probeResult != 0 ? probeResult : sceneResult);
    }

    private static string GetGlString(GL gl, StringName name)
    {
        byte* p = gl.GetString(name);
        return p == null ? "<null>" : Marshal.PtrToStringUTF8((nint)p) ?? "<null>";
    }

    /// <summary>把 ES 的版本指令换成桌面 GL 的等价写法，其余内容不动。</summary>
    private static string ToDesktopGls(string source)
    {
        string[] lines = source.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("#version", StringComparison.Ordinal))
            {
                lines[i] = "#version 330 core";
                return string.Join("\n", lines);
            }
        }
        return "#version 330 core\n" + source;
    }

    private static void TryCompile(
        IGraphicsHost<GL> host, string tag, string vert, string frag, out bool ok)
    {
        try
        {
            using Shader vs = new(host, ShaderType.VertexShader, vert);
            using Shader fs = new(host, ShaderType.FragmentShader, frag);
            ok = true;
            Console.WriteLine($"  [OK]   {tag}");
        }
        catch (Exception ex)
        {
            ok = false;
            Console.WriteLine($"  [FAIL] {tag}");
            foreach (string line in ex.Message.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                    Console.WriteLine($"         {trimmed}");
            }
        }
    }

    private static bool RenderFrame(
        HeadlessGraphicsHost host, string vertSrc, string fragSrc, string pngPath)
    {
        try
        {
            GL gl = host.GetContext();
            using var target = new OffscreenTarget(host, Width, Height);

            (Vertex[] gv, uint[] gi) = Meshes.CreateGround(6f, new Vector4D<float>(0.22f, 0.23f, 0.26f, 1f));
            using Mesh ground = new(host, gv, gi);
            ground.SetupAttributes(0, 1, 2, 3, 4, 5);

            (Vertex[] cv, uint[] ci) = Meshes.CreateCube(0.5f, new Vector4D<float>(1f, 1f, 1f, 1f));
            using Mesh cube = new(host, cv, ci);
            cube.SetupAttributes(0, 1, 2, 3, 4, 5);

            using Shader vs = new(host, ShaderType.VertexShader, vertSrc);
            using Shader fs = new(host, ShaderType.FragmentShader, fragSrc);
            using RenderPipeline pipeline = new(host, vs, fs);

            // 相机置于 (0,3,8) 俯视原点；行向量约定下先平移再绕 X 轴旋转
            const float pitch = 0.36f;   // ≈20.6°，把视线方向 (0,-0.351,-0.937) 转成 (0,0,-1)
            Matrix4X4<float> view = Matrix4X4.CreateTranslation(0f, -3f, -8f)
                                  * Matrix4X4.CreateRotationX(pitch);
            Matrix4X4<float> proj = Matrix4X4.CreatePerspectiveFieldOfView(
                MathF.PI / 4f, (float)Width / Height, 0.1f, 100f);
            Vector3D<float> cameraPos = new(0f, 3f, 8f);

            bool drew = false;
            host.OnRender += _ =>
            {
                target.Bind();

                pipeline.Bind();
                SetCommonUniforms(pipeline, cameraPos);

                DrawObject(pipeline, ground,
                           Matrix4X4<float>.Identity, Matrix4X4<float>.Identity, view, proj,
                           new Vector4D<float>(0.22f, 0.23f, 0.26f, 1f));

                // workpieces: uniform scale + translation only, so WorldToObject stays trivial
                DrawCube(pipeline, cube, view, proj, new Vector3D<float>(-1.1f, 0.5f, 0f), 1f,
                         new Vector4D<float>(0.85f, 0.25f, 0.2f, 1f));
                DrawCube(pipeline, cube, view, proj, new Vector3D<float>(0f, 0.5f, 0.3f), 1f,
                         new Vector4D<float>(0.2f, 0.75f, 0.3f, 1f));
                DrawCube(pipeline, cube, view, proj, new Vector3D<float>(1.1f, 0.5f, -0.2f), 1f,
                         new Vector4D<float>(0.2f, 0.45f, 0.9f, 1f));

                pipeline.Unbind();
                drew = true;

                GLEnum err = gl.GetError();
                if (err != GLEnum.NoError)
                    Console.WriteLine($"  [GL] GetError = {err}");
            };

            host.RenderFrame(0.0);

            if (!drew)
            {
                Console.WriteLine("  [FAIL] render callback never ran");
                return false;
            }

            byte[] pixels = target.ReadPixelsRgba();
            WritePng(pixels, Width, Height, pngPath);

            int cx = Width / 2, cy = Height / 2;
            Console.WriteLine($"  centre pixel RGBA = {pixels[(cy * Width + cx) * 4]},{pixels[(cy * Width + cx) * 4 + 1]},{pixels[(cy * Width + cx) * 4 + 2]}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [FAIL] {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>把自上而下的 RGBA8 像素写成 PNG（32bpp 内存序为 BGRA，故交换 R/B）。</summary>
    internal static void WritePng(byte[] rgba, int width, int height, string path)
    {
        var bgra = new byte[rgba.Length];
        for (int i = 0; i < rgba.Length; i += 4)
        {
            bgra[i + 0] = rgba[i + 2];
            bgra[i + 1] = rgba[i + 1];
            bgra[i + 2] = rgba[i + 0];
            bgra[i + 3] = rgba[i + 3];
        }

        using var bmp = new System.Drawing.Bitmap(width, height, GdPixelFormat.Format32bppArgb);
        BitmapData locked = bmp.LockBits(
            new GdRectangle(0, 0, width, height), ImageLockMode.WriteOnly, GdPixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(bgra, 0, locked.Scan0, bgra.Length);
        }
        finally
        {
            bmp.UnlockBits(locked);
        }

        bmp.Save(path, ImageFormat.Png);
    }

    private static void SetCommonUniforms(RenderPipeline p, Vector3D<float> cameraPos)
    {
        p.SetUniform("Metallic", 0.05f);
        p.SetUniform("Roughness", 0.35f);
        p.SetUniform("AmbientIntensity", 0.15f);
        p.SetUniform("Light0Dir", Vector3D.Normalize(new Vector3D<float>(0.4f, -1f, 0.6f)));
        p.SetUniform("Light0Color", new Vector3D<float>(1f, 1f, 1f));
        p.SetUniform("Light0Intensity", 1.3f);
        p.SetUniform("CameraPos", cameraPos);
        p.SetUniform("HasAlbedoMap", 0);
        p.SetUniform("HasNormalMap", 0);
    }

    private static void DrawCube(
        RenderPipeline p, Mesh mesh, Matrix4X4<float> view, Matrix4X4<float> proj,
        Vector3D<float> translation, float scale, Vector4D<float> albedo)
    {
        // p' = s*p + s*t，故 p = p'/s - t
        Matrix4X4<float> model = Matrix4X4.CreateScale(scale) * Matrix4X4.CreateTranslation(translation);
        Matrix4X4<float> inverse = Matrix4X4.CreateTranslation(-translation) * Matrix4X4.CreateScale(1f / scale);

        DrawObject(p, mesh, model, inverse, view, proj, albedo);
    }

    private static void DrawObject(
        RenderPipeline p, Mesh mesh, Matrix4X4<float> model, Matrix4X4<float> worldToObject,
        Matrix4X4<float> view, Matrix4X4<float> proj, Vector4D<float> albedo)
    {
        p.SetUniform("ObjectToWorld", model);
        // 必须设置：PBR.vert 用它反算法线/切线，缺省为零矩阵会让法线变成 NaN，整帧变黑
        p.SetUniform("WorldToObject", worldToObject);
        p.SetUniform("ObjectToClip", model * view * proj);
        p.SetUniform("Albedo", albedo);
        mesh.Draw();
    }
}
