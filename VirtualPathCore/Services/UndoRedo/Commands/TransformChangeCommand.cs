using Silk.NET.Maths;
using VirtualPathCore.Graphics.Core;

namespace VirtualPathCore.Services.UndoRedo.Commands;

public class TransformChangeCommand : IUndoableCommand
{
    private readonly SceneObject _target;
    private readonly Vector3D<float> _oldPosition;
    private readonly Vector3D<float> _newPosition;
    private readonly Quaternion<float> _oldRotation;
    private readonly Quaternion<float> _newRotation;
    private readonly Vector3D<float> _oldScale;
    private readonly Vector3D<float> _newScale;

    public string Description => $"Transform {_target.Name}";

    public TransformChangeCommand(SceneObject target,
        Vector3D<float> oldPos, Vector3D<float> newPos,
        Quaternion<float> oldRot, Quaternion<float> newRot,
        Vector3D<float> oldScl, Vector3D<float> newScl)
    {
        _target = target;
        _oldPosition = oldPos; _newPosition = newPos;
        _oldRotation = oldRot; _newRotation = newRot;
        _oldScale = oldScl; _newScale = newScl;
    }

    public void Execute()
    {
        _target.Transform.Position = _newPosition;
        _target.Transform.Rotation = _newRotation;
        _target.Transform.Scale = _newScale;
    }

    public void Undo()
    {
        _target.Transform.Position = _oldPosition;
        _target.Transform.Rotation = _oldRotation;
        _target.Transform.Scale = _oldScale;
    }
}
