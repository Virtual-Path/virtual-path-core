using System;
using System.Collections.Generic;
using Silk.NET.Maths;
using VirtualPathCore.Graphics.OpenGL;

namespace VirtualPathCore.Graphics.Core;

public class Material : IDisposable
{
    private bool _disposed;

    public string Name { get; set; } = "Default";

    public Vector4D<float> Albedo { get; set; } = new(1.0f, 1.0f, 1.0f, 1.0f);

    public float Metallic { get; set; } = 0.0f;

    public float Roughness { get; set; } = 1.0f;

    public Vector3D<float> Emissive { get; set; } = Vector3D<float>.Zero;

    public float EmissiveIntensity { get; set; } = 0.0f;

    public bool Wireframe { get; set; } = false;

    public bool CullFace { get; set; } = true;

    public bool DepthTest { get; set; } = true;

    public bool DepthWrite { get; set; } = true;

    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

    public Texture? AlbedoMap { get; set; }

    public Texture? NormalMap { get; set; }

    public Texture? MetallicRoughnessMap { get; set; }

    public Texture? EmissiveMap { get; set; }

    private readonly Dictionary<string, object> _customProperties = new();

    public T? GetCustomProperty<T>(string name)
    {
        if (_customProperties.TryGetValue(name, out var value) && value is T result)
        {
            return result;
        }
        return default;
    }

    public void SetCustomProperty<T>(string name, T value)
    {
        _customProperties[name] = value!;
    }

    public Material Clone()
    {
        return new Material
        {
            Name = Name + "_clone",
            Albedo = Albedo,
            Metallic = Metallic,
            Roughness = Roughness,
            Emissive = Emissive,
            EmissiveIntensity = EmissiveIntensity,
            Wireframe = Wireframe,
            CullFace = CullFace,
            DepthTest = DepthTest,
            DepthWrite = DepthWrite,
            BlendMode = BlendMode,
            AlbedoMap = AlbedoMap,
            NormalMap = NormalMap,
            MetallicRoughnessMap = MetallicRoughnessMap,
            EmissiveMap = EmissiveMap
        };
    }

    public void Apply(RenderPipeline pipeline)
    {
        pipeline.SetUniform("u_Material.Albedo", Albedo);
        pipeline.SetUniform("u_Material.Metallic", Metallic);
        pipeline.SetUniform("u_Material.Roughness", Roughness);
        pipeline.SetUniform("u_Material.Emissive", Emissive);
        pipeline.SetUniform("u_Material.EmissiveIntensity", EmissiveIntensity);

        if (AlbedoMap != null)
        {
            pipeline.SetUniform("u_Material.HasAlbedoMap", 1);
            pipeline.SetUniform("u_Material.AlbedoMap", 0, AlbedoMap);
        }
        else
        {
            pipeline.SetUniform("u_Material.HasAlbedoMap", 0);
        }

        if (NormalMap != null)
        {
            pipeline.SetUniform("u_Material.HasNormalMap", 1);
            pipeline.SetUniform("u_Material.NormalMap", 1, NormalMap);
        }
        else
        {
            pipeline.SetUniform("u_Material.HasNormalMap", 0);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        AlbedoMap?.Dispose();
        NormalMap?.Dispose();
        MetallicRoughnessMap?.Dispose();
        EmissiveMap?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public enum BlendMode
{
    Opaque,
    Masked,
    Transparent,
    Additive
}