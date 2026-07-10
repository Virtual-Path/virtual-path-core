using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VirtualPathCore.Models;

public class ProjectData
{
    public string Name { get; set; } = "New Project";
    public string Description { get; set; } = "";
    public string Path { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastModified { get; set; } = DateTime.Now;
    public SceneData Scene { get; set; } = new();
    public List<SerializedMaterial> Materials { get; set; } = new();
    public List<CameraBookmark> CameraBookmarks { get; set; } = new();
}

public class SerializedMaterial
{
    public string Name { get; set; } = "Material";
    public float[] Albedo { get; set; } = new[] { 1f, 1f, 1f, 1f };
    public float Metallic { get; set; }
    public float Roughness { get; set; } = 1f;
    public float[] Emissive { get; set; } = new[] { 0f, 0f, 0f };
    public float EmissiveIntensity { get; set; }
    public string? AlbedoMapPath { get; set; }
    public string? NormalMapPath { get; set; }
}

public class CameraBookmark
{
    public string Name { get; set; } = "Bookmark";
    public float[] Position { get; set; } = new[] { 0f, 0f, 5f };
    public float Yaw { get; set; }
    public float Pitch { get; set; }
}
