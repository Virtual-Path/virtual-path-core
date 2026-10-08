using System;
using Silk.NET.Maths;
using VirtualPathCore.Graphics.Core;
using Xunit;
using Xunit.Abstractions;

namespace VirtualPathCore.Tests;

public class TransformTests
{
    private readonly ITestOutputHelper _output;

    public TransformTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PrintMatrixLayout()
    {
        var T = Matrix4X4.CreateTranslation(1f, 2f, 3f);
        _output.WriteLine($"T: M11={T.M11} M22={T.M22} M33={T.M33} M14={T.M14} M24={T.M24} M34={T.M34} M41={T.M41} M42={T.M42} M43={T.M43}");
        var S = Matrix4X4.CreateScale(2f, 2f, 2f);
        var R = Matrix4X4.CreateFromQuaternion(Quaternion<float>.Identity);
        var M1 = S * R * T;
        _output.WriteLine($"S*R*T: M11={M1.M11} M22={M1.M22} M33={M1.M33} M14={M1.M14} M24={M1.M24} M34={M1.M34} M41={M1.M41} M42={M1.M42} M43={M1.M43}");
        var M2 = T * R * S;
        _output.WriteLine($"T*R*S: M11={M2.M11} M22={M2.M22} M33={M2.M33} M14={M2.M14} M24={M2.M24} M34={M2.M34} M41={M2.M41} M42={M2.M42} M43={M2.M43}");
    }

    [Fact]
    public void LocalMatrix_Should_OrderTranslationAfterScale()
    {
        var t = new Transform();
        t.SetScale(2f, 2f, 2f);
        t.SetPosition(1f, 2f, 3f);

        var m = t.LocalMatrix;
        _output.WriteLine($"M11={m.M11} M12={m.M12} M13={m.M13} M14={m.M14}");
        _output.WriteLine($"M21={m.M21} M22={m.M22} M23={m.M23} M24={m.M24}");
        _output.WriteLine($"M31={m.M31} M32={m.M32} M33={m.M33} M34={m.M34}");
        _output.WriteLine($"M41={m.M41} M42={m.M42} M43={m.M43} M44={m.M44}");

        var origin = Vector3D<float>.Zero;
        var worldOrigin = Vector3D.Transform(origin, m);
        _output.WriteLine($"origin -> {worldOrigin}");

        Assert.Equal(new Vector3D<float>(1f, 2f, 3f), worldOrigin);
    }

    [Fact]
    public void WorldMatrix_ShouldIncludeParent()
    {
        var parent = new Transform();
        parent.SetPosition(1f, 0f, 0f);

        var child = new Transform();
        child.Parent = parent;
        child.SetPosition(0f, 2f, 0f);

        var m = child.WorldMatrix;
        _output.WriteLine($"M41={m.M41} M42={m.M42} M43={m.M43}");

        Assert.Equal(1f, m.M41);
        Assert.Equal(2f, m.M42);
        Assert.Equal(0f, m.M43);
    }

    [Fact]
    public void CameraProjection_Orthographic_ShouldUseAspectRatio()
    {
        var cam = new Camera();
        cam.ProjectionType = ProjectionType.Orthographic;
        cam.OrthoSize = 5f;
        cam.Width = 800;
        cam.Height = 600;

        var p = cam.Projection;

        float expectedWidth = 5f * 2 * (800f / 600f);
        Assert.Equal(expectedWidth, 2f / p.M11, 4);
    }

    [Fact]
    public void Camera_LookAt_ShouldOrientTowardTarget()
    {
        var cam = new Camera();
        cam.SetPosition(0, 0, 5);
        cam.LookAt(new Vector3D<float>(0, 0, 0));

        var view = cam.View;
        var originWorld = Vector3D<float>.Zero;
        var projected = Vector3D.Transform(originWorld, view);
        Assert.True(projected.Z < 0);
    }
}
