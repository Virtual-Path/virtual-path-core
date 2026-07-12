using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Silk.NET.Maths;
using VirtualPathCore.Graphics;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Helpers;

namespace VirtualPathCore.ViewModels;

public partial class SceneObjectViewModel : ObservableObject
{
    private readonly SceneObject _sceneObject;

    public SceneObject SceneObject => _sceneObject;

    public Action? TransformChanged { get; set; }

    private void NotifyTransformChanged()
    {
        TransformChanged?.Invoke();
        OnPropertyChanged(nameof(PositionX));
        OnPropertyChanged(nameof(PositionY));
        OnPropertyChanged(nameof(PositionZ));
        OnPropertyChanged(nameof(ScaleX));
        OnPropertyChanged(nameof(ScaleY));
        OnPropertyChanged(nameof(ScaleZ));
        OnPropertyChanged(nameof(RotationX));
        OnPropertyChanged(nameof(RotationY));
        OnPropertyChanged(nameof(RotationZ));
    }

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isVisibleInTree = true;

    [ObservableProperty]
    private bool _isSelected;

    public string Name
    {
        get => _sceneObject.Name;
        set
        {
            _sceneObject.Name = value;
            OnPropertyChanged();
        }
    }

    public bool IsVisible
    {
        get => _sceneObject.Active;
        set
        {
            _sceneObject.Active = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<SceneObjectViewModel> Children { get; } = new();

    public float PositionX
    {
        get => _sceneObject.Transform.Position.X;
        set
        {
            var p = _sceneObject.Transform.Position;
            _sceneObject.Transform.Position = new Vector3D<float>(value, p.Y, p.Z);
            OnPropertyChanged();
            NotifyTransformChanged();
        }
    }

    public float PositionY
    {
        get => _sceneObject.Transform.Position.Y;
        set
        {
            var p = _sceneObject.Transform.Position;
            _sceneObject.Transform.Position = new Vector3D<float>(p.X, value, p.Z);
            OnPropertyChanged();
            NotifyTransformChanged();
        }
    }

    public float PositionZ
    {
        get => _sceneObject.Transform.Position.Z;
        set
        {
            var p = _sceneObject.Transform.Position;
            _sceneObject.Transform.Position = new Vector3D<float>(p.X, p.Y, value);
            OnPropertyChanged();
            NotifyTransformChanged();
        }
    }

    public float ScaleX
    {
        get => _sceneObject.Transform.Scale.X;
        set
        {
            var s = _sceneObject.Transform.Scale;
            _sceneObject.Transform.Scale = new Vector3D<float>(value, s.Y, s.Z);
            OnPropertyChanged();
            NotifyTransformChanged();
        }
    }

    public float ScaleY
    {
        get => _sceneObject.Transform.Scale.Y;
        set
        {
            var s = _sceneObject.Transform.Scale;
            _sceneObject.Transform.Scale = new Vector3D<float>(s.X, value, s.Z);
            OnPropertyChanged();
            NotifyTransformChanged();
        }
    }

    public float ScaleZ
    {
        get => _sceneObject.Transform.Scale.Z;
        set
        {
            var s = _sceneObject.Transform.Scale;
            _sceneObject.Transform.Scale = new Vector3D<float>(s.X, s.Y, value);
            OnPropertyChanged();
            NotifyTransformChanged();
        }
    }

    public float RotationX
    {
        get => _sceneObject.Transform.Rotation.X;
        set
        {
            var r = _sceneObject.Transform.Rotation;
            _sceneObject.Transform.Rotation = new Quaternion<float>(value, r.Y, r.Z, r.W);
            OnPropertyChanged();
            NotifyTransformChanged();
        }
    }

    public float RotationY
    {
        get => _sceneObject.Transform.Rotation.Y;
        set
        {
            var r = _sceneObject.Transform.Rotation;
            _sceneObject.Transform.Rotation = new Quaternion<float>(r.X, value, r.Z, r.W);
            OnPropertyChanged();
            NotifyTransformChanged();
        }
    }

    public float RotationZ
    {
        get => _sceneObject.Transform.Rotation.Z;
        set
        {
            var r = _sceneObject.Transform.Rotation;
            _sceneObject.Transform.Rotation = new Quaternion<float>(r.X, r.Y, value, r.W);
            OnPropertyChanged();
            NotifyTransformChanged();
        }
    }

    public float Metallic
    {
        get => _sceneObject.Material?.Metallic ?? 0.0f;
        set
        {
            if (_sceneObject.Material != null)
            {
                _sceneObject.Material.Metallic = value;
                OnPropertyChanged();
            }
        }
    }

    public float Roughness
    {
        get => _sceneObject.Material?.Roughness ?? 1.0f;
        set
        {
            if (_sceneObject.Material != null)
            {
                _sceneObject.Material.Roughness = value;
                OnPropertyChanged();
            }
        }
    }

    public float AlbedoR
    {
        get => _sceneObject.Material?.Albedo.X ?? 1.0f;
        set
        {
            if (_sceneObject.Material != null)
            {
                var a = _sceneObject.Material.Albedo;
                _sceneObject.Material.Albedo = new Vector4D<float>(value, a.Y, a.Z, a.W);
                OnPropertyChanged();
            }
        }
    }

    public float AlbedoG
    {
        get => _sceneObject.Material?.Albedo.Y ?? 1.0f;
        set
        {
            if (_sceneObject.Material != null)
            {
                var a = _sceneObject.Material.Albedo;
                _sceneObject.Material.Albedo = new Vector4D<float>(a.X, value, a.Z, a.W);
                OnPropertyChanged();
            }
        }
    }

    public float AlbedoB
    {
        get => _sceneObject.Material?.Albedo.Z ?? 1.0f;
        set
        {
            if (_sceneObject.Material != null)
            {
                var a = _sceneObject.Material.Albedo;
                _sceneObject.Material.Albedo = new Vector4D<float>(a.X, a.Y, value, a.W);
                OnPropertyChanged();
            }
        }
    }

    // Texture file path properties
    public string? AlbedoMapPath
    {
        get => _sceneObject.Material?.GetCustomProperty<string>("AlbedoMapPath");
        set
        {
            if (_sceneObject.Material != null)
            {
                _sceneObject.Material.SetCustomProperty("AlbedoMapPath", value);
                OnPropertyChanged();
            }
        }
    }

    public string? NormalMapPath
    {
        get => _sceneObject.Material?.GetCustomProperty<string>("NormalMapPath");
        set
        {
            if (_sceneObject.Material != null)
            {
                _sceneObject.Material.SetCustomProperty("NormalMapPath", value);
                OnPropertyChanged();
            }
        }
    }

    public bool HasAlbedoMap => !string.IsNullOrEmpty(AlbedoMapPath);
    public bool HasNormalMap => !string.IsNullOrEmpty(NormalMapPath);

    public bool IsLight => _sceneObject.GetCustomProperty<Light>("Light") != null;

    public SceneObjectViewModel(SceneObject sceneObject)
    {
        _sceneObject = sceneObject;
        foreach (var child in sceneObject.Children)
        {
            Children.Add(new SceneObjectViewModel(child));
        }
    }

    public void AddChildViewModel(SceneObjectViewModel child)
    {
        _sceneObject.AddChild(child.SceneObject);
        Children.Add(child);
    }

    public void RemoveChildViewModel(SceneObjectViewModel child)
    {
        _sceneObject.RemoveChild(child.SceneObject);
        Children.Remove(child);
    }

    public SceneObjectViewModel Clone()
    {
        var clone = new SceneObject(_sceneObject.MeshBlueprint != null
            ? new MeshData((Vertex[])_sceneObject.MeshBlueprint.Vertices.Clone(), (uint[])_sceneObject.MeshBlueprint.Indices.Clone())
            : null!)
        {
            Name = _sceneObject.Name + "_copy",
            Active = _sceneObject.Active,
            Transform = { Position = _sceneObject.Transform.Position, Rotation = _sceneObject.Transform.Rotation, Scale = _sceneObject.Transform.Scale },
            Material = _sceneObject.Material?.Clone()
        };
        if (_sceneObject.Mesh != null)
            clone.Mesh = _sceneObject.Mesh;
        return new SceneObjectViewModel(clone);
    }

    public static SceneObjectViewModel CreateCube(string name = "Cube")
    {
        MeshFactory.GetCube(out Vertex[] vertices, out uint[] indices);
        var obj = new SceneObject
        {
            Name = name,
            MeshBlueprint = new MeshData(vertices, indices),
            Material = new Material
            {
                Albedo = new Vector4D<float>(1.0f, 0.5f, 0.2f, 1.0f),
                Metallic = 0.1f,
                Roughness = 0.5f
            }
        };
        return new SceneObjectViewModel(obj);
    }

    public static SceneObjectViewModel CreateSphere(string name = "Sphere")
    {
        MeshFactory.GetSphere(out Vertex[] vertices, out uint[] indices, 0.5f, 16, 16);
        var obj = new SceneObject
        {
            Name = name,
            MeshBlueprint = new MeshData(vertices, indices),
            Material = new Material
            {
                Albedo = new Vector4D<float>(0.2f, 0.6f, 1.0f, 1.0f),
                Metallic = 0.3f,
                Roughness = 0.4f
            }
        };
        return new SceneObjectViewModel(obj);
    }

    public static SceneObjectViewModel CreateCylinder(string name = "Cylinder")
    {
        MeshFactory.GetCylinder(out Vertex[] vertices, out uint[] indices);
        var obj = new SceneObject
        {
            Name = name,
            MeshBlueprint = new MeshData(vertices, indices),
            Material = new Material
            {
                Albedo = new Vector4D<float>(0.3f, 0.8f, 0.3f, 1.0f),
                Metallic = 0.2f,
                Roughness = 0.6f
            }
        };
        return new SceneObjectViewModel(obj);
    }

    public static SceneObjectViewModel CreateCone(string name = "Cone")
    {
        MeshFactory.GetCone(out Vertex[] vertices, out uint[] indices);
        var obj = new SceneObject
        {
            Name = name,
            MeshBlueprint = new MeshData(vertices, indices),
            Material = new Material
            {
                Albedo = new Vector4D<float>(0.9f, 0.7f, 0.1f, 1.0f),
                Metallic = 0.4f,
                Roughness = 0.3f
            }
        };
        return new SceneObjectViewModel(obj);
    }

    public static SceneObjectViewModel CreateTorus(string name = "Torus")
    {
        MeshFactory.GetTorus(out Vertex[] vertices, out uint[] indices);
        var obj = new SceneObject
        {
            Name = name,
            MeshBlueprint = new MeshData(vertices, indices),
            Material = new Material
            {
                Albedo = new Vector4D<float>(0.8f, 0.2f, 0.6f, 1.0f),
                Metallic = 0.6f,
                Roughness = 0.2f
            }
        };
        return new SceneObjectViewModel(obj);
    }

}
