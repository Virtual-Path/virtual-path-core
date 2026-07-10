using VirtualPathCore.Graphics.Core;
using VirtualPathCore.ViewModels;

namespace VirtualPathCore.Services.UndoRedo.Commands;

public class AddObjectCommand : IUndoableCommand
{
    private readonly SceneService _sceneService;
    private readonly SceneObject _object;
    private SceneObjectViewModel? _viewModel;

    public string Description => $"Add {_object.Name}";

    public AddObjectCommand(SceneService sceneService, SceneObject obj)
    {
        _sceneService = sceneService;
        _object = obj;
    }

    public void Execute()
    {
        if (_viewModel == null)
        {
            _viewModel = new SceneObjectViewModel(_object);
            _sceneService.Scene.AddObject(_object);
            _sceneService.SceneObjects.Add(_viewModel);
            _sceneService.SubscribeTo(_viewModel);
        }
        else
        {
            _sceneService.Scene.AddObject(_object);
            _sceneService.SceneObjects.Add(_viewModel);
            _sceneService.SubscribeTo(_viewModel);
        }
    }

    public void Undo()
    {
        if (_viewModel == null) return;
        _sceneService.Scene.RemoveObject(_object);
        _sceneService.SceneObjects.Remove(_viewModel);
    }
}
