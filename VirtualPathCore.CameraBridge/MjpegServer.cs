using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 把离屏渲染结果以 MJPEG（<c>multipart/x-mixed-replace</c>）推给任意 HTTP 客户端。
///
/// 选 MJPEG 而非 RTSP 的原因：
/// <list type="bullet">
/// <item>OpenCV 的 <c>VideoCapture</c> 直接吃 <c>http://…</c>，因此
/// <c>virtual-path-vision</c> 的 <c>Network Stream</c> 源<b>零改动</b>即可消费。</item>
/// <item>无需额外原生依赖（不需要 live555 / ffmpeg）。</item>
/// </list>
///
/// 为什么不用 <see cref="HttpListener"/>：它在 Windows 上走 http.sys，
/// 绑定具体 IP 前缀需要 URL ACL 管理员权限（HttpListenerException 6 / AccessDenied），
/// 会让"本机跑一下就能看效果"变成要先提权。这里改用裸 <see cref="TcpListener"/>，
/// 自己解析最小 HTTP 请求，零权限要求、行为完全可控。
///
/// 边界：单客户端、不支持鉴权/TLS、不处理 Range 等高级特性。
/// 定位是本机/局域网内的开发与演示通道，不是生产视频服务。
/// </summary>
internal sealed class MjpegServer : IDisposable
{
    private const string Boundary = "vpframe";

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _writeLock = new();

    private TcpClient? _client;
    private NetworkStream? _stream;
    private Task? _acceptLoop;

    /// <summary>是否已有客户端连上。用于避免无客户端时空转占 GPU。</summary>
    public bool HasClient { get; private set; }

    public int Port { get; }

    /// <param name="port">监听端口。</param>
    public MjpegServer(int port)
    {
        Port = port;
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    /// <summary>开始监听。必须在后台线程处理连接，否则会阻塞。</summary>
    public void Start() => _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));

    /// <summary>
    /// 推送一帧 JPEG。
    /// </summary>
    /// <remarks>
    /// 线程模型：可在渲染线程直接调用；内部用锁串行化写入，
    /// 避免与断开处理并发操作同一条流。
    /// </remarks>
    public bool PushFrame(byte[] jpeg)
    {
        NetworkStream? stream = _stream;
        if (stream == null || !stream.CanWrite)
            return false;

        try
        {
            byte[] header = Encoding.ASCII.GetBytes(
                $"--{Boundary}\r\nContent-Type: image/jpeg\r\nContent-Length: {jpeg.Length}\r\n\r\n");
            byte[] trailer = Encoding.ASCII.GetBytes("\r\n");

            lock (_writeLock)
            {
                stream.Write(header, 0, header.Length);
                stream.Write(jpeg, 0, jpeg.Length);
                stream.Write(trailer, 0, trailer.Length);
                stream.Flush();
            }
            return true;
        }
        catch (Exception)
        {
            // 客户端断开是常态（切换画面 / 关窗口），由 AcceptLoop 清理
            return false;
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        _listener.Start();
        Console.WriteLine($"[serve] listening on 127.0.0.1:{Port}");

        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                continue;
            }

            _ = Task.Run(() => ServeClientAsync(client, ct), ct);
        }
    }

    private async Task ServeClientAsync(TcpClient client, CancellationToken ct)
    {
        // 提到 try 外：finally 需要判断"这个连接是否真的成为了 MJPEG 流"，
        // 才能决定要不要清理共享的 _stream / HasClient。
        NetworkStream? stream = null;
        bool becameStream = false;

        try
        {
            client.NoDelay = true;    // MJPEG 帧小且要求低延迟，关闭 Nagle
            stream = client.GetStream();

            // ---- 最小 HTTP 请求解析：只关心第一行与路径 ----
            string? requestLine = await ReadRequestLineAsync(stream, ct).ConfigureAwait(false);
            if (requestLine == null)
                return;

            string[] parts = requestLine.Split(' ');
            string path = parts.Length >= 2 ? parts[1] : "/";

            if (!string.Equals(path, "/cam1", StringComparison.OrdinalIgnoreCase))
            {
                byte[] body = Encoding.UTF8.GetBytes(
                    "Virtual camera stream endpoint: /cam1\n" +
                    "Example (OpenCV): VideoCapture(\"http://127.0.0.1:" + Port + "/cam1\")\n");
                byte[] head = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain; charset=utf-8\r\n" +
                    $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(head, ct).ConfigureAwait(false);
                await stream.WriteAsync(body, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);

                // 打印实际请求的路径：客户端配错路径时会在这里直接暴露，
                // 否则只看到一串无信息的 "client disconnected"。
                Console.WriteLine($"[serve] rejected {path} (only /cam1 is served)");
                return;
            }

            // ---- 切换到 MJPEG 响应 ----
            byte[] headers = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: multipart/x-mixed-replace; boundary=" + Boundary + "\r\n" +
                "Cache-Control: no-cache, no-store, must-revalidate\r\n" +
                "Pragma: no-cache\r\n" +
                "Connection: close\r\n" +
                "\r\n");
            await stream.WriteAsync(headers, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);

            _stream = stream;
            HasClient = true;
            becameStream = true;
            Console.WriteLine($"[serve] client connected: {client.Client.RemoteEndPoint}");

            // 保持连接直到取消；写失败（对端关闭）由 PushFrame 的返回值体现
            while (!ct.IsCancellationRequested && stream.CanWrite)
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
        catch (Exception)
        {
            // 客户端异常断开，忽略
        }
        finally
        {
            // 只有曾经成为 MJPEG 流的连接才需要清理状态。
            //
            // 若无条件执行 _stream = null，会踩到竞态：新连接已经把 _stream 指向
            // 自己的流之后，旧连接的 finally 才跑完，于是把新连接的流清成 null，
            // 表现为"画面莫名其妙停住"，而日志里毫无线索。
            if (becameStream)
            {
                lock (_writeLock)
                {
                    if (ReferenceEquals(_stream, stream))
                    {
                        _stream = null;
                        HasClient = false;
                    }
                }
                Console.WriteLine("[serve] client disconnected");
            }

            lock (_writeLock)
            {
                try { client.Close(); } catch { /* 已关闭 */ }
            }
        }
    }

    /// <summary>
    /// 读第一行请求行（到 CRLF 为止），并把已读字节留在缓冲里不再关心。
    /// </summary>
    private static async Task<string?> ReadRequestLineAsync(NetworkStream stream, CancellationToken ct)
    {
        var buf = new byte[1];
        var line = new List<byte>(64);

        while (line.Count < 8192)
        {
            int n = await stream.ReadAsync(buf.AsMemory(0, 1), ct).ConfigureAwait(false);
            if (n <= 0)
                return null;

            if (buf[0] == (byte)'\n')
            {
                string s = Encoding.ASCII.GetString(line.ToArray()).TrimEnd('\r');
                return s.Length > 0 ? s : null;
            }

            line.Add(buf[0]);
        }

        return null;
    }

    public void Dispose()
    {
        _cts.Cancel();

        lock (_writeLock)
        {
            try { _stream?.Dispose(); } catch { /* 已关闭 */ }
            try { _client?.Close(); } catch { /* 已关闭 */ }
            _stream = null;
            _client = null;
        }

        try { _listener.Stop(); } catch { /* 已停止 */ }

        try { _acceptLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* 忽略 */ }
        _cts.Dispose();
    }
}
