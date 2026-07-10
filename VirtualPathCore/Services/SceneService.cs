using System.Collections.ObjectModel;
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

    public SceneObjectViewModel AddCube(string name = "Cube")
    {
        var vm = SceneObjectViewModel.CreateCube(name);
        _scene.AddObject(vm.SceneObject);
        SceneObjects.Add(vm);
        return vm;
    }

    public SceneObjectViewModel AddSphere(string name = "Sphere")
    {
        var vm = SceneObjectViewModel.CreateSphere(name);
        _scene.AddObject(vm.SceneObject);
        SceneObjects.Add(vm);
        return vm;
    }

    public void RemoveObject(SceneObjectViewModel vm)
    {
        _scene.RemoveObject(vm.SceneObject);
        SceneObjects.Remove(vm);
        if (SelectedObject == vm)
            SelectedObject = null;
    }

    public void Clear()
    {
        _scene.Clear();
        SceneObjects.Clear();
        SelectedObject = null;
    }
}
