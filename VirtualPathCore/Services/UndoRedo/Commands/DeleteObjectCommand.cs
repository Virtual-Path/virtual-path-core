using VirtualPathCore.Graphics.Core;
using VirtualPathCore.ViewModels;

namespace VirtualPathCore.Services.UndoRedo.Commands;

public class DeleteObjectCommand : IUndoableCommand
{
    private readonly SceneService _sceneService;
    private readonly SceneObjectViewModel _viewModel;

    public string Description => $"Delete {_viewModel.Name}";

    public DeleteObjectCommand(SceneService sceneService, SceneObjectViewModel vm)
    {
        _sceneService = sceneService;
        _viewModel = vm;
    }

    public void Execute()
    {
        _sceneService.Scene.RemoveObject(_viewModel.SceneObject);
        _sceneService.SceneObjects.Remove(_viewModel);
    }

    public void Undo()
    {
        _sceneService.Scene.AddObject(_viewModel.SceneObject);
        _sceneService.SceneObjects.Add(_viewModel);
        _sceneService.SubscribeTo(_viewModel);
    }
}
