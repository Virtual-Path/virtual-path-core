using Silk.NET.OpenGLES;
using VirtualPathCore.Graphics;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 离屏渲染目标（FBO + 颜色纹理 + 深度渲染缓冲）并支持读回像素。
///
/// 为什么不复用引擎里的 <c>Graphics/OpenGL/Frame.cs</c>：
/// 该类全项目没有任何调用点（死代码），且它用的是 Silk.NET <b>静态</b> GL API
/// （<c>GL.GenFramebuffer()</c>），从未被验证过。
/// 这里按 <c>Renderer.EnsureSceneFbo</c> 的做法重建一份，走宿主提供的实例 GL。
/// </summary>
internal sealed unsafe class OffscreenTarget : IDisposable
{
    private readonly GL _gl;
    private bool _disposed;

    public int Width { get; }
    public int Height { get; }

    private uint _fbo;
    private uint _colorTex;
    private uint _depthRbo;

    public OffscreenTarget(IGraphicsHost<GL> host, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "offscreen size must be positive");

        _gl = host.GetContext();
        Width = width;
        Height = height;

        _colorTex = _gl.GenTexture();
        _gl.BindTexture(GLEnum.Texture2D, _colorTex);
        _gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgba8, (uint)width, (uint)height, 0,
                       GLEnum.Rgba, GLEnum.UnsignedByte, null);
        _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.BindTexture(GLEnum.Texture2D, 0);

        _depthRbo = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(GLEnum.Renderbuffer, _depthRbo);
        _gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent24, (uint)width, (uint)height);
        _gl.BindRenderbuffer(GLEnum.Renderbuffer, 0);

        _fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(GLEnum.Framebuffer, _fbo);
        _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.Texture2D, _colorTex, 0);
        _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, _depthRbo);

        var status = _gl.CheckFramebufferStatus(GLEnum.Framebuffer);
        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);

        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"offscreen FBO incomplete: {status}");
    }

    /// <summary>绑定为绘制目标并清屏。</summary>
    public void Bind(float clearR = 0.08f, float clearG = 0.09f, float clearB = 0.11f, float clearA = 1f)
    {
        _gl.BindFramebuffer(GLEnum.Framebuffer, _fbo);
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
        _gl.Enable(GLEnum.DepthTest);
        _gl.ClearColor(clearR, clearG, clearB, clearA);
        _gl.Clear((uint)(GLEnum.ColorBufferBit | GLEnum.DepthBufferBit));
    }

    /// <summary>读回颜色附件，返回自上而下、行优先的 RGBA8 像素（已翻转 GL 的原点）。</summary>
    public byte[] ReadPixelsRgba()
    {
        var raw = new byte[Width * Height * 4];
        _gl.BindFramebuffer(GLEnum.ReadFramebuffer, _fbo);
        fixed (byte* dst = raw)
        {
            _gl.ReadPixels(0, 0, (uint)Width, (uint)Height, GLEnum.Rgba, GLEnum.UnsignedByte, dst);
        }
        _gl.BindFramebuffer(GLEnum.ReadFramebuffer, 0);

        // OpenGL 原点在左下，读回后需上下翻转才是图像惯例
        var flipped = new byte[raw.Length];
        int stride = Width * 4;
        for (int y = 0; y < Height; y++)
            Array.Copy(raw, y * stride, flipped, (Height - 1 - y) * stride, stride);

        return flipped;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_fbo != 0) { _gl.DeleteFramebuffer(_fbo); _fbo = 0; }
        if (_colorTex != 0) { _gl.DeleteTexture(_colorTex); _colorTex = 0; }
        if (_depthRbo != 0) { _gl.DeleteRenderbuffer(_depthRbo); _depthRbo = 0; }
    }
}
