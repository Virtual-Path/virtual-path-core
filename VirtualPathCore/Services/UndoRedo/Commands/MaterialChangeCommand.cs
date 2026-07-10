using Silk.NET.Maths;
using VirtualPathCore.Graphics.Core;

namespace VirtualPathCore.Services.UndoRedo.Commands;

public class MaterialChangeCommand : IUndoableCommand
{
    private readonly Material _material;
    private readonly Vector4D<float> _oldAlbedo;
    private readonly Vector4D<float> _newAlbedo;
    private readonly float _oldMetallic;
    private readonly float _newMetallic;
    private readonly float _oldRoughness;
    private readonly float _newRoughness;

    public string Description => $"Material change";

    public MaterialChangeCommand(Material material,
        Vector4D<float> oldAlbedo, Vector4D<float> newAlbedo,
        float oldMetallic, float newMetallic,
        float oldRoughness, float newRoughness)
    {
        _material = material;
        _oldAlbedo = oldAlbedo; _newAlbedo = newAlbedo;
        _oldMetallic = oldMetallic; _newMetallic = newMetallic;
        _oldRoughness = oldRoughness; _newRoughness = newRoughness;
    }

    public void Execute()
    {
        _material.Albedo = _newAlbedo;
        _material.Metallic = _newMetallic;
        _material.Roughness = _newRoughness;
    }

    public void Undo()
    {
        _material.Albedo = _oldAlbedo;
        _material.Metallic = _oldMetallic;
        _material.Roughness = _oldRoughness;
    }
}
