using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGLES;

namespace VirtualPathCore.Graphics.OpenGL;

public unsafe class Renderer : OpenGlControlBase, IGraphicsHost<GL>
{
    private readonly Stopwatch _stopwatch = new();

    private GL? context;

    private uint _sceneFbo;
    private uint _sceneColorTex;
    private uint _sceneDepthRbo;
    private int _sceneW, _sceneH;
    private bool _sceneDirty = true;

    public event Action? OnLoad;
    public event Action? OnUnload;
    public event DeltaAction? OnUpdate;
    public event DeltaAction? OnRender;
    public event SizeAction? OnResize;

    public int PixelWidth => (int)(Bounds.Width * (VisualRoot?.RenderScaling ?? 1.0));
    public int PixelHeight => (int)(Bounds.Height * (VisualRoot?.RenderScaling ?? 1.0));

    public void RequestRender()
    {
        _sceneDirty = true;
        RequestNextFrameRendering();
    }

    private void EnsureSceneFbo(int w, int h)
    {
        if (_sceneFbo != 0 && w == _sceneW && h == _sceneH)
            return;

        var gl = context!;
        DestroySceneFbo();

        _sceneW = w;
        _sceneH = h;
        _sceneDirty = true;

        _sceneColorTex = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, _sceneColorTex);
        gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgb8, (uint)w, (uint)h, 0, GLEnum.Rgb, GLEnum.UnsignedByte, null);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
        gl.BindTexture(GLEnum.Texture2D, 0);

        _sceneDepthRbo = gl.GenRenderbuffer();
        gl.BindRenderbuffer(GLEnum.Renderbuffer, _sceneDepthRbo);
        gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent16, (uint)w, (uint)h);
        gl.BindRenderbuffer(GLEnum.Renderbuffer, 0);

        _sceneFbo = gl.GenFramebuffer();
        gl.BindFramebuffer(GLEnum.Framebuffer, _sceneFbo);
        gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.Texture2D, _sceneColorTex, 0);
        gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, _sceneDepthRbo);
        Debug.Assert(gl.CheckFramebufferStatus(GLEnum.Framebuffer) == GLEnum.FramebufferComplete, "Scene FBO incomplete");
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
    }

    private void DestroySceneFbo()
    {
        var gl = context!;
        if (_sceneFbo != 0) { gl.DeleteFramebuffer(_sceneFbo); _sceneFbo = 0; }
        if (_sceneColorTex != 0) { gl.DeleteTexture(_sceneColorTex); _sceneColorTex = 0; }
        if (_sceneDepthRbo != 0) { gl.DeleteRenderbuffer(_sceneDepthRbo); _sceneDepthRbo = 0; }
    }

    private void RenderScene()
    {
        var gl = context!;
        gl.BindFramebuffer(GLEnum.Framebuffer, _sceneFbo);
        gl.Viewport(0, 0, (uint)_sceneW, (uint)_sceneH);
        OnRender?.Invoke(_stopwatch.Elapsed.TotalSeconds);
    }

    private void BlitToFb(uint targetFb)
    {
        var gl = context!;
        gl.BindFramebuffer(GLEnum.ReadFramebuffer, _sceneFbo);
        gl.BindFramebuffer(GLEnum.DrawFramebuffer, targetFb);
        gl.BlitFramebuffer(0, 0, _sceneW, _sceneH, 0, 0, _sceneW, _sceneH,
            (uint)GLEnum.ColorBufferBit, GLEnum.Nearest);
        gl.BindFramebuffer(GLEnum.ReadFramebuffer, 0);
        gl.BindFramebuffer(GLEnum.DrawFramebuffer, 0);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        _stopwatch.Start();

        try
        {
            context ??= GL.GetApi(gl.GetProcAddress);
            OnLoad?.Invoke();
            OnResize?.Invoke(PixelWidth, PixelHeight);
            RequestRender();
        }
        catch
        {
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _stopwatch.Stop();

        OnUnload?.Invoke();
        DestroySceneFbo();
        context?.Dispose();
        context = null;
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (context == null)
            return;

        int w = PixelWidth, h = PixelHeight;

        OnUpdate?.Invoke(_stopwatch.Elapsed.TotalSeconds);

        EnsureSceneFbo(w, h);

        if (_sceneDirty)
        {
            _sceneDirty = false;
            RenderScene();
        }

        BlitToFb((uint)fb);

        RequestNextFrameRendering();
    }

    public event Action<float, float>? OnMouseDown;
    public event Action? OnMouseUp;
    public event Action<float, float>? OnMouseMove;
    public event Action<float>? OnScroll;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        OnMouseDown?.Invoke((float)point.Position.X, (float)point.Position.Y);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        OnMouseUp?.Invoke();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        OnMouseMove?.Invoke((float)point.Position.X, (float)point.Position.Y);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        OnScroll?.Invoke((float)e.Delta.Y);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        if (context != null)
        {
            OnResize?.Invoke((int)e.NewSize.Width, (int)e.NewSize.Height);
            RequestRender();
        }
    }

    public GL GetContext()
    {
        if (context == null)
        {
            throw new InvalidOperationException("The OpenGL context has not been initialized yet.");
        }

        return context;
    }
}
