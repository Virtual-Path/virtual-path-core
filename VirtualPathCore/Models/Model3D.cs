using System;
using Silk.NET.Maths;
using VirtualPathCore.Graphics;
using VirtualPathCore.Graphics.OpenGL;

namespace VirtualPathCore.Models;

public class Model3D : IDisposable
{
    private bool _disposed;

    public string Name { get; set; } = "Untitled";
    public Mesh Mesh { get; set; }
    public Vector3D<float> Position { get; set; }
    public Vector3D<float> Rotation { get; set; }
    public Vector3D<float> Scale { get; set; } = Vector3D<float>.One;
    public Vector4D<float> Color { get; set; } = new(1.0f, 0.5f, 0.2f, 1.0f);

    public Model3D(Mesh mesh)
    {
        Mesh = mesh;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Mesh?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
