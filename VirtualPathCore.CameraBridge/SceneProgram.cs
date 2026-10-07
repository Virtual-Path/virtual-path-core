using VirtualPathCore.Services;
using VirtualPathCore.ViewModels;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 第二段验证：直接复用引擎本体的绘图服务（<see cref="SimpleDrawingService"/> +
/// <c>SceneService</c> + PBR 着色器 + 网格构建），在无 UI 进程里渲染一个真实场景。
///
/// 关键点：本文件不创建任何 Mesh、不加载任何着色器、不调用任何 GL 绘制函数——
/// 这些都由被复用的引擎代码完成。若此程序能出图，就证明"引擎场景系统"整体可离屏运行。
/// </summary>
internal static class SceneProgram
{
    public static int Run(int width, int height, string outPath, string sceneName)
    {
        Console.WriteLine();
        Console.WriteLine("=== [4] engine scene rendering (headless) ===");

        try
        {
            using var host = new HeadlessGraphicsHost(width, height);
            using var target = new OffscreenTarget(host, width, height);

            var sceneService = new SceneService();
            var drawing = new SimpleDrawingService();

            // 宿主订阅渲染回调。关键：绘制服务假定"宿主已绑定绘制目标"
            // （Avalonia 的 Renderer.RenderScene() 就是在触发 OnRender 前绑定 _sceneFbo），
            // 因此离屏宿主必须自己先绑定 FBO，否则绘制会落到默认帧缓冲、读回全黑。
            host.OnRender += delta =>
            {
                target.Bind();
                drawing.Render(delta);
            };
            host.OnUpdate += delta => drawing.Update(delta);

            // 与 MainView.axaml.cs 相同的初始化顺序：先宿主事件，再 Load
            drawing.Load(new object[] { host, sceneService });

            // 摆一个可辨识的工业场景 + 设定机位
            DemoScene.Build(sceneService);
            DemoScene.FrameCamera(drawing);

            // 打印每个物体的世界坐标与缩放，便于核对是否落在相机视锥内
            foreach (var vm in sceneService.SceneObjects)
            {
                var so = vm.SceneObject;
                if (so == null) continue;
                var p = so.Transform.Position;
                var s = so.Transform.Scale;
                Console.WriteLine($"    {vm.Name,-18} pos=({p.X,6:F2},{p.Y,6:F2},{p.Z,6:F2}) scale=({s.X,5:F2},{s.Y,5:F2},{s.Z,5:F2})");
            }
            Console.WriteLine($"    camera pos = {drawing.CameraPosition}");

            // 渲染若干帧：动画/轨道相机会随 delta 推进
            for (int i = 0; i < 8; i++)
                host.RenderFrameAlways(1.0 / 30.0);

            byte[] pixels = target.ReadPixelsRgba();
            Program.WritePng(pixels, width, height, outPath);

            var (bright, saturated, dominant) = AnalyzeFrame(pixels);
            Console.WriteLine($"  objects in scene  : {sceneService.Scene.Objects.Count}");
            Console.WriteLine($"  dominant colour   : {dominant}");
            Console.WriteLine($"  lit pixels        : {bright:P1} (luma > 60)");
            Console.WriteLine($"  saturated pixels  : {saturated:P1} (max-min > 40)");
            Console.WriteLine($"  wrote {outPath}");

            // 受光几何体应占据可观面积；仅有网格的话亮部占比会很低
            if (bright < 0.05)
            {
                Console.WriteLine("RESULT: FAIL - scene drew no lit geometry (grid/background only).");
                return 4;
            }

            Console.WriteLine();
            Console.WriteLine("RESULT: PASS - engine scene system renders headlessly.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [FAIL] {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"         inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            return 5;
        }
    }

    /// <summary>
    /// 画面内容判定。
    ///
    /// 注意：不能只与"单一背景色"比较——引擎的 ClearViewport 用 (0.08,0.08,0.10)，
    /// 而网格绘制会铺满大面积低亮度像素，导致任何固定阈值都误判为 100%。
    /// 这里改用两种与背景无关的判据：是否存在亮度明显高于背景的像素（几何体受光面），
    /// 以及是否存在色彩饱和度足够高的像素（有颜色的工件）。
    /// </summary>
    private static (double bright, double saturated, string mostCommon) AnalyzeFrame(byte[] pixels)
    {
        // 背景亮度约 0.08*255 ≈ 20
        int bright = 0, saturated = 0;
        int total = pixels.Length / 4;
        var buckets = new Dictionary<int, int>();

        for (int i = 0; i < pixels.Length; i += 4)
        {
            int r = pixels[i], g = pixels[i + 1], b = pixels[i + 2];
            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));
            int luma = (int)(0.299 * r + 0.587 * g + 0.114 * b);

            if (luma > 60) bright++;
            if (max - min > 40) saturated++;

            // 量化到 16 级灰度做主色统计
            int key = (r >> 4) << 8 | (g >> 4) << 4 | (b >> 4);
            buckets.TryGetValue(key, out int c);
            buckets[key] = c + 1;
        }

        var top = buckets.OrderByDescending(kv => kv.Value).First();
        return (bright / (double)total, saturated / (double)total, $"#{top.Key:X3}");
    }

}
