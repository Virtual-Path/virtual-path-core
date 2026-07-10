using System;
using System.Collections.Generic;
using Silk.NET.Maths;
using VirtualPathCore.Helpers;

namespace VirtualPathCore.Graphics.Core;

public abstract class Light
{
    public string Name { get; set; } = "Light";

    public bool Enabled { get; set; } = true;

    public Vector3D<float> Color { get; set; } = Vector3D<float>.One;

    public float Intensity { get; set; } = 1.0f;
}

public class DirectionalLight : Light
{
    public Vector3D<float> Direction { get; set; } = -Vector3D<float>.UnitY;

    public bool CastShadows { get; set; } = true;

    public float ShadowStrength { get; set; } = 1.0f;
}

public class PointLight : Light
{
    public Vector3D<float> Position { get; set; } = Vector3D<float>.Zero;

    public float Constant { get; set; } = 1.0f;

    public float Linear { get; set; } = 0.09f;

    public float Quadratic { get; set; } = 0.032f;

    public float Distance { get; set; } = 100.0f;

    public bool CastShadows { get; set; } = false;
}

public class SpotLight : Light
{
    public Vector3D<float> Position { get; set; } = Vector3D<float>.Zero;

    public Vector3D<float> Direction { get; set; } = -Vector3D<float>.UnitZ;

    public float CutOff { get; set; } = MathHelper.DegreesToRadians(12.5f);

    public float OuterCutOff { get; set; } = MathHelper.DegreesToRadians(15.0f);

    public float Constant { get; set; } = 1.0f;

    public float Linear { get; set; } = 0.09f;

    public float Quadratic { get; set; } = 0.032f;

    public bool CastShadows { get; set; } = false;
}

public class AmbientLight
{
    public Vector3D<float> Color { get; set; } = new(0.03f, 0.03f, 0.03f);

    public float Intensity { get; set; } = 1.0f;
}

public class LightingData
{
    public AmbientLight Ambient { get; set; } = new();

    public DirectionalLight? DirectionalLight { get; set; }

    public List<PointLight> PointLights { get; set; } = new();

    public List<SpotLight> SpotLights { get; set; } = new();
}