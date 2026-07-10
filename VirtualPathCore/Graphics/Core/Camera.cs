using System;
using VirtualPathCore.Helpers;
using Silk.NET.Maths;

namespace VirtualPathCore.Graphics.Core;

public class Camera
{
    private Vector3D<float> _front = -Vector3D<float>.UnitZ;
    private Vector3D<float> _up = Vector3D<float>.UnitY;
    private Vector3D<float> _right = Vector3D<float>.UnitX;
    private float _pitch;
    private float _yaw = -MathHelper.PiOver2;
    private float _fov = MathHelper.PiOver2;
    private float _orthoSize = 5.0f;

    public int Width { get; set; }
    public int Height { get; set; }

    public Vector3D<float> Position { get; set; } = new(0.0f, 0.0f, 5.0f);

    public Vector3D<float> Front => _front;
    public Vector3D<float> Back => -_front;
    public Vector3D<float> Up => _up;
    public Vector3D<float> Down => -_up;
    public Vector3D<float> Right => _right;
    public Vector3D<float> Left => -_right;

    public ProjectionType ProjectionType { get; set; } = ProjectionType.Perspective;

    public float Near { get; set; } = 0.1f;
    public float Far { get; set; } = 1000.0f;

    public float Pitch
    {
        get => MathHelper.RadiansToDegrees(_pitch);
        set
        {
            _pitch = MathHelper.DegreesToRadians(MathHelper.Clamp(value, -89f, 89f));
            UpdateVectors();
        }
    }

    public float Yaw
    {
        get => MathHelper.RadiansToDegrees(_yaw);
        set
        {
            _yaw = MathHelper.DegreesToRadians(value);
            UpdateVectors();
        }
    }

    public float Fov
    {
        get => MathHelper.RadiansToDegrees(_fov);
        set
        {
            _fov = MathHelper.DegreesToRadians(MathHelper.Clamp(value, 1f, 90f));
        }
    }

    public float OrthoSize
    {
        get => _orthoSize;
        set => _orthoSize = MathHelper.Max(value, 0.01f);
    }

    public float AspectRatio => Height > 0 ? (float)Width / Height : 1.0f;

    public Matrix4X4<float> View => Matrix4X4.CreateLookAt(Position, Position + Front, Up);

    public Matrix4X4<float> Projection => ProjectionType switch
    {
        ProjectionType.Perspective => Matrix4X4.CreatePerspectiveFieldOfView(_fov, AspectRatio, Near, Far),
        ProjectionType.Orthographic => Matrix4X4.CreateOrthographic(OrthoSize * 2 * AspectRatio, OrthoSize * 2, Near, Far),
        _ => Matrix4X4<float>.Identity
    };

    public void SetPosition(float x, float y, float z)
    {
        Position = new Vector3D<float>(x, y, z);
    }

    public void SetRotation(float pitch, float yaw)
    {
        Pitch = pitch;
        Yaw = yaw;
    }

    public void Translate(Vector3D<float> delta)
    {
        Position += delta;
    }

    public void Translate(float x, float y, float z)
    {
        Position += new Vector3D<float>(x, y, z);
    }

    public void Rotate(float pitchDelta, float yawDelta)
    {
        Pitch += pitchDelta;
        Yaw += yawDelta;
    }

    public void LookAt(Vector3D<float> target)
    {
        Vector3D<float> forward = Vector3D.Normalize(target - Position);
        _pitch = MathF.Asin(forward.Y);
        _yaw = MathF.Atan2(forward.Z, forward.X);
        UpdateVectors();
    }

    public void Reset()
    {
        Position = new Vector3D<float>(0, 0, 5);
        Pitch = 0;
        Yaw = -90;
    }

    private void UpdateVectors()
    {
        _front.X = MathF.Cos(_pitch) * MathF.Cos(_yaw);
        _front.Y = MathF.Sin(_pitch);
        _front.Z = MathF.Cos(_pitch) * MathF.Sin(_yaw);

        _front = Vector3D.Normalize(_front);
        _right = Vector3D.Normalize(Vector3D.Cross(_front, Vector3D<float>.UnitY));
        _up = Vector3D.Normalize(Vector3D.Cross(_right, _front));
    }
}

public enum ProjectionType
{
    Perspective,
    Orthographic
}

public abstract class CameraController
{
    protected Camera Camera { get; }

    protected CameraController(Camera camera)
    {
        Camera = camera;
    }

    public abstract void Update(double deltaTime);
}

public class OrbitCameraController : CameraController
{
    private float _distance = 10.0f;
    private float _orbitSpeed = 0.5f;
    private float _zoomSpeed = 1.0f;
    private float _panSpeed = 0.01f;

    private Vector3D<float> _target = Vector3D<float>.Zero;

    public OrbitCameraController(Camera camera) : base(camera)
    {
    }

    public float Distance
    {
        get => _distance;
        set => _distance = MathHelper.Max(value, 0.1f);
    }

    public Vector3D<float> Target
    {
        get => _target;
        set => _target = value;
    }

    public void Orbit(float deltaX, float deltaY)
    {
        Camera.Yaw += deltaX * _orbitSpeed;
        Camera.Pitch += deltaY * _orbitSpeed;
    }

    public void Zoom(float delta)
    {
        Distance -= delta * _zoomSpeed;
    }

    public void Pan(float deltaX, float deltaY)
    {
        Vector3D<float> right = Camera.Right;
        Vector3D<float> up = Camera.Up;

        _target += right * (-deltaX * _panSpeed * _distance);
        _target += up * (deltaY * _panSpeed * _distance);
    }

    public override void Update(double deltaTime)
    {
        float yawRad = MathHelper.DegreesToRadians(Camera.Yaw);
        float pitchRad = MathHelper.DegreesToRadians(Camera.Pitch);

        float x = _distance * MathF.Cos(pitchRad) * MathF.Cos(yawRad);
        float y = _distance * MathF.Sin(pitchRad);
        float z = _distance * MathF.Cos(pitchRad) * MathF.Sin(yawRad);

        Camera.SetPosition(_target.X + x, _target.Y + y, _target.Z + z);
        Camera.LookAt(_target);
    }
}

public class FreeFlyCameraController : CameraController
{
    private float _moveSpeed = 5.0f;
    private float _lookSpeed = 0.2f;

    private bool _mouseDown;
    private float _lastMouseX;
    private float _lastMouseY;

    public FreeFlyCameraController(Camera camera) : base(camera)
    {
    }

    public float MoveSpeed
    {
        get => _moveSpeed;
        set => _moveSpeed = MathHelper.Max(value, 0.1f);
    }

    public float LookSpeed
    {
        get => _lookSpeed;
        set => _lookSpeed = MathHelper.Max(value, 0.01f);
    }

    public void OnMouseDown(float x, float y)
    {
        _mouseDown = true;
        _lastMouseX = x;
        _lastMouseY = y;
    }

    public void OnMouseUp()
    {
        _mouseDown = false;
    }

    public void OnMouseMove(float x, float y)
    {
        if (!_mouseDown) return;

        float deltaX = x - _lastMouseX;
        float deltaY = y - _lastMouseY;

        Camera.Yaw -= deltaX * _lookSpeed;
        Camera.Pitch -= deltaY * _lookSpeed;

        _lastMouseX = x;
        _lastMouseY = y;
    }

    public void MoveForward(double deltaTime)
    {
        Camera.Translate(Camera.Front * (_moveSpeed * (float)deltaTime));
    }

    public void MoveBackward(double deltaTime)
    {
        Camera.Translate(Camera.Back * (_moveSpeed * (float)deltaTime));
    }

    public void MoveLeft(double deltaTime)
    {
        Camera.Translate(Camera.Left * (_moveSpeed * (float)deltaTime));
    }

    public void MoveRight(double deltaTime)
    {
        Camera.Translate(Camera.Right * (_moveSpeed * (float)deltaTime));
    }

    public void MoveUp(double deltaTime)
    {
        Camera.Translate(Camera.Up * (_moveSpeed * (float)deltaTime));
    }

    public void MoveDown(double deltaTime)
    {
        Camera.Translate(Camera.Down * (_moveSpeed * (float)deltaTime));
    }

    public override void Update(double deltaTime)
    {
    }
}