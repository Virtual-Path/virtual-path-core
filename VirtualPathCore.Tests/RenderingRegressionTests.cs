using System;
using System.IO;
using Silk.NET.Maths;
using VirtualPathCore.Graphics;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Helpers;
using Xunit;

namespace VirtualPathCore.Tests;

/// <summary>
/// 锁定渲染链路中"约定在脑子里、类型系统管不着"的那几处。
///
/// 这些断言的存在理由不是覆盖功能，而是证明判据对它们要抓的错误敏感：
/// 每条断言都配有下方的注释说明"若有人改错，这里会红"。
/// 判据本身如果无法对目标错误转红，就不算判据。
/// </summary>
public class RenderingRegressionTests
{
    /// <summary>
    /// MeshFactory 产出的顶点必须带非零、有限、单位长度的切线。
    ///
    /// 敏感度：若去掉 EnsureTangents 调用，GetSphere 等图元返回的
    /// Tangent/Bitangent 为零向量，着色器对其 normalize() 得 NaN。
    /// </summary>
    [Theory]
    [InlineData("GetCube")]
    [InlineData("GetSphere")]
    [InlineData("GetCylinder")]
    [InlineData("GetCone")]
    [InlineData("GetTorus")]
    [InlineData("GetCanvas")]
    public void Primitives_Should_ProduceFiniteUnitTangents(string factory)
    {
        Vertex[] vertices = BuildPrimitive(factory);

        Assert.NotNull(vertices);
        Assert.NotEmpty(vertices);

        for (int i = 0; i < vertices.Length; i++)
        {
            AssertFinite(vertices[i].Tangent, vertices[i].Position, factory, i, "Tangent");
            AssertFinite(vertices[i].Bitangent, vertices[i].Position, factory, i, "Bitangent");

            Assert.True(IsFinite(vertices[i].Normal), $"{factory}[{i}] Normal is not finite");
            Assert.True(
                Length(vertices[i].Normal) > 1e-4f,
                $"{factory}[{i}] Normal is a zero vector; normalize() would yield NaN");
        }
    }

    /// <summary>
    /// 各向同性球经完整矩阵链 model * view * projection 后，
    /// 在 NDC 上必须同时满足形状、尺寸、居中三项。
    ///
    /// 这是 GPU 裁决（MatrixSweepProgram）结论的 CPU 侧等价物：
    /// 各向同性物体对均匀缩放不敏感，只看宽高比无法区分整体放大 3.7 倍。
    /// 因此三项缺一不可。
    ///
    /// 敏感度：若给 Camera.View 加 Transpose，尺寸项立即超出容差。
    /// </summary>
    [Theory]
    [InlineData(0f)]    // 平视
    [InlineData(20f)]   // 俯视
    [InlineData(-20f)]  // 仰视
    [InlineData(35f)]   // 俯视加侧偏
    public void SphereProjection_Should_BeCorrectInShapeSizeAndCentering(float pitch)
    {
        // 与 GPU 裁决（MatrixSweepProgram）同一组参数，便于交叉验证。
        const float meshRadius = 0.5f;   // MeshFactory 默认半径
        const float scale = 2f;          // 场景里的缩放
        const float distance = 8f;
        const float fov = 50f;
        const int width = 1280, height = 720;

        var camera = new Camera { Width = width, Height = height, Fov = fov, Near = 0.1f, Far = 100f };
        ApplyPose(camera, Vector3D<float>.Zero, distance, pitch);

        Matrix4X4<float> model = Matrix4X4.CreateScale(scale);
        Matrix4X4<float> clip = model * camera.View * camera.Projection;

        // 转成屏幕像素再测量。NDC 的 x/y 各自归一化到 [-1,1]，
        // 所以 NDC 包围盒的宽高比等于屏幕宽高比，不是 1；
        // 只有转成像素后，各向同性球才应当是正圆。
        (float w, float h, float cx, float cy) = PixelBounds(clip, meshRadius, width, height);

        float effectiveRadius = meshRadius * scale;

        // 形状：各向同性球在像素空间必须投影为正圆，w/h = 1。
        // 注意 NDC 包围盒的宽高比是屏幕宽高比（x/y 各自归一化到 [-1,1]），
        // 转成像素后才是 1。这里直接对像素断言。
        Assert.InRange(w / h, 0.97f, 1.03f);

        // 尺寸：球是凸体，其投影轮廓由从眼点引出的切线决定，
        // 轮廓半角为 asin(r/d)（d 为球心距），不是 atan(r/d)。
        // NDC 半高 = tan(asin(r/d)) / tan(fov/2)，再乘半屏高得像素高度。
        float contourHalfAngle = MathF.Asin(effectiveRadius / distance);
        float expectedHalfNdc = MathF.Tan(contourHalfAngle) / MathF.Tan(fov * MathF.PI / 360f);
        float expectedPixelHeight = 2f * expectedHalfNdc * height / 2f;

        Assert.True(MathF.Abs(h - expectedPixelHeight) / expectedPixelHeight < 0.05f,
            $"expected pixel height {expectedPixelHeight:F1}, got {h:F1}");

        // 居中：球心正对相机，应落在画面中心
        Assert.True(MathF.Abs(cx - width / 2f) < width * 0.02f, $"centre X = {cx:F1}");
        Assert.True(MathF.Abs(cy - height / 2f) < height * 0.02f, $"centre Y = {cy:F1}");
    }

    /// <summary>
    /// 视点正对目标时，目标点的裁剪空间 w 必须等于视距，
    /// 这是行向量约定的直接推论：clip.w = -z_view。
    ///
    /// 敏感度：若矩阵链顺序改成 projection * view * model，
    /// w 会退化为 0 或视距的倒数。
    /// </summary>
    [Fact]
    public void ClipW_Should_EqualViewDistance()
    {
        const float distance = 6f;
        var camera = new Camera { Width = 1280, Height = 720, Fov = 45f, Near = 0.1f, Far = 100f };
        camera.SetPosition(0f, 0f, distance);
        camera.LookAt(Vector3D<float>.Zero);

        Matrix4X4<float> clip = camera.View * camera.Projection;

        Vector4D<float> c = TransformPoint(Vector3D<float>.Zero, clip);

        Assert.Equal(distance, c.W, 4);
    }

    /// <summary>
    /// PBR 片元着色器的环境光项必须由 AmbientIntensity 驱动。
    ///
    /// 这是 wiring 护栏而非数值测试：uniform 存在、编译器认得，
    /// 却没有任何机制保证它被真正用上。把这里改回硬编码常量，
    /// C# 与 GLSL 两侧都不会报错，只会让画面暗约十倍。
    /// </summary>
    [Fact]
    public void PbrFragmentShader_Should_DriveAmbientFromUniform()
    {
        string src = ReadEngineFile(Path.Combine("Resources", "Shaders", "PBR.frag"));

        // 环境光项必须由 uniform 驱动。逐行找赋值给 ambient 的那一行，
        // 避免注释里出现同样的字面量造成假通过。
        string ambientLine = FirstCodeLine(src, "ambient =");

        Assert.Contains("AmbientIntensity", ambientLine);
        Assert.DoesNotContain("0.03", ambientLine);

        // 直接光不得再被环境光强度反向压制。
        // 同样只看代码行，避免命中上方解释这段历史的那条注释。
        string colorLine = FirstCodeLine(src, "vec3 color =");

        Assert.DoesNotContain("AmbientIntensity", colorLine);
    }

    /// <summary>
    /// 线框网格必须以 GL_Lines 绘制。
    ///
    /// Mesh.Draw 默认 Triangles，而 BuildGridMesh 按线段对生成顶点。
    /// 画成三角形会把整张栅格变成连接无关顶点的退化细条，
    /// 表现为一块带撕裂锯齿的灰色实心区域。编译器无法发现此错。
    /// </summary>
    [Fact]
    public void WireframeMeshes_Should_BeDrawnAsLines()
    {
        // SimpleDrawingService.cs 未被 CopyToOutputDirectory 复制，只能从源码树读。
        // 着色器走输出目录副本，两者路径规则不同，故分开处理。
        string src = ReadEngineSource(Path.Combine("Services", "SimpleDrawingService.cs"));

        // 这些网格都由 BuildGridMesh / BuildGizmoMeshes 按线段对生成顶点，
        // 必须逐个指定 GL_Lines。Mesh.Draw 的默认值是 Triangles，
        // 漏写任何一处都会让该网格变成连接无关顶点的退化细条。
        string[] mustBeLines =
        {
            "gridMesh.Draw(GLEnum.Lines)",
            "_gizmoMeshTranslateX.Draw(GLEnum.Lines)",
            "_gizmoMeshTranslateY.Draw(GLEnum.Lines)",
            "_gizmoMeshTranslateZ.Draw(GLEnum.Lines)",
            "_gizmoMeshScaleX.Draw(GLEnum.Lines)",
            "_gizmoMeshScaleY.Draw(GLEnum.Lines)",
            "_gizmoMeshScaleZ.Draw(GLEnum.Lines)",
        };

        foreach (string expected in mustBeLines)
            Assert.True(src.Contains(expected, StringComparison.Ordinal),
                $"expected `{expected}` in SimpleDrawingService.cs; " +
                "a bare Draw() there falls back to Triangles");

        // 反向护栏：任何线框网格都不允许以无参形式绘制。
        // 只检查这几类网格的名字，避免误伤真正的三角形网格。
        foreach (string name in new[]
                 {
                     "gridMesh", "_gizmoMeshTranslateX", "_gizmoMeshTranslateY", "_gizmoMeshTranslateZ",
                     "_gizmoMeshScaleX", "_gizmoMeshScaleY", "_gizmoMeshScaleZ",
                 })
        {
            string bare = $"{name}.Draw()";
            Assert.False(src.Contains(bare, StringComparison.Ordinal),
                $"found `{bare}` -- wireframe mesh drawn as Triangles");
        }
    }

    // ---------------- helpers ----------------

    private static Vertex[] BuildPrimitive(string factory)
    {
        switch (factory)
        {
            // Silk.NET.Maths 没有 Vector3D.Transform 重载（只有显式转换和运算符），
            // 所以每个分支用各自的名字，避免 out var 在同一 switch 作用域内重名。
            case "GetCube":
                MeshFactory.GetCube(out Vertex[] v, out _); return v;
            case "GetSphere":
                MeshFactory.GetSphere(out Vertex[] v2, out _); return v2;
            case "GetCylinder":
                MeshFactory.GetCylinder(out Vertex[] v3, out _); return v3;
            case "GetCone":
                MeshFactory.GetCone(out Vertex[] v4, out _); return v4;
            case "GetTorus":
                MeshFactory.GetTorus(out Vertex[] v5, out _); return v5;
            case "GetCanvas":
                MeshFactory.GetCanvas(out Vertex[] v6, out _); return v6;
            default: throw new ArgumentOutOfRangeException(nameof(factory), factory, null);
        }
    }

    /// <summary>
    /// 把半径 radius 的球采样一圈，返回其屏幕像素包围盒。
    /// 采样足以暴露各向异性：若某一轴被压缩，宽高比立刻偏离 1。
    /// </summary>
    private static (float Width, float Height, float CentreX, float CentreY) PixelBounds(
        Matrix4X4<float> clip, float radius, int width, int height)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        const int samples = 256;
        for (int i = 0; i < samples; i++)
        {
            // Fibonacci 球面采样。z 必须线性分布在 [-1,1]，
            // 半径用 sqrt(1-z^2)——写成 sqrt(1-t) 会让采样点
            // 到不了赤道，测出的高度偏小约 24%。
            double t = (i + 0.5) / (double)samples;
            double z = 1.0 - 2.0 * t;
            double r = Math.Sqrt(Math.Max(0.0, 1.0 - z * z));
            double phi = Math.PI * (3.0 - Math.Sqrt(5.0)) * i;
            double x = radius * r * Math.Cos(phi);
            double y = radius * r * Math.Sin(phi);
            z *= radius;

            Vector4D<float> c = TransformPoint(new Vector3D<float>((float)x, (float)y, (float)z), clip);
            Assert.True(c.W > 1e-5f, $"clip.w must be positive in front of the camera, got {c.W}");

            // NDC [-1,1] -> 像素 [0,width] x [0,height]
            float px = (c.X / c.W + 1f) * width / 2f;
            float py = (c.Y / c.W + 1f) * height / 2f;

            minX = MathF.Min(minX, px); maxX = MathF.Max(maxX, px);
            minY = MathF.Min(minY, py); maxY = MathF.Max(maxY, py);
        }

        return (maxX - minX, maxY - minY, (minX + maxX) / 2f, (minY + maxY) / 2f);
    }

    /// <summary>
    /// 把相机放在目标的视线反方向上，距离 distance、俯仰 pitch 度。
    /// 与 CameraBridge 的 SetCameraPose 同一约定：pitch 为负表示低头俯视。
    /// </summary>
    private static void ApplyPose(Camera camera, Vector3D<float> target, float distance, float pitchDegrees)
    {
        float pitch = pitchDegrees * MathF.PI / 180f;
        var viewDir = new Vector3D<float>(
            MathF.Cos(pitch), MathF.Sin(pitch), 0f);

        Vector3D<float> eye = target - viewDir * distance;
        camera.SetPosition(eye.X, eye.Y, eye.Z);
        camera.LookAt(target);
    }

    /// <summary>
    /// 行向量变换：v * M，即 (x, y, z, 1) 依次乘 M 的各行。
    /// Silk.NET.Maths 未提供该重载，直接手写以免依赖不确定的重载语义。
    /// </summary>
    /// <summary>
    /// 返回第一条包含 <paramref name="token"/> 且不是注释的代码行。
    /// 着色器里的历史说明注释会重复提到被删掉的写法，
    /// 直接全文 Contains 会命中注释，产生假通过。
    /// </summary>
    private static string FirstCodeLine(string source, string token)
    {
        foreach (string raw in source.Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith("//", StringComparison.Ordinal)) continue;
            if (line.Contains(token, StringComparison.Ordinal)) return line;
        }

        throw new Xunit.Sdk.XunitException($"no code line containing '{token}' was found");
    }

    // 行向量变换 v * M：乘的是 M 的"列"。
    // 写成 x*M11 + y*M12 + z*M13 + M14 是乘了 M 的转置，
    // 会让正确的矩阵链看起来像被压成竖条——本测试最初就栽在这里。
    private static Vector4D<float> TransformPoint(Vector3D<float> v, Matrix4X4<float> m)
        => new Vector4D<float>(
            v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31 + m.M41,
            v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + m.M42,
            v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + m.M43,
            v.X * m.M14 + v.Y * m.M24 + v.Z * m.M34 + m.M44);

    private static void AssertFinite(
        Vector3D<float> v, Vector3D<float> at, string factory, int i, string name)
    {
        Assert.True(IsFinite(v),
            $"{factory}[{i}] at {at} has non-finite {name}=({v.X}, {v.Y}, {v.Z}); " +
            "normalize() of this would yield NaN and poison the varying");
        Assert.True(Length(v) > 1e-4f,
            $"{factory}[{i}] at {at} has zero-length {name}; normalize() would yield NaN");
    }

    private static bool IsFinite(Vector3D<float> v)
        => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    private static float Length(Vector3D<float> v)
        => MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    /// <summary>
    /// 读取引擎项目下的文件，路径相对 VirtualPathCore 工程目录。
    ///
    /// 着色器与 .cs 源码都由引擎项目设为 CopyToOutputDirectory=PreserveNewest，
    /// 因此测试的输出目录里就有一份与源码同版本的副本。
    /// 直接读这份副本，而不是从仓库根按目录层级向上摸索——
    /// 后者在 bin 目录层级下找不到 csproj，只会抛异常。
    /// </summary>
    private static string ReadEngineFile(string relativePath)
    {
        string full = Path.Combine(AppContext.BaseDirectory,
            relativePath.Replace(Path.DirectorySeparatorChar, '/'));

        Assert.True(File.Exists(full),
            $"engine file not found in test output: {full}. " +
            "It is copied there by VirtualPathCore.csproj (CopyToOutputDirectory=PreserveNewest).");

        return File.ReadAllText(full);
    }

    /// <summary>
    /// 从源码树读取引擎文件，路径相对 VirtualPathCore 工程目录。
    ///
    /// 只有着色器被设为 CopyToOutputDirectory；.cs 源码不会进输出目录，
    /// 所以这类断言必须回到源码树。这里向上找含 VirtualPathCore.sln 的目录，
    /// 而不是硬编码层级数——换机器或换输出配置都不该让测试失效。
    /// </summary>
    private static string ReadEngineSource(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VirtualPathCore.sln")))
            {
                string full = Path.Combine(dir.FullName, "VirtualPathCore",
                    relativePath.Replace('/', Path.DirectorySeparatorChar));

                Assert.True(File.Exists(full),
                    $"engine source not found: {full}");

                return File.ReadAllText(full);
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"could not locate the solution root from {AppContext.BaseDirectory}");
    }
}