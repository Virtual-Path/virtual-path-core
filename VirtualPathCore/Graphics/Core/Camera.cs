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

    /// <summary>
    /// 视图矩阵（行向量布局，与 <see cref="Projection"/> 及 <c>Transform</c> 一致）。
    /// </summary>
    /// <remarks>
    /// <para>本引擎统一使用<b>行向量</b>约定（<c>v * M</c>），矩阵链为
    /// <c>model * view * projection</c>。</para>
    /// <para><b>不要在此转置</b>。曾因误判 <c>CreateLookAt</c> 的元素布局而加过
    /// <c>Transpose</c>，结果场景物体尺寸放大 3.7 倍（实测 720px vs 期望 193px），
    /// 随后回退。</para>
    /// <para>当前约定的依据：在 GPU 上穷举 16 种
    /// (<c>SetUniform</c> 的 transpose 上传 x View 是否转置 x 4 种乘法顺序) 组合，
    /// 以各向同性球的<b>形状 + 尺寸 + 居中</b>三项为判据，
    /// 只有 <c>transpose=false + View 原样 + M*V*P</c> 全部通过
    /// (w/h=1.010、192px vs 期望 193px)。</para>
    /// <para>本属性当前与 HEAD 一致，未做实质改动。此前记录的"历史故障根因是
    /// <c>SetUniform</c> 误传 transpose=true"并不成立 —— HEAD 一直是
    /// <c>false</c>，该说法已作废。</para>
    /// </remarks>
    public Matrix4X4<float> View =>
        Matrix4X4.CreateLookAt(Position, Position + Front, Up);

    /// <summary>
    /// 投影矩阵。
    /// </summary>
    /// <remarks>
    /// 本引擎的矩阵链按<b>行向量</b>约定合成（<c>v * M</c>，见
    /// <c>SimpleDrawingService</c> 里的 <c>model * view * projection</c>
    /// 与 <c>Transform.UpdateMatrix</c>）。
    /// <see cref="Matrix4X4.CreatePerspectiveFieldOfView"/> / <c>CreateOrthographic</c>
    /// 产出的矩阵同样是行向量布局：透视除法的 <c>-1</c> 落在 <b>M34</b>，
    /// 近/远平面偏移落在 <b>M43</b>，可直接参与行向量链式运算，<b>不要转置</b>。
    /// </remarks>
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
        Position = new Vector3D<float>(0, 2, 8);
        Pitch = 0;
        Yaw = -90;
        _pitch = 0;
        _yaw = -MathHelper.PiOver2;
        UpdateVectors();
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

    /// <summary>
    /// 依据当前环绕角与距离更新相机位置，并使其朝向目标。
    /// </summary>
    /// <remarks>
    /// 角度约定（重要）：
    /// <see cref="Camera.Yaw"/> / <see cref="Camera.Pitch"/> 描述的是<b>视线方向</b>
    /// （由 <c>Camera.UpdateVectors</c> 生成，即 <c>Front</c> 向量）。
    /// 相机位于目标的<b>视线反方向</b>上，所以位置为 <c>target - d * viewDir</c>。
    ///
    /// 这样 <see cref="Camera.LookAt(Vector3D{float})"/> 反推出的角度与写入值一致，
    /// 每帧不会互相翻转（早先按 <c>target + d * viewDir</c> 计算会导致 Yaw/Pitch 每帧跳变）。
    /// </remarks>
    public override void Update(double deltaTime)
    {
        float yawRad = MathHelper.DegreesToRadians(Camera.Yaw);
        float pitchRad = MathHelper.DegreesToRadians(Camera.Pitch);

        var viewDir = new Vector3D<float>(
            MathF.Cos(pitchRad) * MathF.Cos(yawRad),
            MathF.Sin(pitchRad),
            MathF.Cos(pitchRad) * MathF.Sin(yawRad));

        Vector3D<float> position = _target - viewDir * _distance;

        Camera.SetPosition(position.X, position.Y, position.Z);
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