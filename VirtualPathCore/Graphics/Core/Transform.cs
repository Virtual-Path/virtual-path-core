using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.Maths;

namespace VirtualPathCore.Graphics.Core;

public class Transform
{
    private Vector3D<float> _position = Vector3D<float>.Zero;
    private Quaternion<float> _rotation = Quaternion<float>.Identity;
    private Vector3D<float> _scale = Vector3D<float>.One;

    private Matrix4X4<float> _localMatrix = Matrix4X4<float>.Identity;
    private Matrix4X4<float> _worldMatrix = Matrix4X4<float>.Identity;
    private bool _isDirty = true;

    private Transform? _parent;
    private readonly List<Transform> _children = new();

    public Vector3D<float> Position
    {
        get => _position;
        set
        {
            _position = value;
            MarkDirty();
        }
    }

    public Quaternion<float> Rotation
    {
        get => _rotation;
        set
        {
            _rotation = value;
            MarkDirty();
        }
    }

    public Vector3D<float> Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            MarkDirty();
        }
    }

    public Vector3D<float> Forward => Vector3D.Normalize(Vector3D.Transform(-Vector3D<float>.UnitZ, _rotation));
    public Vector3D<float> Back => -Forward;
    public Vector3D<float> Up => Vector3D.Normalize(Vector3D.Transform(Vector3D<float>.UnitY, _rotation));
    public Vector3D<float> Down => -Up;
    public Vector3D<float> Right => Vector3D.Normalize(Vector3D.Transform(Vector3D<float>.UnitX, _rotation));
    public Vector3D<float> Left => -Right;

    public Transform? Parent
    {
        get => _parent;
        set
        {
            if (_parent == value) return;
            if (_parent != null) _parent._children.Remove(this);
            _parent = value;
            if (_parent != null) _parent._children.Add(this);
            MarkDirty();
        }
    }

    public IReadOnlyList<Transform> Children => _children;

    public Matrix4X4<float> LocalMatrix
    {
        get
        {
            if (_isDirty)
            {
                UpdateMatrix();
            }
            return _localMatrix;
        }
    }

    public Matrix4X4<float> WorldMatrix
    {
        get
        {
            if (_isDirty)
            {
                UpdateMatrix();
            }
            return _worldMatrix;
        }
    }

    public void SetPosition(float x, float y, float z)
    {
        Position = new Vector3D<float>(x, y, z);
    }

    public void SetRotation(float pitch, float yaw, float roll)
    {
        Rotation = CreateRotation(pitch, yaw, roll);
    }

    public void SetScale(float x, float y, float z)
    {
        Scale = new Vector3D<float>(x, y, z);
    }

    public void SetScale(float uniform)
    {
        Scale = new Vector3D<float>(uniform, uniform, uniform);
    }

    public void Translate(Vector3D<float> delta)
    {
        Position += delta;
    }

    public void Translate(float x, float y, float z)
    {
        Position += new Vector3D<float>(x, y, z);
    }

    public void Rotate(Quaternion<float> delta)
    {
        _rotation = delta * _rotation;
        MarkDirty();
    }

    public void Rotate(float pitch, float yaw, float roll)
    {
        Rotation = CreateRotation(pitch, yaw, roll) * _rotation;
    }

    public void LookAt(Vector3D<float> target, Vector3D<float> up)
    {
        Vector3D<float> forward = Vector3D.Normalize(target - _position);
        Vector3D<float> right = Vector3D.Normalize(Vector3D.Cross(up, forward));
        Vector3D<float> newUp = Vector3D.Cross(forward, right);

        Matrix4X4<float> lookAt = Matrix4X4.CreateLookAt(Vector3D<float>.Zero, forward, up);

        lookAt.M14 = _position.X;
        lookAt.M24 = _position.Y;
        lookAt.M34 = _position.Z;

        _rotation = Quaternion<float>.CreateFromRotationMatrix(lookAt);
        MarkDirty();
    }

    public T GetComponent<T>() where T : class
    {
        if (this is T result) return result;
        return null!;
    }

    public T AddComponent<T>() where T : class, new()
    {
        return new T();
    }

    private void MarkDirty()
    {
        _isDirty = true;
        foreach (var child in _children)
        {
            child.MarkDirty();
        }
    }

    private void UpdateMatrix()
    {
        _localMatrix = Matrix4X4.CreateScale(_scale) *
                       Matrix4X4.CreateFromQuaternion(_rotation) *
                       Matrix4X4.CreateTranslation(_position);

        if (_parent != null)
        {
            _worldMatrix = _localMatrix * _parent.WorldMatrix;
        }
        else
        {
            _worldMatrix = _localMatrix;
        }

        _isDirty = false;
    }

    private static Quaternion<float> CreateRotation(float pitch, float yaw, float roll)
    {
        float cp = MathF.Cos(pitch);
        float sp = MathF.Sin(pitch);
        float cy = MathF.Cos(yaw);
        float sy = MathF.Sin(yaw);
        float cr = MathF.Cos(roll);
        float sr = MathF.Sin(roll);

        Quaternion<float> q = new();
        q.W = cr * cp * cy + sr * sp * sy;
        q.X = sr * cp * cy - cr * sp * sy;
        q.Y = cr * sp * cy + sr * cp * sy;
        q.Z = cr * cp * sy - sr * sp * cy;
        return q;
    }
}