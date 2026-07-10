using System;
using System.Diagnostics;
using Silk.NET.OpenGLES;

namespace VirtualPathCore.Graphics.OpenGL;

/// <summary>
/// 表示一个帧缓冲区对象，用于渲染到纹理或离屏渲染
/// </summary>
public class Frame : GraphicsResource
{
    /// <summary>
    /// 初始化 <see cref="Frame"/> 类的新实例
    /// </summary>
    /// <param name="graphicsHost">图形宿主对象，提供 OpenGL 上下文</param>
    public Frame(IGraphicsHost<GL> graphicsHost) : base(graphicsHost)
    {
        Handle = GL.GenFramebuffer();

        Framebuffer = GL.GenFramebuffer();
        ColorBuffer = GL.GenRenderbuffer();
        DepthStencilBuffer = GL.GenRenderbuffer();

        Texture = new Texture(GraphicsHost);
    }

    /// <summary>
    /// 获取帧缓冲区的宽度
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// 获取帧缓冲区的高度
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// 获取帧缓冲区的多重采样数
    /// </summary>
    public int Samples { get; private set; }

    /// <summary>
    /// 获取与帧缓冲区关联的纹理对象
    /// </summary>
    public Texture Texture { get; }

    /// <summary>
    /// 获取帧缓冲区的 OpenGL 句柄
    /// </summary>
    public uint Framebuffer { get; }

    /// <summary>
    /// 获取颜色缓冲区的 OpenGL 句柄
    /// </summary>
    public uint ColorBuffer { get; }

    /// <summary>
    /// 获取深度和模板缓冲区的 OpenGL 句柄
    /// </summary>
    public uint DepthStencilBuffer { get; }

    /// <summary>
    /// 释放帧缓冲区及其相关资源
    /// </summary>
    /// <param name="disposing">指示是否显式释放资源</param>
    protected override void Destroy(bool disposing = false)
    {
        if (Handle != 0)
        {
            GL.DeleteFramebuffer(Handle);
            Handle = 0;
        }

        if (Framebuffer != 0)
        {
            GL.DeleteFramebuffer(Framebuffer);
        }

        GL.DeleteRenderbuffer(ColorBuffer);
        GL.DeleteRenderbuffer(DepthStencilBuffer);

        Texture.Dispose();
    }

    /// <summary>
    /// 更新帧缓冲区的尺寸和多重采样数
    /// </summary>
    /// <param name="width">帧缓冲区的新宽度</param>
    /// <param name="height">帧缓冲区的新高度</param>
    /// <param name="samples">帧缓冲区的新多重采样数</param>
    /// <exception cref="Exception">当采样数小于 1 时抛出异常</exception>
    public void Update(int width, int height, int samples = 1)
    {
        if (samples < 1)
        {
            throw new Exception("The number of samples must be greater than or equal to 1.");
        }

        if (Width == width && Height == height && Samples == samples)
        {
            return;
        }

        Width = width;
        Height = height;
        Samples = samples;

        if (Handle == 0)
        {
            return;
        }

        Texture.Clear((uint)Width, (uint)Height);

        GL.BindFramebuffer(GLEnum.Framebuffer, Handle);
        GL.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.Texture2D, Texture.Handle, 0);
        GL.BindFramebuffer(GLEnum.Framebuffer, 0);

        // 多重采样缓冲区
        {
            GL.BindRenderbuffer(GLEnum.Renderbuffer, ColorBuffer);
            GL.RenderbufferStorageMultisample(GLEnum.Renderbuffer, (uint)Samples, GLEnum.Rgb8, (uint)Width, (uint)Height);
            GL.BindRenderbuffer(GLEnum.Renderbuffer, 0);

            GL.BindRenderbuffer(GLEnum.Renderbuffer, DepthStencilBuffer);
            GL.RenderbufferStorageMultisample(GLEnum.Renderbuffer, (uint)Samples, GLEnum.Depth32fStencil8, (uint)Width, (uint)Height);
            GL.BindRenderbuffer(GLEnum.Renderbuffer, 0);

            GL.BindFramebuffer(GLEnum.Framebuffer, Framebuffer);
            GL.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.Renderbuffer, ColorBuffer);
            GL.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthStencilAttachment, GLEnum.Renderbuffer, DepthStencilBuffer);

            Debug.Assert(GL.CheckFramebufferStatus(GLEnum.Framebuffer) == GLEnum.FramebufferComplete, "Framebuffer is incomplete");

            GL.BindFramebuffer(GLEnum.Framebuffer, 0);
        }
    }

    /// <summary>
    /// 绑定帧缓冲区以进行渲染
    /// </summary>
    public void Bind()
    {
        GL.BindFramebuffer(GLEnum.Framebuffer, Framebuffer);
        GL.Viewport(0, 0, (uint)Width, (uint)Height);
    }

    /// <summary>
    /// 解绑帧缓冲区，并将内容复制到默认帧缓冲区
    /// </summary>
    public void Unbind()
    {
        GL.BindFramebuffer(GLEnum.Framebuffer, 0);

        GL.BindFramebuffer(GLEnum.ReadFramebuffer, Framebuffer);
        GL.BindFramebuffer(GLEnum.DrawFramebuffer, Handle);
        GL.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, (uint)GLEnum.ColorBufferBit, GLEnum.Nearest);
        GL.BindFramebuffer(GLEnum.DrawFramebuffer, 0);
        GL.BindFramebuffer(GLEnum.ReadFramebuffer, 0);
    }
}
