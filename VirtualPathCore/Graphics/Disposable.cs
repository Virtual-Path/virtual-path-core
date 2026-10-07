using System;

namespace VirtualPathCore.Graphics;

/// <summary>
/// 提供一种机制来释放非托管资源，并确保资源在不再需要时被正确释放
/// 该类实现了 <see cref="IDisposable"/> 接口，并提供了一个抽象方法 <see cref="Destroy(bool)"/> 供派生类实现
/// </summary>
public abstract class Disposable : IDisposable
{
    private bool disposedValue;

    /// <summary>
    /// 析构函数，用于在对象被垃圾回收时释放非托管资源
    /// </summary>
    /// <remarks>
    /// 终结器线程上的异常无法被调用方捕获，会直接终止进程。
    /// 而 GL 资源（如 <c>Mesh</c>）被回收时上下文可能已经销毁，
    /// 此时 <c>GraphicsHost.GetContext()</c> 会抛 InvalidOperationException。
    /// 因此这里吞掉异常：GL 资源本身随上下文一起失效，无需再释放，
    /// 而崩溃的后果远比漏释放严重（渲染中断、界面无响应）。
    /// </remarks>
    ~Disposable()
    {
        try
        {
            Dispose(disposing: false);
        }
        catch
        {
            // 终结器线程上不能抛出异常；GL 资源已随上下文失效，静默忽略
        }
    }

    /// <summary>
    /// 派生类应重写此方法以释放非托管资源
    /// </summary>
    /// <param name="disposing">指示是否由 <see cref="Dispose()"/> 方法调用</param>
    protected abstract void Destroy(bool disposing = false);

    /// <summary>
    /// 释放托管和非托管资源
    /// </summary>
    /// <param name="disposing">指示是否由 <see cref="Dispose()"/> 方法调用</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            Destroy(disposing);

            disposedValue = true;
        }
    }

    /// <summary>
    /// 释放由 <see cref="Disposable"/> 类使用的所有资源
    /// 此方法应被显式调用，以确保资源在不再需要时被正确释放
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
