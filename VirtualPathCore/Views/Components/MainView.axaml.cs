using System;
using Avalonia.Controls;
using VirtualPathCore.Contracts.Services;
using VirtualPathCore.Graphics.OpenGL;
using VirtualPathCore.Services;
using VirtualPathCore.ViewModels;

namespace VirtualPathCore.Views
{
public partial class MainView : UserControl
{
    private readonly IDrawingService _drawingService1;
    private SceneService? _sceneService;
    private bool _rendererReady;

    public bool IsRendererReady { get; private set; }
    public Action? RendererReady;
    public SimpleDrawingService? DrawingService => _drawingService1 as SimpleDrawingService;

    public MainView()
    {
        InitializeComponent();

        _drawingService1 = new SimpleDrawingService();

        glRenderer1.OnLoad += OnRendererLoaded;
        glRenderer1.OnUpdate += d => _drawingService1.Update(d);
        glRenderer1.OnRender += d => _drawingService1.Render(d);

        glRenderer1.OnMouseDown += (x, y) => { if (_drawingService1 is SimpleDrawingService s) s.OnMouseDown(x, y); };
        glRenderer1.OnMouseUp += () => { if (_drawingService1 is SimpleDrawingService s) s.OnMouseUp(); };
        glRenderer1.OnMouseMove += (x, y) => { if (_drawingService1 is SimpleDrawingService s) s.OnMouseMove(x, y); };
        glRenderer1.OnScroll += (d) => { if (_drawingService1 is SimpleDrawingService s) s.OnScroll(d); };
        glRenderer1.OnRightMouseDown += (x, y) => { if (_drawingService1 is SimpleDrawingService s) s.OnRightMouseDown(x, y); };
        glRenderer1.OnRightMouseUp += () => { if (_drawingService1 is SimpleDrawingService s) s.OnRightMouseUp(); };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainViewModel vm)
        {
            _sceneService = vm.SceneService;
            if (_rendererReady)
                InitializeDrawingService();
        }
    }

    private void OnRendererLoaded()
    {
        _rendererReady = true;
        if (_sceneService != null)
            InitializeDrawingService();
    }

    private void InitializeDrawingService()
    {
        _drawingService1.Load(new object[] { glRenderer1, _sceneService! });
        IsRendererReady = true;
        RendererReady?.Invoke();
    }
}
}
