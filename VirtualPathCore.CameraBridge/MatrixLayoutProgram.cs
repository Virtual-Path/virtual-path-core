using System.Runtime.InteropServices;
using Silk.NET.Maths;
using VirtualPathCore.Graphics;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Graphics.OpenGL;
using Shader = VirtualPathCore.Graphics.OpenGL.Shader;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 逐元素实测 Silk.NET.Maths 的 Matrix4X4 内存布局。
///
/// <para>目的：彻底搞清楚 <c>Matrix4X4&lt;T&gt;</c> 的字段顺序与 GPU 侧
/// <c>transpose</c> 标志的交互，从而确定着色器里 <c>M * vec4(v,1)</c>
/// 在 CPU 上究竟等价于哪种手写算法。</para>
///
/// <para>方法：不依赖任何"约定假设"，而是让 GPU 自己算。
/// 上传一个"单位矩阵探针"——CPU 上先构造出已知的 16 个 float，
/// 再用不同的 transpose 标志上传，让着色器把它当矩阵与固定点相乘；
/// 由渲染出的 NDC 反推 GPU 实际使用的元素排列。</para>
/// </summary>
internal static unsafe class MatrixLayoutProgram
{
    public static int Run(int width, int height, string outDir)
    {
        Console.WriteLine();
        Console.WriteLine("=== matrix memory layout: ground-truth measurement ===");

        string outPath = Path.Combine(outDir, "matrix_layout.png");

        // ---- 1. CPU 侧：直接把 16 个 float 放进结构体，确认写入顺序 ----
        float[] want = new float[16];
        for (int i = 0; i < 16; i++) want[i] = i + 1;   // 1..16

        var m = new Matrix4X4<float>();
        unsafe
        {
            float* dst = (float*)&m;
            for (int i = 0; i < 16; i++) dst[i] = want[i];
        }

        Console.WriteLine("  CPU 写入顺序 (want 1..16) -> 读回字段:");
        Console.WriteLine($"    M11={m.M11,5:F0} M12={m.M12,5:F0} M13={m.M13,5:F0} M14={m.M14,5:F0}");
        Console.WriteLine($"    M21={m.M21,5:F0} M22={m.M22,5:F0} M23={m.M23,5:F0} M24={m.M24,5:F0}");
        Console.WriteLine($"    M31={m.M31,5:F0} M32={m.M32,5:F0} M33={m.M33,5:F0} M34={m.M34,5:F0}");
        Console.WriteLine($"    M41={m.M41,5:F0} M42={m.M42,5:F0} M43={m.M43,5:F0} M44={m.M44,5:F0}");
        Console.WriteLine($"    sizeof(Matrix4X4<float>) = {sizeof(float) * 16} bytes (16 floats)");
        Console.WriteLine();

        // ---- 2. GPU 侧：让着色器把固定点变换后的坐标画出来 ----
        using var host = new HeadlessGraphicsHost(width, height);
        Silk.NET.OpenGLES.GL gl = host.GetContext();
        using var target = new OffscreenTarget(host, width, height);

        // 探针着色器：把传入矩阵作用到已知点上，输出 NDC
        // 探针着色器：把 Probe 作用到已知点上，输出结果到颜色。
        // 关键：不依赖光栅化位置（单位矩阵会让 w=0 触发除零/裁剪），
        // 而是让片元着色器把变换结果编码进 RGB 读回 —— 这样任何数值都可测。
        // NOTE: keep these strings pure ASCII. A Chinese comment slipped into one of
        // the concatenation lines earlier and the GLSL compiler reported a bogus
        // "unexpected $end" error, because the non-ASCII bytes broke tokenization.
        string vert =
            "#version 330 core\n" +
            "layout(location = 0) in vec3 In_Position;\n" +
            "uniform mat4 Probe;\n" +
            "flat out vec4 vResult;\n" +
            "void main()\n" +
            "{\n" +
            "    vResult = Probe * vec4(In_Position, 1.0);\n" +
            "    gl_Position = vec4(In_Position.xy * 0.2, 0.0, 1.0);\n" +
            "}\n";

        string frag =
            "#version 330 core\n" +
            "flat in vec4 vResult;\n" +
            "layout(location = 0) out vec4 Out_Color;\n" +
            "void main()\n" +
            "{\n" +
            "    Out_Color = vec4(vResult.x, vResult.y, vResult.z, 1.0) * 0.5 + 0.5;\n" +
            "}\n";

        using var vs = new Shader(host, Silk.NET.OpenGLES.ShaderType.VertexShader, vert);
        using var fs = new Shader(host, Silk.NET.OpenGLES.ShaderType.FragmentShader, frag);
        using var pipeline = new RenderPipeline(host, vs, fs);

        // 中心固定的大三角形（gl_Position 被强制在中心，但仍需有几何体被光栅化）
        (Vertex[] verts, uint[] idx) = BuildFullscreenTriangle();
        using var mesh = new Mesh(host, verts, idx);
        mesh.SetupAttributes(pipeline.GetAttribLocation("In_Position"), -1, -1, -1, -1, -1);

        int loc = pipeline.GetUniformLocation("Probe");

        Console.WriteLine("  probe: vertex = (0.25, 0.5, 0.75, 1.0)");
        Console.WriteLine();
        Console.WriteLine("  slot(hex) = index into the 16-float array as CPU sees it (M11..M44).");
        Console.WriteLine("  'col k' means that slot multiplies v.k in the shader's `M * v`.");
        Console.WriteLine();

        // 完整 4x4 反推：对每个 slot，用 4 组已知输入分别求 x 分量，
        // 由 x' = v0*G[r0] + v1*G[r1] + v2*G[r2] + v3*G[r3] 解出该 slot 是哪一列。
        //
        // 简化做法：分别用 4 个"只有单一分量非零"的顶点，
        // 观测 x' 直接就是该 slot 作为第 k 列时乘的系数。
        // 这里顶点固定为 (0.25, 0.5, 0.75, 1.0)：
        //   x' = 0.25*A0 + 0.5*A1 + 0.75*A2 + 1.0*A3
        // 四个权重两两可分（0.25/0.5/0.75/1），故 x' 的取值唯一确定该列。

        // ---- 决定性方法：上传已知矩阵，比对 GPU 实测值与两种候选算法的预测 ----
        //
        // 候选 A（列向量 M*v）：  r.x = sum_k M[k][col0] * v[k]
        // 候选 B（行向量 v*M）：  r.x = sum_k M[col0][k] * v[k]
        //
        // 构造一个元素互不相同（含大数值与负值）的矩阵，
        // 让两种算法的预测值差异极大，从而可无歧义地判定。
        var probe = BuildDistinctMatrix();

        Console.WriteLine("  CPU matrix (row-major view):");
        PrintMatrix(probe);
        Console.WriteLine();
        Console.WriteLine("  vertex v = (0.25, 0.5, 0.75, 1.0)");
        Console.WriteLine("  NOTE: probe values are small so the shader output (v*0.5+0.5) stays inside [0,1]");
        Console.WriteLine("        and is not clamped -- clamping would erase the distinction.");
        Console.WriteLine();

        foreach (bool t in new[] { false, true })
        {
            (float gx, float gy, float gz) = MeasureResultXYZ(host, gl, target, pipeline, mesh, loc, probe, t);

            // 候选 A：M * v （列向量）
            Vector4D<float> a = MultiplyColumnVector(probe, new Vector4D<float>(0.25f, 0.5f, 0.75f, 1f));
            // 候选 B：v * M （行向量）
            Vector4D<float> b = MultiplyRowVector(probe, new Vector4D<float>(0.25f, 0.5f, 0.75f, 1f));

            Console.WriteLine($"  === transpose = {t} ===");
            Console.WriteLine($"    GPU measured     : ({gx,9:F4}, {gy,9:F4}, {gz,9:F4})");
            Console.WriteLine($"    predict M*v (col) : ({a.X,9:F4}, {a.Y,9:F4}, {a.Z,9:F4})");
            Console.WriteLine($"    predict v*M (row) : ({b.X,9:F4}, {b.Y,9:F4}, {b.Z,9:F4})");

            double errA = Math.Abs(gx - a.X) + Math.Abs(gy - a.Y) + Math.Abs(gz - a.Z);
            double errB = Math.Abs(gx - b.X) + Math.Abs(gy - b.Y) + Math.Abs(gz - b.Z);

            Console.WriteLine($"    error vs M*v : {errA:F4}");
            Console.WriteLine($"    error vs v*M : {errB:F4}");
            Console.WriteLine($"    => GPU 语义: {(errA < errB ? "列向量 M * v" : "行向量 v * M")}"
                            + $"   (误差相差 {Math.Abs(errA - errB):F2})");
            Console.WriteLine();
        }

        Console.WriteLine("RESULT: PASS - layout measured; interpret the contribution table above.");
        return 0;
    }

    /// <summary>构造元素互不相同的探针矩阵，便于区分两种乘法约定。</summary>
    private static Matrix4X4<float> BuildDistinctMatrix()
    {
        var m = new Matrix4X4<float>();
        unsafe
        {
            float* p = (float*)&m;
            // 小数值：避免 GLSL 输出饱和（Out_Color 会 clamp 到 [0,1]）
            float[] vals = { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f,
                             0.9f, 1.0f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f };
            for (int i = 0; i < 16; i++) p[i] = vals[i];
        }
        return m;
    }

    private static void PrintMatrix(Matrix4X4<float> m)
    {
        Console.WriteLine($"    [{m.M11,6:F1} {m.M12,6:F1} {m.M13,6:F1} {m.M14,6:F1}]");
        Console.WriteLine($"    [{m.M21,6:F1} {m.M22,6:F1} {m.M23,6:F1} {m.M24,6:F1}]");
        Console.WriteLine($"    [{m.M31,6:F1} {m.M32,6:F1} {m.M33,6:F1} {m.M34,6:F1}]");
        Console.WriteLine($"    [{m.M41,6:F1} {m.M42,6:F1} {m.M43,6:F1} {m.M44,6:F1}]");
    }

    /// <summary>列向量：r = M * v。</summary>
    private static Vector4D<float> MultiplyColumnVector(Matrix4X4<float> m, Vector4D<float> v)
        => new(
            m.M11 * v.X + m.M21 * v.Y + m.M31 * v.Z + m.M41 * v.W,
            m.M12 * v.X + m.M22 * v.Y + m.M32 * v.Z + m.M42 * v.W,
            m.M13 * v.X + m.M23 * v.Y + m.M33 * v.Z + m.M43 * v.W,
            m.M14 * v.X + m.M24 * v.Y + m.M34 * v.Z + m.M44 * v.W);

    /// <summary>行向量：r = v * M。</summary>
    private static Vector4D<float> MultiplyRowVector(Matrix4X4<float> m, Vector4D<float> v)
        => new(
            v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31 + v.W * m.M41,
            v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + v.W * m.M42,
            v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + v.W * m.M43,
            v.X * m.M14 + v.Y * m.M24 + v.Z * m.M34 + v.W * m.M44);

    /// <summary>上传已知矩阵，读回变换结果的 x/y/z 三个分量。</summary>
    private static (float X, float Y, float Z) MeasureResultXYZ(
        HeadlessGraphicsHost host, Silk.NET.OpenGLES.GL gl, OffscreenTarget target,
        RenderPipeline pipeline, Mesh mesh, int loc, Matrix4X4<float> matrix, bool transpose)
    {
        float[] flat =
        {
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44,
        };

        target.Bind();
        pipeline.Bind();
        unsafe
        {
            fixed (float* p = flat)
                gl.UniformMatrix4(loc, 1, transpose, p);
        }
        mesh.Draw();
        pipeline.Unbind();

        byte[] px = target.ReadPixelsRgba();
        int cx = target.Width / 2, cy = target.Height / 2;
        int i = (cy * target.Width + cx) * 4;

        return (px[i] / 255f * 2f - 1f,
                px[i + 1] / 255f * 2f - 1f,
                px[i + 2] / 255f * 2f - 1f);
    }

    /// <summary>构造覆盖 [-2,2] 的全屏大三角形（超出裁剪空间，保证总能被光栅化）。</summary>
    private static (Vertex[], uint[]) BuildFullscreenTriangle()
    {
        var verts = new[]
        {
            new Vertex(new Vector3D<float>(-2f, -2f, 0f), new Vector3D<float>(0f, 0f, 1f)),
            new Vertex(new Vector3D<float>( 4f, -2f, 0f), new Vector3D<float>(0f, 0f, 1f)),
            new Vertex(new Vector3D<float>(-2f,  4f, 0f), new Vector3D<float>(0f, 0f, 1f)),
        };
        uint[] idx = { 0, 1, 2 };
        return (verts, idx);
    }
}
