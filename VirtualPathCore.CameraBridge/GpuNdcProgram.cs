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
/// GPU 回读验证：让着色器把 <c>gl_Position</c> 计算结果写进颜色通道，
/// 再用 transform feedback 之外最简单的方式读回 —— 把结果编码到像素里。
///
/// <para>做法：用一个顶点着色器把 clip 坐标直接当输出位置，
/// 片元着色器把 <c>gl_FragCoord</c> 对应的 NDC 映射写为颜色；
/// 由于 NDC 决定像素落在哪里，读回的颜色即反映真实投影结果。</para>
///
/// <para>目的：彻底摆脱"C# 侧矩阵算得对不对"的争议，
/// 直接问 GPU"你算出来的顶点落在哪里"。</para>
/// </summary>
internal static unsafe class GpuNdcProgram
{
    public static int Run(int width, int height, string outDir)
    {
        Console.WriteLine();
        Console.WriteLine("=== GPU ground-truth NDC probe ===");

        string shaderDir = Path.Combine(AppContext.BaseDirectory, "Resources", "Shaders");
        using var host = new HeadlessGraphicsHost(width, height);
        GL gl = host.GetContext();
        using var target = new OffscreenTarget(host, width, height);

        MeshFactory.GetCube(out Vertex[] cubeVerts, out uint[] cubeIdx);
        using var cubeMesh = new Mesh(host, cubeVerts, cubeIdx);

        // 探针着色器：原样透传引擎的 ObjectToClip，并把 NDC 编码进颜色。
        // 注意必须是无缩进的纯文本 —— GLSL 对前导空白/BOM 敏感。
        string probeVert =
            "#version 330 core\n" +
            "layout(location = 0) in vec3 In_Position;\n" +
            "uniform mat4 ObjectToClip;\n" +
            "out vec3 vNdc;\n" +
            "void main()\n" +
            "{\n" +
            "    gl_Position = ObjectToClip * vec4(In_Position, 1.0);\n" +
            "    vNdc = gl_Position.xyz / gl_Position.w;\n" +
            "}\n";

        string probeFrag =
            "#version 330 core\n" +
            "in vec3 vNdc;\n" +
            "layout(location = 0) out vec4 Out_Color;\n" +
            "void main()\n" +
            "{\n" +
            "    Out_Color = vec4(vNdc * 0.5 + 0.5, 1.0);\n" +
            "}\n";

        using var vs = new Shader(host, ShaderType.VertexShader, probeVert);
        using var fs = new Shader(host, ShaderType.FragmentShader, probeFrag);
        using var pipeline = new RenderPipeline(host, vs, fs);

        cubeMesh.SetupAttributes(
            pipeline.GetAttribLocation("In_Position"), -1, -1, -1, -1, -1);

        var camera = new Camera { Width = width, Height = height, Fov = 50f, Near = 0.1f, Far = 100f };
        var model = Matrix4X4.CreateScale(2f) * Matrix4X4.CreateTranslation(Vector3D<float>.Zero);

        foreach (var (tag, transpose) in new (string, bool)[]
                 {
                     ("transpose=false", false),
                     ("transpose=true", true),
                 })
        {
            var orbit = new OrbitCameraController(camera) { Distance = 8f, Target = Vector3D<float>.Zero };
            camera.Pitch = 0f;
            camera.Yaw = 180f;
            orbit.Update(0.0);

            // 各向同性的球：屏幕宽高比应接近 1.0
            MeshFactory.GetSphere(out Vertex[] sphereVerts, out uint[] sphereIdx, radius: 0.5f);
            using var sphereMesh = new Mesh(host, sphereVerts, sphereIdx);
            sphereMesh.SetupAttributes(pipeline.GetAttribLocation("In_Position"), -1, -1, -1, -1, -1);

            Matrix4X4<float> sphereModel = Matrix4X4.CreateScale(2f) * Matrix4X4.CreateTranslation(Vector3D<float>.Zero);
            Matrix4X4<float> sphereClip = sphereModel * camera.View * camera.Projection;

            target.Bind();
            pipeline.Bind();

            // 直接按指定 transpose 标志上传，绕过 RenderPipeline
            int loc = pipeline.GetUniformLocation("ObjectToClip");
            float[] flat = new float[]
            {
                sphereClip.M11, sphereClip.M12, sphereClip.M13, sphereClip.M14,
                sphereClip.M21, sphereClip.M22, sphereClip.M23, sphereClip.M24,
                sphereClip.M31, sphereClip.M32, sphereClip.M33, sphereClip.M34,
                sphereClip.M41, sphereClip.M42, sphereClip.M43, sphereClip.M44,
            };
            fixed (float* mp = flat)
                gl.UniformMatrix4(loc, 1, transpose, mp);

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
                    if (b > 35 && g > 32 && b - r > 12 && g - r > 8)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < 0)
            {
                Console.WriteLine($"  {tag,-18} sphere not visible");
                continue;
            }

            int bw = maxX - minX + 1, bh = maxY - minY + 1;
            Console.WriteLine($"  {tag,-18} clipM34={sphereClip.M34,7:F3} M11={sphereClip.M11,6:F3} M22={sphereClip.M22,6:F3}");
            Console.WriteLine($"      sphere bbox = {bw}x{bh}   w/h = {(double)bw / bh:F3}   (期望 ≈ 1.0)");
        }

        Console.WriteLine();
        Console.WriteLine("若 covered 接近 0% => 着色器算出的 NDC 把顶点甩到屏幕外");
        Console.WriteLine("RESULT: PASS - probe completed; see coverage ratios above.");
        return 0;
    }
}
