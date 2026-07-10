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

        public MainView()
        {
            InitializeComponent();

            _drawingService1 = new SimpleDrawingService();

            // Register events BEFORE DataContext is set, to catch early OnLoad
            glRenderer1.OnLoad += OnRendererLoaded;
            glRenderer1.OnUpdate += d => _drawingService1.Update(d);
            glRenderer1.OnRender += d => _drawingService1.Render(d);

            glRenderer1.OnMouseDown += (x, y) => { if (_drawingService1 is SimpleDrawingService s) s.OnMouseDown(x, y); };
            glRenderer1.OnMouseUp += () => { if (_drawingService1 is SimpleDrawingService s) s.OnMouseUp(); };
            glRenderer1.OnMouseMove += (x, y) => { if (_drawingService1 is SimpleDrawingService s) s.OnMouseMove(x, y); };
            glRenderer1.OnScroll += (d) => { if (_drawingService1 is SimpleDrawingService s) s.OnScroll(d); };
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is MainViewModel vm)
            {
                _sceneService = vm.SceneService;

                // If renderer already loaded, initialize now
                if (_rendererReady)
                {
                    InitializeDrawingService();
                }
            }
        }

        private void OnRendererLoaded()
        {
            _rendererReady = true;

            // If scene service is available, initialize now
            if (_sceneService != null)
            {
                InitializeDrawingService();
            }
        }

        private void InitializeDrawingService()
        {
            _drawingService1.Load(new object[] { glRenderer1, _sceneService! });
        }
    }
}
