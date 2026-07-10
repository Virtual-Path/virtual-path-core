using System;

namespace VirtualPathCore.Models;

public class ProjectData
{
    public string Name { get; set; } = "New Project";
    public string Description { get; set; } = "";
    public string Path { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastModified { get; set; } = DateTime.Now;
    public SceneData Scene { get; set; } = new();
}
