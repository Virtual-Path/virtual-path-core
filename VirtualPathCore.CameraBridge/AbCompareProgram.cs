using Silk.NET.Maths;
using Silk.NET.OpenGLES;
using VirtualPathCore.Graphics;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Services;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// A/B 对照：把引擎场景中的某个物体，与"独立立方体"放在同一个 FBO、
/// 同一台相机下渲染。若独立立方体正常而引擎物体成刀片，
/// 差异就在引擎的顶点/矩阵路径；若两者都正常，则差异在场景搭建。
/// </summary>
internal static class AbCompareProgram
{
    public static int Run(int width, int height, string outPath)
    {
        Console.WriteLine();
        Console.WriteLine("=== A/B compare: engine scene vs standalone cube ===");

        using var host = new HeadlessGraphicsHost(width, height);
        using var target = new OffscreenTarget(host, width, height);

        var sceneService = new SceneService();
        var drawing = new SimpleDrawingService();

        host.OnRender += delta =>
        {
            target.Bind();
            drawing.Render(delta);
        };
        host.OnUpdate += delta => drawing.Update(delta);

        drawing.Load(new object[] { host, sceneService });
        sceneService.Clear();

        var cube = sceneService.AddCube("OnlyCube");
        cube.PositionX = 0f;
        cube.PositionY = 0f;
        cube.PositionZ = 0f;
        cube.ScaleX = 2f;
        cube.ScaleY = 2f;
        cube.ScaleZ = 2f;
        cube.SceneObject!.Material ??= new Material();
        cube.SceneObject.Material.Albedo = new Vector4D<float>(0.9f, 0.2f, 0.2f, 1f);

        // Aim slightly above the floor plane. With the target at y=0 the camera
        // ends up exactly in the y=0 ground-grid plane; the grid is then viewed
        // edge-on, degenerates, and covers the cube with garbage depth.
        var look = new Vector3D<float>(0f, 0.4f, 0f);

        // 正面平视，FOV 适中
        drawing.SetCameraPose(look, 6f, 0f, 180f);
        drawing.SetCameraFov(45f);

        // 变体扫描：逐个改变姿态，找出从"正常"变到"异常"的临界条件
        var poses = new (string Tag, float Pitch, float Yaw, float Dist)[]
        {
            ("front_0deg",      0f,  180f, 6f),
            ("front_+30yaw",    0f,  150f, 6f),
            ("front_-30yaw",    0f,  210f, 6f),
            ("pitch_-20",     -20f,  180f, 6f),
            ("pitch_+20",      20f,  180f, 6f),
            ("pitch_-45",     -45f,  180f, 6f),
        };

        // Count reddish pixels by channel dominance, not by absolute brightness.
        // The face pointing at the camera faces away from the key light, so it
        // only receives ambient (AmbientIntensity 0.3 * albedo 0.9 = 0.27 ->
        // r ~= 69) and an absolute "r > 70" threshold sits right on the boundary
        // and reports a solid cube as empty.
        static bool IsReddish(int r, int g, int b) => r > 40 && r > g + 25 && r > b + 25;

        foreach (var (tag, pitch, yaw, dist) in poses)
        {
            drawing.SetCameraPose(look, dist, pitch, yaw);
            host.RenderFrameAlways(1.0 / 30.0);

            byte[] p = target.ReadPixelsRgba();
            int cnt = 0;
            for (int i = 0; i < p.Length; i += 4)
            {
                if (IsReddish(p[i], p[i + 1], p[i + 2])) cnt++;
            }
            Console.WriteLine($"  [{tag,-14}] red = {cnt / (double)(p.Length / 4),7:P2}");
        }

        // 复位到正面平视，供后续正式渲染
        drawing.SetCameraPose(look, 6f, 0f, 180f);
        host.RenderFrameAlways(1.0 / 30.0);

        // 诊断：打印视口与相机宽高，确认 Render() 拿到的表面尺寸正确
        Console.WriteLine($"  surface      : {host.SurfaceWidth}x{host.SurfaceHeight}");
        Console.WriteLine($"  target FBO   : {target.Width}x{target.Height}");

        GL gl = host.GetContext();

        for (int i = 0; i < 3; i++)
        {
            host.RenderFrameAlways(1.0 / 30.0);

            // 读取引擎内部的相机宽高，验证视口链路
            if (i == 2)
            {
                var vp = new int[4];
                gl.GetInteger(GLEnum.Viewport, vp);
                Console.WriteLine($"  gl viewport  : [{vp[0]}, {vp[1]}, {vp[2]}, {vp[3]}]");
                Console.WriteLine($"  gl err       : {gl.GetError()}");
            }
        }

        byte[] pixels = target.ReadPixelsRgba();
        Program.WritePng(pixels, width, height, outPath);

        // 统计：只关心"红色物体"（albedo 0.9,0.2,0.2）占据的像素
        int red = 0, any = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            int r = pixels[i], g = pixels[i + 1], b = pixels[i + 2];
            if (r + g + b > 60) any++;
            if (IsReddish(r, g, b)) red++;
        }
        int total = pixels.Length / 4;

        Console.WriteLine($"  camera pos : {drawing.CameraPosition}");
        Console.WriteLine($"  lit pixels : {any / (double)total:P2}");
        Console.WriteLine($"  red pixels : {red / (double)total:P2}");
        Console.WriteLine($"  wrote {outPath}");

        // A 2-unit cube at distance 6 with a 45 deg vertical FOV: its near face is
        // 5 away (camera at x=6, cube half-width 1), so the near face covers
        // (1/5) / tan(22.5 deg) = 48.3% of the half-height -> 348px tall,
        // i.e. about 13% of a 1280x720 frame. Measured 12.94%.
        double expected = 0.13;
        bool ok = red / (double)total > expected * 0.7 && red / (double)total < expected * 1.4;
        Console.WriteLine($"  expected   : ~{expected:P1} of frame (front face, {width}x{height})");
        Console.WriteLine();
        Console.WriteLine(ok
            ? "RESULT: PASS - engine cube renders as a solid box."
            : "RESULT: FAIL - engine cube is not rendering as a solid box.");
        return ok ? 0 : 7;
    }
}
