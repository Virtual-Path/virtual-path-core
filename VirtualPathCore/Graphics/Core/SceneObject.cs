using System;
using System.Collections.Generic;
using Silk.NET.Maths;
using VirtualPathCore.Graphics.OpenGL;

namespace VirtualPathCore.Graphics.Core;

public class MeshData
{
    public Vertex[] Vertices { get; }
    public uint[] Indices { get; }

    public MeshData(Vertex[] vertices, uint[] indices)
    {
        Vertices = vertices;
        Indices = indices;
    }
}

public class SceneObject : IDisposable
{
    private bool _disposed;

    public string Name { get; set; } = "Object";

    public bool Active { get; set; } = true;

    public Transform Transform { get; } = new();

    public Mesh? Mesh { get; set; }

    public MeshData? MeshBlueprint { get; set; }

    public Material? Material { get; set; }

    public int Layer { get; set; } = 0;

    public SceneObject? Parent
    {
        get => _parent;
        set
        {
            if (_parent == value) return;
            if (_parent != null) _parent._children.Remove(this);
            _parent = value;
            if (_parent != null) _parent._children.Add(this);
            Transform.Parent = value?.Transform;
        }
    }

    private SceneObject? _parent;
    private readonly List<SceneObject> _children = new();

    public IReadOnlyList<SceneObject> Children => _children;

    public T? GetComponent<T>() where T : class
    {
        if (this is T result) return result;
        return null;
    }

    public T? GetComponentInChildren<T>() where T : class
    {
        if (GetComponent<T>() is T result) return result;
        foreach (var child in _children)
        {
            if (child.GetComponentInChildren<T>() is T childResult) return childResult;
        }
        return null;
    }

    public T? GetComponentInParent<T>() where T : class
    {
        if (_parent == null) return null;
        if (_parent is T result) return result;
        return _parent.GetComponentInParent<T>();
    }

    public void AddChild(SceneObject child)
    {
        child.Parent = this;
    }

    public void RemoveChild(SceneObject child)
    {
        if (child.Parent == this)
        {
            child.Parent = null;
        }
    }

    public void Draw(RenderPipeline pipeline)
    {
        if (!Active || Mesh == null || Material == null) return;

        Matrix4X4<float> worldMatrix = Transform.WorldMatrix;

        pipeline.SetUniform("u_Model", worldMatrix);

        Material.Apply(pipeline);

        Mesh.Draw();
    }

    public virtual void Update(double deltaTime)
    {
        if (!Active) return;

        foreach (var child in _children)
        {
            child.Update(deltaTime);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        Mesh?.Dispose();
        Material?.Dispose();

        foreach (var child in _children.ToArray())
        {
            child.Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public class Scene : IDisposable
{
    private bool _disposed;

    public string Name { get; set; } = "Scene";

    private readonly List<SceneObject> _objects = new();
    private readonly object _objectsLock = new();
    private volatile SceneObject[] _snapshot = Array.Empty<SceneObject>();

    public IReadOnlyList<SceneObject> Objects => _snapshot;

    public Camera? MainCamera { get; set; }

    public LightingData Lighting { get; } = new();

    public SceneObject CreateObject(string name = "Object")
    {
        var obj = new SceneObject { Name = name };
        lock (_objectsLock) { _objects.Add(obj); _snapshot = _objects.ToArray(); }
        return obj;
    }

    public void RemoveObject(SceneObject obj)
    {
        lock (_objectsLock) { _objects.Remove(obj); _snapshot = _objects.ToArray(); }
        obj.Dispose();
    }

    public void AddObject(SceneObject obj)
    {
        lock (_objectsLock)
        {
            if (!_objects.Contains(obj))
            {
                _objects.Add(obj);
                _snapshot = _objects.ToArray();
            }
        }
    }

    public void Clear()
    {
        SceneObject[] toDispose;
        lock (_objectsLock)
        {
            toDispose = _objects.ToArray();
            _objects.Clear();
            _snapshot = Array.Empty<SceneObject>();
        }
        foreach (var obj in toDispose)
        {
            obj.Dispose();
        }
    }

    public void Update(double deltaTime)
    {
        foreach (var obj in _snapshot)
        {
            obj.Update(deltaTime);
        }
    }

    public void Render(RenderPipeline pipeline, Camera camera)
    {
        SceneObject[] snapshot;
        lock (_objectsLock) { snapshot = _objects.ToArray(); }
        foreach (var obj in snapshot)
        {
            if (obj.Active && obj.Mesh != null && obj.Material != null)
            {
                pipeline.SetUniform("u_View", camera.View);
                pipeline.SetUniform("u_Projection", camera.Projection);

                pipeline.SetUniform("u_Model", obj.Transform.WorldMatrix);
                obj.Material.Apply(pipeline);
                obj.Mesh.Draw();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}