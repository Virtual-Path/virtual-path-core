using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using VirtualPathCore.Services;
using GdPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 第三段：把引擎场景渲染成 MJPEG 视频流，供 <c>virtual-path-vision</c> 的
/// <c>Network Stream</c> 源直接消费（vision 侧零代码改动）。
///
/// 启动后保持运行，Ctrl+C 退出：
/// <code>
/// VirtualPathCore.CameraBridge --serve --port 8080 --fps 30
/// # 然后在 vision app 里：Source = Network Stream, URL = http://127.0.0.1:8080/cam1
/// </code>
/// </summary>
internal static class ServeProgram
{
    public static int Run(int width, int height, int port, double fps)
    {
        Console.WriteLine();
        Console.WriteLine("=== virtual camera server ===");
        Console.WriteLine($"  resolution : {width}x{height}");
        Console.WriteLine($"  fps        : {fps:0.##}");
        Console.WriteLine($"  stream url : http://127.0.0.1:{port}/cam1");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;   // 阻止进程被立即杀掉，走优雅退出
            Console.WriteLine("\n[serve] shutting down...");
            cts.Cancel();
        };

        using var host = new HeadlessGraphicsHost(width, height);
        using var target = new OffscreenTarget(host, width, height);
        using var server = new MjpegServer(port);
        using var jpeg = new JpegEncoder(width, height, quality: 80);

        var sceneService = new SceneService();
        var drawing = new SimpleDrawingService();

        // 宿主负责绑定绘制目标：绘图服务假定 FBO 已就绪
        // （Avalonia 的 Renderer.RenderScene() 也是这么做的）
        host.OnRender += delta =>
        {
            target.Bind();
            drawing.Render(delta);
        };
        host.OnUpdate += delta => drawing.Update(delta);

        drawing.Load(new object[] { host, sceneService });
        DemoScene.Build(sceneService);
        DemoScene.FrameCamera(drawing);

        Console.WriteLine($"  camera pos : {drawing.CameraPosition}");

        server.Start();

        // 用 Stopwatch 控制节拍：真实节拍比"每轮固定开销"更接近相机行为
        var clock = System.Diagnostics.Stopwatch.StartNew();
        double frameInterval = 1.0 / Math.Max(1.0, fps);
        long nextFrameDue = 0;
        int frameIndex = 0;
        int pushed = 0;

        Console.WriteLine("[serve] waiting for client (Ctrl+C to stop)");

        while (!cts.IsCancellationRequested)
        {
            // 到点才渲染：无客户端时不必空转占 GPU
            if (clock.Elapsed.TotalMilliseconds < nextFrameDue)
            {
                cts.Token.WaitHandle.WaitOne(2);
                continue;
            }

            double delta = frameInterval;
            host.RenderFrameAlways(delta);
            frameIndex++;

            // 让场景动起来（传送带上的工件沿 X 移动）
            DemoScene.Animate(sceneService, delta);

            if (server.HasClient)
            {
                byte[] rgba = target.ReadPixelsRgba();
                byte[] jpegBytes = jpeg.Encode(rgba);
                if (server.PushFrame(jpegBytes))
                {
                    pushed++;
                    if (pushed == 1 || pushed % 150 == 0)
                        Console.WriteLine($"[serve] streaming... frame {frameIndex}, pushed {pushed}");
                }
            }

            // 下一帧到期时间（以绝对时刻为准，避免累积漂移）
            nextFrameDue = (long)(clock.Elapsed.TotalMilliseconds + frameInterval * 1000.0);
        }

        Console.WriteLine($"[serve] stopped after {frameIndex} frames, {pushed} pushed");
        return 0;
    }
}

/// <summary>
/// 把 RGBA8 像素编码为 JPEG。
///
/// 复用 <see cref="Bitmap"/> 而非引第三方图像库：System.Drawing 在本工程是
/// Windows-only 依赖，而本工具定位就是本机开发用的虚拟相机。
/// </summary>
internal sealed class JpegEncoder : IDisposable
{
    private readonly int _width;
    private readonly int _height;
    private readonly ImageCodecInfo _jpegCodec;
    private readonly EncoderParameters _params;
    private readonly Bitmap _bitmap;
    private readonly byte[] _bgra;

    public JpegEncoder(int width, int height, long quality)
    {
        _width = width;
        _height = height;
        _bgra = new byte[width * height * 4];
        _bitmap = new Bitmap(width, height, GdPixelFormat.Format32bppArgb);

        _jpegCodec = Array.Find(ImageCodecInfo.GetImageEncoders(),
                                c => c.FormatID == ImageFormat.Jpeg.Guid)
            ?? throw new InvalidOperationException("JPEG encoder not available");

        _params = new EncoderParameters(1);
        _params.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
    }

    /// <summary>把自上而下的 RGBA8 像素编码为 JPEG 字节。</summary>
    public byte[] Encode(byte[] rgba)
    {
        // 32bpp 内存序是 BGBA，交换 R/B
        for (int i = 0; i < rgba.Length; i += 4)
        {
            _bgra[i + 0] = rgba[i + 2];
            _bgra[i + 1] = rgba[i + 1];
            _bgra[i + 2] = rgba[i + 0];
            _bgra[i + 3] = 255;
        }

        BitmapData locked = _bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, _width, _height),
            ImageLockMode.WriteOnly, GdPixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(_bgra, 0, locked.Scan0, _bgra.Length);
        }
        finally
        {
            _bitmap.UnlockBits(locked);
        }

        using var ms = new MemoryStream();
        _bitmap.Save(ms, _jpegCodec, _params);
        return ms.ToArray();
    }

    public void Dispose()
    {
        _params.Dispose();
        _bitmap.Dispose();
    }
}
