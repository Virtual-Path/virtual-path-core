using Silk.NET.OpenGLES;

namespace VirtualPathCore.Graphics;

/// <summary>
/// 渲染宿主额外需要提供的信息：成像表面尺寸与"内容已变脏、需要重绘"的信号。
///
/// 与 <see cref="IGraphicsHost{TContext}"/> 分开的原因：前者只描述 GL 上下文的供给方式
/// （Avalonia 视口 / 无 UI 离屏宿主都可能实现），而表面尺寸与重绘请求是"窗口宿主"的职责。
/// 绘图服务只依赖本接口，因此可以在没有窗口的进程里运行。
/// </summary>
public interface IRenderSurface
{
    /// <summary>成像表面的像素宽度</summary>
    int SurfaceWidth { get; }

    /// <summary>成像表面的像素高度</summary>
    int SurfaceHeight { get; }

    /// <summary>请求重绘。离屏宿主可借此决定何时渲染下一帧。</summary>
    void RequestRender();
}
