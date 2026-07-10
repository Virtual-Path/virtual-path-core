using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using VirtualPathCore.Helpers;
using Silk.NET.Maths;
using Silk.NET.OpenGLES;

namespace VirtualPathCore.Graphics.OpenGL;

public class Renderer : OpenGlControlBase, IGraphicsHost<GL>
{
    public static readonly StyledProperty<int> SamplesProperty = AvaloniaProperty.Register<Renderer, int>("Samples", 4);

    private readonly Stopwatch _stopwatch = new();

    private GL? context;
    private Frame? frame;

    private RenderPipeline? canvasPipeline;
    private Mesh[]? canvasMeshes;

    private bool _renderQueued;

    public event Action? OnLoad;
    public event Action? OnUnload;
    public event DeltaAction? OnUpdate;
    public event DeltaAction? OnRender;
    public event SizeAction? OnResize;

    public int Samples
    {
        get { return GetValue(SamplesProperty); }
        set { SetValue(SamplesProperty, value); }
    }

    public int PixelWidth => (int)(Bounds.Width * (VisualRoot?.RenderScaling ?? 1.0));
    public int PixelHeight => (int)(Bounds.Height * (VisualRoot?.RenderScaling ?? 1.0));

    public void RequestRender()
    {
        if (!_renderQueued)
        {
            _renderQueued = true;
            Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Background);
        }
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        _stopwatch.Start();

        try
        {
            context ??= GL.GetApi(gl.GetProcAddress);
            frame ??= new Frame(this);

            string shaderDir = Path.Combine(AppContext.BaseDirectory, "Resources", "Shaders");

            using Shader canvasVs = new(this, ShaderType.VertexShader, File.ReadAllText(Path.Combine(shaderDir, "Canvas.vert")));
            using Shader canvasFs = new(this, ShaderType.FragmentShader, File.ReadAllText(Path.Combine(shaderDir, "Canvas.frag")));
            canvasPipeline = new RenderPipeline(this, canvasVs, canvasFs);

            MeshFactory.GetCanvas(out Vertex[] cv, out uint[] ci);
            canvasMeshes = [new(this, cv, ci)];
            canvasMeshes[0].SetupAttributes(
                canvasPipeline.GetAttribLocation("In_Position"),
                canvasPipeline.GetAttribLocation("In_Normal"),
                canvasPipeline.GetAttribLocation("In_Tangent"),
                canvasPipeline.GetAttribLocation("In_Bitangent"),
                canvasPipeline.GetAttribLocation("In_Color"),
                canvasPipeline.GetAttribLocation("In_TexCoord"));

            OnLoad?.Invoke();
            OnResize?.Invoke(PixelWidth, PixelHeight);
        }
        catch
        {
            // OpenGL init failed — rendering will not proceed
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _stopwatch.Stop();

        OnUnload?.Invoke();

        if (canvasMeshes != null)
        {
            foreach (Mesh mesh in canvasMeshes)
            {
                mesh.Dispose();
            }
        }
        canvasPipeline?.Dispose();
        frame?.Dispose();
        context?.Dispose();

        frame = null;
        context = null;
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (context == null || frame == null || canvasPipeline == null || canvasMeshes == null)
            return;

        _renderQueued = false;

        int w = PixelWidth, h = PixelHeight;

        // Render scene directly to the default framebuffer (no offscreen pass)
        context.BindFramebuffer(GLEnum.Framebuffer, (uint)fb);
        context.Viewport(0, 0, (uint)w, (uint)h);

        OnUpdate?.Invoke(_stopwatch.Elapsed.TotalSeconds);
        OnRender?.Invoke(_stopwatch.Elapsed.TotalSeconds);

        if (!_renderQueued)
        {
            _renderQueued = true;
            Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Background);
        }
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
