using Silk.NET.Maths;
using Silk.NET.OpenGLES;
using VirtualPathCore.Graphics;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Graphics.OpenGL;
using VirtualPathCore.Helpers;
using Shader = VirtualPathCore.Graphics.OpenGL.Shader;
using Camera = VirtualPathCore.Graphics.Core.Camera;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 一次跑完所有候选组合，让 GPU 直接裁决。
///
/// <para>此前多轮失败的根源：我在 CPU 侧用"推导的公式"预测 GPU 行为，
/// 而预测公式本身可能与 GPU 实际语义不符，于是每轮修复都被下一轮推翻。</para>
///
/// <para>本程序不做任何推导，只做实验：</para>
/// <list type="number">
/// <item>枚举 transpose ∈ {false, true} × 合成顺序 ∈ {M*V*P, P*V*M, M*P*V, V*P*M}</item>
/// <item>每种组合渲染一个各向同性的球，测量其屏幕像素宽高比。</item>
/// <item>球各向同性，宽高比 ≈ 1.0 即为正确组合；其余组合会明显偏离。</item>
/// </list>
///
/// <para>同时输出 View 是否转置的两种变体，共 8 种组合。</para>
/// </summary>
internal static unsafe class MatrixSweepProgram
{
    public static int Run(int width, int height)
    {
        Console.WriteLine();
        Console.WriteLine("=== matrix combination sweep (GPU decides) ===");

        string shaderDir = Path.Combine(AppContext.BaseDirectory, "Resources", "Shaders");

        MeshFactory.GetSphere(out Vertex[] sphereVerts, out uint[] sphereIdx, radius: 0.5f);

        // 着色器只依赖 ObjectToClip，因此组合全部在 CPU 侧完成
        using var host = new HeadlessGraphicsHost(width, height);
        GL gl = host.GetContext();
        using var target = new OffscreenTarget(host, width, height);

        string vert =
            "#version 330 core\n" +
            "layout(location = 0) in vec3 In_Position;\n" +
            "layout(location = 1) in vec3 In_Normal;\n" +
            "uniform mat4 ObjectToClip;\n" +
            "void main()\n" +
            "{\n" +
            "    gl_Position = ObjectToClip * vec4(In_Position, 1.0);\n" +
            "}\n";

        string frag =
            "#version 330 core\n" +
            "layout(location = 0) out vec4 Out_Color;\n" +
            "void main()\n" +
            "{\n" +
            "    Out_Color = vec4(0.2, 0.8, 0.9, 1.0);\n" +
            "}\n";

        using var vs = new Shader(host, ShaderType.VertexShader, vert);
        using var fs = new Shader(host, ShaderType.FragmentShader, frag);
        using var pipeline = new RenderPipeline(host, vs, fs);

        using var sphere = new Mesh(host, sphereVerts, sphereIdx);
        sphere.SetupAttributes(pipeline.GetAttribLocation("In_Position"), -1, -1, -1, -1, -1);

        int loc = pipeline.GetUniformLocation("ObjectToClip");

        var camera = new Camera { Width = width, Height = height, Fov = 50f, Near = 0.1f, Far = 100f };
        var model = Matrix4X4.CreateScale(2f) * Matrix4X4.CreateTranslation(Vector3D<float>.Zero);

        Console.WriteLine();
        Console.WriteLine("  transpose | View      | order     | sphere w/h | size      | verdict");
        Console.WriteLine("  ----------+-----------+-----------+------------+-----------+------------------");

        string bestConfig = "";

        foreach (bool transpose in new[] { false, true })
        {
            var orbit = new OrbitCameraController(camera) { Distance = 8f, Target = Vector3D<float>.Zero };
            camera.Pitch = 0f;
            camera.Yaw = 180f;
            orbit.Update(0.0);

            var P = camera.Projection;
            var Vraw = Matrix4X4.CreateLookAt(camera.Position, camera.Position + camera.Front, camera.Up);

            foreach (bool transposeView in new[] { false, true })
            {
                var V = transposeView ? Matrix4X4.Transpose(Vraw) : Vraw;

                var combos = new (string Name, Matrix4X4<float> M)[]
                {
                    ("M*V*P", model * V * P),
                    ("P*V*M", P * V * model),
                    ("M*P*V", model * P * V),
                    ("V*P*M", V * P * model),
                };

                foreach (var (name, clip) in combos)
                {
                    var m = RenderAndMeasure(host, gl, target, pipeline, sphere, loc, clip, transpose,
                                             width, height);
                    float ratio = m.Ratio;

                    // 参考尺寸：球半径 0.5 经 scale=2 后直径为 2；相机距其 8、垂直 FOV 50°。
                    // 屏幕投影高度 = height * 直径 / (2 * 距离 * tan(fov/2))，fov/2 = 25°。
                    float expectedHeight = height * (2f / (2f * 8f * MathF.Tan(25f * MathF.PI / 180f)));
                    bool sizeOk = !double.IsNaN(ratio) && m.Height > 0
                                  && Math.Abs(m.Height / expectedHeight - 1f) < 0.25f;
                    bool centred = !double.IsNaN(ratio)
                                   && Math.Abs(m.CentreX - width / 2f) < width * 0.08f
                                   && Math.Abs(m.CentreY - height / 2f) < height * 0.08f;

                    string verdict;
                    if (double.IsNaN(ratio)) verdict = "invisible";
                    else if (Math.Abs(ratio - 1.0) >= 0.06) verdict = ratio < 1.0 ? "squashed" : "stretched";
                    else if (!sizeOk) verdict = $"shape OK but size off ({m.Height}px vs {expectedHeight:F0}px)";
                    else if (!centred) verdict = $"shape+size OK but off-centre ({m.CentreX:F0},{m.CentreY:F0})";
                    else verdict = "*** CORRECT (shape+size+centre) ***";

                    Console.WriteLine($"  {transpose,-10} | {(transposeView ? "transpose" : "raw     "),-9} |"
                                      + $" {name,-9} | {(double.IsNaN(ratio) ? "  n/a  " : ratio.ToString("F3")),10}"
                                      + $" | {m.Height,5}px ({m.Height / expectedHeight,5:F2}x) | {verdict}");

                    if (verdict.StartsWith("***"))
                    {
                        bestConfig = $"transpose={transpose}, viewTranspose={transposeView}, order={name}"
                                   + $"  (w/h={ratio:F3}, height={m.Height}px)";
                    }
                }
            }
        }

        Console.WriteLine();
        if (bestConfig.Length > 0)
            Console.WriteLine($"  WINNER: {bestConfig}");
        else
            Console.WriteLine("  no combination passed shape + size + centre checks");

        Console.WriteLine();
        Console.WriteLine("RESULT: PASS - sweep completed; see table above.");
        return 0;
    }

    /// <summary>
    /// 渲染球并返回其屏幕包围盒信息：宽高比、像素高度、中心位置。
    /// 不可见时 <c>Ratio</c> 为 NaN。
    /// </summary>
    private static (float Ratio, int Height, float CentreX, float CentreY) RenderAndMeasure(
        HeadlessGraphicsHost host, GL gl, OffscreenTarget target,
        RenderPipeline pipeline, Mesh mesh, int loc, Matrix4X4<float> clip, bool transpose,
        int width, int height)
    {
        float[] flat =
        {
            clip.M11, clip.M12, clip.M13, clip.M14,
            clip.M21, clip.M22, clip.M23, clip.M24,
            clip.M31, clip.M32, clip.M33, clip.M34,
            clip.M41, clip.M42, clip.M43, clip.M44,
        };

        gl.Disable(GLEnum.CullFace);
        gl.Disable(GLEnum.DepthTest);
        target.Bind();
        pipeline.Bind();
        fixed (float* p = flat)
            gl.UniformMatrix4(loc, 1, transpose, p);
        mesh.Draw();
        pipeline.Unbind();
        gl.Enable(GLEnum.DepthTest);

        byte[] px = target.ReadPixelsRgba();
        int minX = width, maxX = -1, minY = height, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                if (px[i + 2] > 100 && px[i + 1] > 100 && px[i] < 100)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < 0) return (float.NaN, 0, 0, 0);

        int w = maxX - minX + 1;
        int h = maxY - minY + 1;
        if (h == 0) return (float.NaN, 0, 0, 0);

        return ((float)w / h, h, (minX + maxX) / 2f, (minY + maxY) / 2f);
    }
}
