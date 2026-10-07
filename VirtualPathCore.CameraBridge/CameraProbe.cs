using Silk.NET.Maths;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Helpers;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 相机数学自检：把 Camera 的 View/Projection 与标准实现逐元素比对。
///
/// 目的：区分"我的场景参数错了"与"引擎的相机数学有 bug"。
/// 若两者一致，则引擎相机正确，问题只在场景构图；
/// 若不一致，则引擎的相机实现存在缺陷，需要修复本体。
/// </summary>
internal static class CameraProbe
{
    public static int Run()
    {
        Console.WriteLine();
        Console.WriteLine("=== camera math probe ===");

        ProbeMatrixMultiplyConvention();

        var camera = new Camera
        {
            Width = 1280,
            Height = 720,
            Near = 0.1f,
            Far = 100f,
            Fov = 50f,
        };

        camera.SetPosition(5.46f, 3.03f, 2.55f);
        camera.LookAt(new Vector3D<float>(0f, 0.6f, 0f));

        Console.WriteLine($"  position  : {camera.Position}");
        Console.WriteLine($"  front     : {camera.Front}");
        Console.WriteLine($"  up        : {camera.Up}");
        Console.WriteLine($"  right     : {camera.Right}");
        Console.WriteLine($"  yaw/pitch : {camera.Yaw:F2} / {camera.Pitch:F2}");
        Console.WriteLine();

        // 期望：front 归一化且指向目标
        Vector3D<float> toTarget = Vector3D.Normalize(new Vector3D<float>(0f, 0.6f, 0f) - camera.Position);
        Console.WriteLine($"  expected front (target - pos, normalized): {toTarget}");
        double frontErr = Vector3D.Distance(camera.Front, toTarget);
        Console.WriteLine($"  |front - expected| = {frontErr:F6}  {(frontErr < 1e-3 ? "OK" : "MISMATCH")}");
        Console.WriteLine();

        // 期望：LookAt 之后 yaw/pitch 反推应与球面参数化一致（自洽性）
        float yawFromFront = MathHelper.RadiansToDegrees(MathF.Atan2(camera.Front.Z, camera.Front.X));
        float pitchFromFront = MathHelper.RadiansToDegrees(MathF.Asin(camera.Front.Y));
        Console.WriteLine($"  yaw   from front = {yawFromFront:F2}   (Camera.Yaw = {camera.Yaw:F2})");
        Console.WriteLine($"  pitch from front = {pitchFromFront:F2}   (Camera.Pitch = {camera.Pitch:F2})");
        Console.WriteLine();

        Matrix4X4<float> view = camera.View;
        Matrix4X4<float> proj = camera.Projection;

        // 物体变换自检：单位立方体（半边长 0.5）经 Transform 后，包围盒应为
        // 位置 ± 尺寸。只有当行向量约定与矩阵书写顺序匹配时才会成立。
        Console.WriteLine("  object transform check:");
        bool transformOk = true;
        foreach (var (pos, scl, label) in new (Vector3D<float>, Vector3D<float>, string)[]
                 {
                     (new Vector3D<float>(1f, 2f, 3f), new Vector3D<float>(1f, 1f, 1f), "pos(1,2,3) scale(1,1,1)"),
                     (new Vector3D<float>(0f, 0f, 0f), new Vector3D<float>(2f, 2f, 2f), "pos(0,0,0) scale(2,2,2)"),
                 })
        {
            var tf = new Transform { Position = pos, Scale = scl };
            Matrix4X4<float> world = tf.WorldMatrix;

            // 取立方体 8 个角点，按行向量约定变换（v' = v * M）
            var min = new Vector3D<float>(float.MaxValue);
            var max = new Vector3D<float>(float.MinValue);
            foreach (var sx in new[] { -0.5f, 0.5f })
            foreach (var sy in new[] { -0.5f, 0.5f })
            foreach (var sz in new[] { -0.5f, 0.5f })
            {
                var corner = new Vector3D<float>(sx, sy, sz);
                var r = Transform(world, corner);
                min = Vector3D.Min(min, new Vector3D<float>(r.X, r.Y, r.Z));
                max = Vector3D.Max(max, new Vector3D<float>(r.X, r.Y, r.Z));
            }

            Vector3D<float> expectedMin = pos - scl * 0.5f;
            Vector3D<float> expectedMax = pos + scl * 0.5f;
            double err = Vector3D.Distance(min, expectedMin) + Vector3D.Distance(max, expectedMax);

            Console.WriteLine($"    {label}");
            Console.WriteLine($"      bbox min = ({min.X:F3}, {min.Y:F3}, {min.Z:F3})   expect ({expectedMin.X:F3}, {expectedMin.Y:F3}, {expectedMin.Z:F3})");
            Console.WriteLine($"      bbox max = ({max.X:F3}, {max.Y:F3}, {max.Z:F3})   expect ({expectedMax.X:F3}, {expectedMax.Y:F3}, {expectedMax.Z:F3})");
            Console.WriteLine($"      error = {err:F4}  {(err < 1e-3 ? "OK" : "MISMATCH -> matrix order / convention wrong")}");
            if (err >= 1e-3) transformOk = false;
        }
        Console.WriteLine();

        // ---- 决定性检查：把真实场景角点一路投影到 NDC ----
        //
        // 前面只验证了单位/等比缩放，而传送带是极端非均匀缩放 (6, 0.3, 2)。
        // 这里复刻引擎的合成顺序 objectToClip = model * view * projection，
        // 并按 GPU 实际收到的形式（transpose 后列向量乘法）计算 NDC。
        Console.WriteLine("  scene corner projection (model * view * proj):");

        var cameraForScene = new Camera
        {
            Width = 1280,
            Height = 720,
            Near = 0.1f,
            Far = 100f,
            Fov = 50f,
        };
        cameraForScene.SetPosition(5.46f, 3.03f, 2.55f);
        cameraForScene.LookAt(new Vector3D<float>(0f, 0.6f, 0f));

        // (name, position, scale) —— 与 DemoScene 一致
        var probes = new (string Name, Vector3D<float> Pos, Vector3D<float> Scale)[]
        {
            ("Conveyor_Belt", new Vector3D<float>(0f, 0.35f, 0f), new Vector3D<float>(6f, 0.3f, 2f)),
            ("Workpiece_Box", new Vector3D<float>(-1.2f, 1.0f, 0f), new Vector3D<float>(1f, 1f, 1f)),
            ("Workpiece_Drum", new Vector3D<float>(1.2f, 1.0f, 0f), new Vector3D<float>(0.9f, 0.5f, 0.9f)),
        };

        foreach (var (name, pos, scl) in probes)
        {
            var tf = new Transform { Position = pos, Scale = scl };
            Matrix4X4<float> model = tf.WorldMatrix;
            Matrix4X4<float> clip = model * cameraForScene.View * cameraForScene.Projection;

            var ndcMin = new Vector2D<float>(float.MaxValue);
            var ndcMax = new Vector2D<float>(float.MinValue);
            bool allFinite = true;

            foreach (var sx in new[] { -0.5f, 0.5f })
            foreach (var sy in new[] { -0.5f, 0.5f })
            foreach (var sz in new[] { -0.5f, 0.5f })
            {
                var corner = new Vector3D<float>(sx, sy, sz);
                var c = Transform(clip, corner);

                if (c.W == 0f || !float.IsFinite(c.X) || !float.IsFinite(c.Y) || !float.IsFinite(c.W))
                {
                    allFinite = false;
                    continue;
                }

                float nx = c.X / c.W;
                float ny = c.Y / c.W;
                ndcMin = Vector2D.Min(ndcMin, new Vector2D<float>(nx, ny));
                ndcMax = Vector2D.Max(ndcMax, new Vector2D<float>(nx, ny));
            }

            bool inView = allFinite
                          && ndcMax.X >= -1.2f && ndcMin.X <= 1.2f
                          && ndcMax.Y >= -1.2f && ndcMin.Y <= 1.2f;

            Console.WriteLine($"    {name,-16} NDC x[{ndcMin.X,7:F3},{ndcMax.X,7:F3}] y[{ndcMin.Y,7:F3},{ndcMax.Y,7:F3}]"
                            + $"  {(allFinite ? "" : "NON-FINITE ")}{(inView ? "on-screen" : "OFF-SCREEN")}");
        }
        Console.WriteLine();

        // View 自检：把目标点变换到视图空间，应得到 (0, 0, -distance)
        Vector4D<float> targetView = Transform(view, new Vector3D<float>(0f, 0.6f, 0f));
        Console.WriteLine($"  target in view space = ({targetView.X:F3}, {targetView.Y:F3}, {targetView.Z:F3}, {targetView.W:F3})");
        Console.WriteLine($"  expected  = (0, 0, -{Vector3D.Distance(camera.Position, new Vector3D<float>(0f, 0.6f, 0f)):F3}, 1)");
        Console.WriteLine();

        // Projection 自检：视空间中 z = -near 的点，NDC z 应为 -1。
        //
        // 注意不能沿 Front 前移：Front 是单位向量，而 near/far 定义在<b>视图空间 z</b> 上
        // （视图空间 z 为负方向）。直接在视图空间构造点，绕开朝向换算。
        Vector3D<float> nearPointView = new(0f, 0f, -camera.Near);   // 正前方、恰好在近平面
        Vector4D<float> nearClip = Transform(proj, nearPointView);
        Console.WriteLine($"  view-space point (0,0,-near), clip = ({nearClip.X:F3}, {nearClip.Y:F3}, {nearClip.Z:F3}, {nearClip.W:F3})");
        double ndcZ = nearClip.W != 0 ? nearClip.Z / nearClip.W : double.NaN;
        Console.WriteLine($"  NDC z = {ndcZ:F4}  (期望 -1.0)");

        Vector3D<float> farPointView = new(0f, 0f, -camera.Far);
        Vector4D<float> farClip = Transform(proj, farPointView);
        double ndcZFar = farClip.W != 0 ? farClip.Z / farClip.W : double.NaN;
        Console.WriteLine($"  view-space point (0,0,-far),  NDC z = {ndcZFar:F4}  (期望 +1.0)");
        Console.WriteLine();

        // Projection 自检：行向量布局下，透视除法的 -1 落在 M34，近/远偏移落在 M43
        Console.WriteLine("  projection matrix (row-vector layout):");
        Console.WriteLine($"    M41={proj.M41:F4}  M42={proj.M42:F4}  M43={proj.M43:F4}  M44={proj.M44:F4}   (前三个应为 0)");
        Console.WriteLine($"    M14={proj.M14:F4}  M24={proj.M24:F4}  M34={proj.M34:F4}  M44={proj.M44:F4}   (M34 应为 -1)");
        Console.WriteLine($"    M11={proj.M11:F4}  M22={proj.M22:F4}  M33={proj.M33:F4}");

        // 注意：本引擎的近/远平面映射到 NDC z ∈ [0,1]（D3D 约定），
        // 而非 OpenGL 的 [-1,1]，因此近平面期望值为 0。
        bool ok = frontErr < 1e-3
                  && Math.Abs(targetView.X) < 1e-3
                  && Math.Abs(targetView.Y) < 1e-3
                  && Math.Abs(targetView.Z + Vector3D.Distance(camera.Position, new Vector3D<float>(0f, 0.6f, 0f))) < 1e-2
                  && Math.Abs(ndcZ) < 0.01
                  && Math.Abs(ndcZFar - 1.0) < 0.01
                  && Math.Abs(proj.M41) < 1e-6 && Math.Abs(proj.M42) < 1e-6
                  && Math.Abs(proj.M44) < 1e-6
                  && Math.Abs(proj.M34 + 1.0) < 1e-6
                  && transformOk;

        Console.WriteLine();
        Console.WriteLine(ok ? "RESULT: PASS - camera math is consistent." : "RESULT: FAIL - camera math has a defect.");
        return ok ? 0 : 6;
    }

    /// <summary>
    /// 复刻 GPU 侧的实际运算：<c>v' = v * M</c>（行向量约定）。
    ///
    /// <para>推导链：</para>
    /// <list type="number">
    /// <item>矩阵按行主序存于 M11..M44（实测 CreateTranslation 的平移落在 M41）。</item>
    /// <item><c>RenderPipeline.SetUniform</c> 以 <c>transpose: true</c> 上传，
    /// 于是 GPU 内部按列主序解释这块内存，等价于收到 M^T。</item>
    /// <item>着色器执行 <c>ObjectToClip * vec4(pos, 1)</c>，即 (M^T) * v = v * M。</item>
    /// </list>
    /// 所以探针必须用 <c>v * M</c>，否则结论会与渲染结果矛盾。
    /// </summary>
    private static Vector4D<float> Transform(Matrix4X4<float> m, Vector3D<float> v)
    {
        return new Vector4D<float>(
            v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31 + m.M41,
            v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + m.M42,
            v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + m.M43,
            v.X * m.M14 + v.Y * m.M24 + v.Z * m.M34 + m.M44);
    }

    /// <summary>
    /// 实测 Silk.NET.Maths 的矩阵乘法语义。
    ///
    /// <para><c>A * B</c> 在列向量约定下应等价于"先 B 后 A"（<c>(A*B)*v == A*(B*v)</c>），
    /// 在行向量约定下则相反。这里用一个纯平移矩阵与非平凡矩阵相乘，
    /// 通过结果矩阵的平移分量落在哪一行/哪一列来判定实际约定。</para>
    /// </summary>
    private static void ProbeMatrixMultiplyConvention()
    {
        Console.WriteLine("  matrix multiply convention:");

        // 纯 X 平移
        Matrix4X4<float> translateX = Matrix4X4.CreateTranslation(new Vector3D<float>(10f, 0f, 0f));

        // 纯 X 缩放
        Matrix4X4<float> scaleX = Matrix4X4.CreateScale(2f);

        Console.WriteLine($"    CreateTranslation(10,0,0): M14={translateX.M14:F1} M41={translateX.M41:F1}"
                        + $"  -> 平移在第 1 列 = 列向量约定");
        Console.WriteLine($"    CreateScale(2):             M11={scaleX.M11:F1} M14={scaleX.M14:F1}");

        var product = translateX * scaleX;
        Console.WriteLine($"    T * S : M14={product.M14:F1} M41={product.M41:F1} M11={product.M11:F1}");
        Console.WriteLine($"    S * T : M14={(scaleX * translateX).M14:F1} M41={(scaleX * translateX).M41:F1} M11={(scaleX * translateX).M11:F1}");

        // 判定：若 T*S 的平移是 10（未被缩放），说明乘法是"先 T 后 S"（行向量）
        //      若平移是 20（被缩放），说明是"先 S 后 T"（列向量）
        Matrix4X4<float> ts = translateX * scaleX;
        double tOffset = Math.Abs(ts.M14) > 0.01 ? ts.M14 : ts.M41;
        bool rowVectorSemantics = Math.Abs(tOffset - 10.0) < 0.01;

        Console.WriteLine();
        Console.WriteLine($"    => Silk.NET 的 A*B 语义: {(rowVectorSemantics ? "行向量（先 A 后 B）" : "列向量（先 B 后 A）")}");
        Console.WriteLine($"    => 引擎应使用顺序: {(rowVectorSemantics ? "Scale * Rotation * Translation" : "Translation * Rotation * Scale")}");
        Console.WriteLine();
    }
}
