using Silk.NET.Maths;
using Silk.NET.OpenGLES;
using VirtualPathCore.Graphics;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Graphics.OpenGL;
using VirtualPathCore.Helpers;
using VirtualPathCore.Services;
using Shader = VirtualPathCore.Graphics.OpenGL.Shader;
using Camera = VirtualPathCore.Graphics.Core.Camera;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 最小可判别实验：完全绕开 <c>SimpleDrawingService.Render()</c>，
/// 用引擎的 <c>MeshFactory</c> + <c>Mesh</c> + <c>RenderPipeline</c> + <c>Camera</c>，
/// 但由本文件手写绘制循环与 uniform 上传。
///
/// <para>判定逻辑：</para>
/// <list type="bullet">
/// <item>若此路径渲染出正常立方体 ⇒ 问题在 <c>RenderScene</c> 的状态设置/绘制顺序里。</item>
/// <item>若仍是竖线 ⇒ 问题在引擎的 GL 资源或矩阵上传里，与绘制逻辑无关。</item>
/// </list>
///
/// 由于每种组合都会跑，这里同时跑三个变体对比。
/// </summary>
internal static class MinimalRenderProgram
{
    /// <summary>
    /// 用指定背面剔除设置渲染一帧，统计覆盖的红色像素比例。
    /// 用于判定立方体三角形在引擎当前矩阵约定下的绕序方向。
    /// </summary>
    private static void RenderVariant(
        HeadlessGraphicsHost host, GL gl, OffscreenTarget target,
        RenderPipeline pipeline, Mesh mesh, Camera camera, Matrix4X4<float> model,
        string label, GLEnum? cullFace)
    {
        Matrix4X4<float> clip = model * camera.View * camera.Projection;

        target.Bind();
        pipeline.Bind();

        // Each variant must start from a clean buffer. Without this, the pixels
        // left by the previous variant survive and every cull mode reports the
        // same coverage, which hides the very difference being measured.
        gl.ClearColor(0f, 0f, 0f, 1f);
        gl.Clear((uint)(GLEnum.ColorBufferBit | GLEnum.DepthBufferBit));

        if (cullFace.HasValue)
        {
            gl.Enable(GLEnum.CullFace);
            gl.CullFace(cullFace.Value);
        }
        else
        {
            gl.Disable(GLEnum.CullFace);
        }

        pipeline.SetUniform("ObjectToWorld", model);
        pipeline.SetUniform("WorldToObject", model.Invert());
        pipeline.SetUniform("ObjectToClip", clip);
        pipeline.SetUniform("Albedo", new Vector4D<float>(0.9f, 0.2f, 0.2f, 1f));
        pipeline.SetUniform("Metallic", 0.0f);
        pipeline.SetUniform("Roughness", 0.5f);
        pipeline.SetUniform("AmbientIntensity", 0.4f);
        pipeline.SetUniform("Light0Dir", Vector3D.Normalize(new Vector3D<float>(0.4f, -0.7f, 0.6f)));
        pipeline.SetUniform("Light0Color", new Vector3D<float>(1f, 1f, 1f));
        pipeline.SetUniform("Light0Intensity", 1.5f);
        pipeline.SetUniform("CameraPos", camera.Position);
        pipeline.SetUniform("HasAlbedoMap", 0);
        pipeline.SetUniform("HasNormalMap", 0);

        mesh.Draw();
        pipeline.Unbind();

        // Read the cull state back rather than trusting the calls. Three cull modes
        // producing identical output usually means the state never landed, and
        // guessing from pixel coverage alone is unreliable for convex solids.
        bool cullEnabled = gl.IsEnabled(GLEnum.CullFace);
        var modeVal = new int[1];
        gl.GetInteger(GLEnum.CullFaceMode, modeVal);
        gl.GetError();   // drop any error raised before the readback

        byte[] px = target.ReadPixelsRgba();
        int red = 0;
        long sumR = 0, sumG = 0, sumB = 0;
        for (int i = 0; i < px.Length; i += 4)
        {
            int r = px[i], g = px[i + 1], b = px[i + 2];
            if (r > 70 && g < 90 && b < 90) { red++; sumR += r; sumG += g; sumB += b; }
        }
        double ratio = red / (double)(px.Length / 4);
        // Coverage alone cannot tell the winding apart: for a convex cube, culling
        // front or back just swaps which shell is drawn and the silhouette barely
        // moves. The mean colour of the covered pixels does move, because the far
        // shell is shaded by a different set of faces. Compare those instead.
        string mean = red > 0
            ? $"rgb=({sumR / (double)red,6:F1},{sumG / (double)red,5:F1},{sumB / (double)red,5:F1})"
            : "rgb=(n/a)";
        Console.WriteLine($"  [{label,-10}] red = {ratio,7:P2}  {mean}  cull={(cullEnabled ? modeVal[0].ToString() : "off")}  glErr={gl.GetError()}");
    }

    /// <summary>
    /// 渲染一个各向同性的球，测量其在屏幕上的像素包围盒宽高比。
    ///
    /// 球体不受物体尺寸影响，因此其屏幕宽高比直接反映投影是否引入形变：
    /// 接近 1.0 = 无形变；明显偏大/偏小 = 长宽比被扭曲。
    /// </summary>
    private static void RenderSphereAspect(
        HeadlessGraphicsHost host, GL gl, OffscreenTarget target,
        string shaderDir, int width, int height)
    {
        MeshFactory.GetSphere(out Vertex[] sphereVerts, out uint[] sphereIdx, radius: 0.5f);
        using var sphereMesh = new Mesh(host, sphereVerts, sphereIdx);

        using var vs = new Shader(host, ShaderType.VertexShader,
            File.ReadAllText(Path.Combine(shaderDir, "PBR.vert")));
        using var fs = new Shader(host, ShaderType.FragmentShader,
            File.ReadAllText(Path.Combine(shaderDir, "PBR.frag")));
        using var pipeline = new RenderPipeline(host, vs, fs);

        sphereMesh.SetupAttributes(
            pipeline.GetAttribLocation("In_Position"), pipeline.GetAttribLocation("In_Normal"),
            pipeline.GetAttribLocation("In_Tangent"), pipeline.GetAttribLocation("In_Bitangent"),
            pipeline.GetAttribLocation("In_Color"), pipeline.GetAttribLocation("In_TexCoord"));

        var camera = new Camera { Width = width, Height = height, Fov = 50f, Near = 0.1f, Far = 100f };
        var orbit = new OrbitCameraController(camera) { Distance = 8f, Target = Vector3D<float>.Zero };
        camera.Pitch = 0f;
        camera.Yaw = 180f;
        orbit.Update(0.0);

        var model = Matrix4X4.CreateScale(2f) * Matrix4X4.CreateTranslation(Vector3D<float>.Zero);
        var clip = model * camera.View * camera.Projection;

        Console.WriteLine($"  sphere: camAspect={camera.AspectRatio:F3} clipM34={clip.M34:F3} M44={clip.M44:F3}");

        gl.Disable(GLEnum.CullFace);
        target.Bind();
        pipeline.Bind();
        pipeline.SetUniform("ObjectToWorld", model);
        pipeline.SetUniform("WorldToObject", model.Invert());
        pipeline.SetUniform("ObjectToClip", clip);
        pipeline.SetUniform("Albedo", new Vector4D<float>(0.2f, 0.8f, 0.9f, 1f));
        pipeline.SetUniform("Metallic", 0f);
        pipeline.SetUniform("Roughness", 0.5f);
        pipeline.SetUniform("AmbientIntensity", 0.5f);
        pipeline.SetUniform("Light0Dir", Vector3D.Normalize(new Vector3D<float>(0.4f, -0.7f, 0.6f)));
        pipeline.SetUniform("Light0Color", new Vector3D<float>(1f, 1f, 1f));
        pipeline.SetUniform("Light0Intensity", 1.2f);
        pipeline.SetUniform("CameraPos", camera.Position);
        pipeline.SetUniform("HasAlbedoMap", 0);
        pipeline.SetUniform("HasNormalMap", 0);
        sphereMesh.Draw();
        pipeline.Unbind();

        byte[] px = target.ReadPixelsRgba();
        int minX = width, maxX = -1, minY = height, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                int r = px[i], g = px[i + 1], b = px[i + 2];
                // 背景清屏色约 (20,23,28)。青色球 (0.2,0.8,0.9) 的特征是
                // 蓝绿分量显著高于红色，据此与背景区分（阈值放宽以覆盖暗部）。
                if (b > 35 && g > 32 && b - r > 12 && g - r > 8)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < 0)
        {
            Console.WriteLine("    sphere not visible on screen");
            return;
        }

        int w = maxX - minX + 1, h = maxY - minY + 1;
        Console.WriteLine($"    sphere screen bbox = {w}x{h} at ({minX},{minY})");
        Console.WriteLine($"    pixel aspect w/h = {(double)w / h:F3}   (期望 ≈ 1.0)");
    }

    public static int Run(int width, int height, string outDir)
    {
        Console.WriteLine();
        Console.WriteLine("=== minimal render (bypass SimpleDrawingService.Render) ===");

        string shaderDir = Path.Combine(AppContext.BaseDirectory, "Resources", "Shaders");
        using var host = new HeadlessGraphicsHost(width, height);
        GL gl = host.GetContext();

        using var target = new OffscreenTarget(host, width, height);

        // ---- 引擎自己的网格 ----
        MeshFactory.GetCube(out Vertex[] cubeVerts, out uint[] cubeIdx);
        using var cubeMesh = new Mesh(host, cubeVerts, cubeIdx);

        // ---- 引擎自己的着色器 ----
        using var vs = new Shader(host, ShaderType.VertexShader,
            File.ReadAllText(Path.Combine(shaderDir, "PBR.vert")));
        using var fs = new Shader(host, ShaderType.FragmentShader,
            File.ReadAllText(Path.Combine(shaderDir, "PBR.frag")));
        using var pipeline = new RenderPipeline(host, vs, fs);

        cubeMesh.SetupAttributes(
            pipeline.GetAttribLocation("In_Position"),
            pipeline.GetAttribLocation("In_Normal"),
            pipeline.GetAttribLocation("In_Tangent"),
            pipeline.GetAttribLocation("In_Bitangent"),
            pipeline.GetAttribLocation("In_Color"),
            pipeline.GetAttribLocation("In_TexCoord"));

        // ---- 引擎自己的相机：用 OrbitCameraController 定位，与 GPU 探针保持一致 ----
        var camera = new Camera { Width = width, Height = height, Fov = 50f, Near = 0.1f, Far = 100f };
        var orbit = new OrbitCameraController(camera) { Distance = 6f, Target = Vector3D<float>.Zero };
        camera.Pitch = 0f;
        camera.Yaw = 180f;
        orbit.Update(0.0);

        // ---- 物体变换：与 Transform 一致的 Scale * Translation ----
        Matrix4X4<float> model = Matrix4X4.CreateScale(2f) * Matrix4X4.CreateTranslation(Vector3D<float>.Zero);
        Matrix4X4<float> clip = model * camera.View * camera.Projection;

        Console.WriteLine($"  attrib locs  : pos={pipeline.GetAttribLocation("In_Position")}"
                        + $" nrm={pipeline.GetAttribLocation("In_Normal")}"
                        + $" uv={pipeline.GetAttribLocation("In_TexCoord")}");
        Console.WriteLine($"  camera pos   : {camera.Position}");
        Console.WriteLine($"  camera front : {camera.Front}");
        // NOTE: for clip = model * View * Projection the M34 slot is legitimately 0, not -1.
        // The row-vector perspective stores the divide sign in P.M34, which the model
        // scale (M33) then multiplies. clip.M44 keeps the view distance and is the real signal.
        // ---- 球体正圆性实验 ----
        // 球是各向同性的：若屏幕上宽高比接近 1.0，说明投影没有引入形变；
        // 若明显偏离，说明长宽比被扭曲（aspect 错误或矩阵约定仍有冲突）。
        RenderSphereAspect(host, gl, target, shaderDir, width, height);

        // ---- 绕序实验：禁用剔除 / 剔除 Back / 剔除 Front 三种模式 ----
        // 判定立方体三角形在当前约定下到底是 CCW 还是 CW。
        // 覆盖率不足以区分（凸体近壳与远壳轮廓重合），要看覆盖区均值颜色。
        RenderVariant(host, gl, target, pipeline, cubeMesh, camera, model,
                      "nocull", cullFace: null);
        RenderVariant(host, gl, target, pipeline, cubeMesh, camera, model,
                      "cull_back", cullFace: GLEnum.Back);
        RenderVariant(host, gl, target, pipeline, cubeMesh, camera, model,
                      "cull_front", cullFace: GLEnum.Front);

        // Match the engine: back-face culling enabled with CullFace(Back).
        gl.Enable(GLEnum.CullFace);
        gl.CullFace(GLEnum.Back);
        gl.ClearColor(0f, 0f, 0f, 1f);
        gl.Clear((uint)(GLEnum.ColorBufferBit | GLEnum.DepthBufferBit));
        target.Bind();
        pipeline.Bind();

        pipeline.SetUniform("ObjectToWorld", model);
        pipeline.SetUniform("WorldToObject", model.Invert());
        pipeline.SetUniform("ObjectToClip", clip);
        pipeline.SetUniform("Albedo", new Vector4D<float>(0.9f, 0.2f, 0.2f, 1f));
        pipeline.SetUniform("Metallic", 0.0f);
        pipeline.SetUniform("Roughness", 0.5f);
        pipeline.SetUniform("AmbientIntensity", 0.4f);
        pipeline.SetUniform("Light0Dir", Vector3D.Normalize(new Vector3D<float>(0.4f, -0.7f, 0.6f)));
        pipeline.SetUniform("Light0Color", new Vector3D<float>(1f, 1f, 1f));
        pipeline.SetUniform("Light0Intensity", 1.5f);
        pipeline.SetUniform("CameraPos", camera.Position);
        pipeline.SetUniform("HasAlbedoMap", 0);
        pipeline.SetUniform("HasNormalMap", 0);

        cubeMesh.Draw();
        pipeline.Unbind();

        GLEnum err = gl.GetError();
        Console.WriteLine($"  gl error     : {err}");

        byte[] pixels = target.ReadPixelsRgba();
        string path = Path.Combine(outDir, "minimal_cube.png");
        Program.WritePng(pixels, width, height, path);

        int red = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            int r = pixels[i], g = pixels[i + 1], b = pixels[i + 2];
            if (r > 70 && g < 90 && b < 90) red++;
        }
        double ratio = red / (double)(pixels.Length / 4);

        Console.WriteLine($"  red pixels   : {ratio:P2}");
        Console.WriteLine($"  wrote {path}");

        bool ok = ratio > 0.05 && err == GLEnum.NoError;
        Console.WriteLine();
        Console.WriteLine(ok
            ? "RESULT: PASS - engine resources render correctly with a hand-written draw loop."
            : "RESULT: FAIL - engine resources themselves do not render correctly.");
        return ok ? 0 : 9;
    }
}

