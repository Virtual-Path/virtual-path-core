using System.Collections.Generic;

namespace VirtualPathCore.Models;

public class SceneData
{
    public string Name { get; set; } = "New Scene";
    public List<Model3D> Models { get; set; } = new();
}
