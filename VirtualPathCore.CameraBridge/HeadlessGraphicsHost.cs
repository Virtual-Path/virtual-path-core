using Silk.NET.OpenGLES;
using VirtualPathCore.Graphics;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 无窗口（headless）的渲染宿主：同时实现 <see cref="IGraphicsHost{TContext}"/>（供给 GL 上下文）
/// 与 <see cref="IRenderSurface"/>（提供表面尺寸与重绘信号）。
///
/// 有了这两个接口，引擎本体的 <c>SimpleDrawingService</c> —— 连同它依赖的
/// SceneService / PBR 着色器 / 网格构建 —— 可以原封不动地跑在没有 UI 的进程里。
///
/// GL 函数地址由 <see cref="GlfwNative"/> 提供（含 opengl32 回退）；
/// 不用 Silk.NET 的 DefaultNativeContext：它把 GL 符号当窗口库导出符号查，必然失败。
///
/// 线程约定：GL 上下文绑定在创建它的线程上，所有成员必须在同一线程调用。
/// </summary>
internal sealed class HeadlessGraphicsHost : IGraphicsHost<GL>, IRenderSurface, IDisposable
{
    private GL? _gl;
    private bool _disposed;

    /// <summary>是否有新的一帧需要渲染。Avalonia 宿主用"脏标记 + RequestNextFrameRendering"，这里同构。</summary>
    private volatile bool _dirty = true;

    public event Action? OnLoad;
    public event Action? OnUnload;
    public event DeltaAction? OnUpdate;
    public event DeltaAction? OnRender;
    public event SizeAction? OnResize;

    public int SurfaceWidth { get; private set; }
    public int SurfaceHeight { get; private set; }

    /// <param name="width">成像宽度（像素）。</param>
    /// <param name="height">成像高度（像素）。</param>
    public HeadlessGraphicsHost(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "surface size must be positive");

        SurfaceWidth = width;
        SurfaceHeight = height;

        GlfwNative.CreateHiddenContext(width, height);
        _gl = GL.GetApi(GlfwNative.GetProcAddress);

        OnLoad?.Invoke();
        OnResize?.Invoke(width, height);
    }

    public GL GetContext()
        => _gl ?? throw new InvalidOperationException("GL context is not initialized yet.");

    public void RequestRender() => _dirty = true;

    /// <summary>推进一帧。先 Update 再 Render，与 <c>Renderer.OnOpenGlRender</c> 的顺序一致。</summary>
    public void RenderFrame(double deltaSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        OnUpdate?.Invoke(deltaSeconds);

        if (_dirty)
        {
            _dirty = false;
            OnRender?.Invoke(deltaSeconds);
        }

        GlfwNative.PollEvents();
    }

    /// <summary>连续模式下每帧都渲染，忽略脏标记（用于实时视频流）。</summary>
    public void RenderFrameAlways(double deltaSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        OnUpdate?.Invoke(deltaSeconds);
        OnRender?.Invoke(deltaSeconds);
        GlfwNative.PollEvents();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _gl?.Dispose();
        _gl = null;
        GlfwNative.Destroy();

        OnUnload?.Invoke();
    }
}
