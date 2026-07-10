using System.Collections.ObjectModel;
using System.ComponentModel;
using Silk.NET.OpenGLES;
using VirtualPathCore.Graphics;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Graphics.OpenGL;
using VirtualPathCore.ViewModels;

namespace VirtualPathCore.Services;

public class SceneService
{
    private IGraphicsHost<GL>? _graphicsHost;
    private readonly Scene _scene;

    public Scene Scene => _scene;
    public ObservableCollection<SceneObjectViewModel> SceneObjects { get; } = new();
    public SceneObjectViewModel? SelectedObject { get; set; }
    public GizmoMode GizmoMode { get; set; } = GizmoMode.Translate;

    public SceneService(IGraphicsHost<GL>? graphicsHost = null)
    {
        _graphicsHost = graphicsHost;
        _scene = new Scene { Name = "MainScene" };
    }

    public void SetHost(IGraphicsHost<GL> host)
    {
        _graphicsHost = host;
    }

    public bool CanCreateObjects => _graphicsHost != null;

    private void RequestRender()
    {
        if (_graphicsHost is Renderer r)
            r.RequestRender();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        RequestRender();
    }

    public void SubscribeTo(SceneObjectViewModel vm)
    {
        vm.PropertyChanged += OnViewModelChanged;
    }

    private void Subscribe(SceneObjectViewModel vm)
    {
        vm.PropertyChanged += OnViewModelChanged;
    }

    private void Unsubscribe(SceneObjectViewModel vm)
    {
        vm.PropertyChanged -= OnViewModelChanged;
    }

    public SceneObjectViewModel AddCube(string name = "Cube")
    {
        var vm = SceneObjectViewModel.CreateCube(name);
        _scene.AddObject(vm.SceneObject);
        SceneObjects.Add(vm);
        Subscribe(vm);
        RequestRender();
        return vm;
    }

    public SceneObjectViewModel AddSphere(string name = "Sphere")
    {
        var vm = SceneObjectViewModel.CreateSphere(name);
        _scene.AddObject(vm.SceneObject);
        SceneObjects.Add(vm);
        Subscribe(vm);
        RequestRender();
        return vm;
    }

    public SceneObjectViewModel DuplicateObject(SceneObjectViewModel source)
    {
        var vm = source.Clone();
        _scene.AddObject(vm.SceneObject);
        SceneObjects.Add(vm);
        Subscribe(vm);
        RequestRender();
        return vm;
    }

    public void RemoveObject(SceneObjectViewModel vm)
    {
        Unsubscribe(vm);
        _scene.RemoveObject(vm.SceneObject);
        SceneObjects.Remove(vm);
        if (SelectedObject == vm)
            SelectedObject = null;
        RequestRender();
    }

    public void Clear()
    {
        foreach (var vm in SceneObjects)
            Unsubscribe(vm);
        _scene.Clear();
        SceneObjects.Clear();
        SelectedObject = null;
        RequestRender();
    }
}
