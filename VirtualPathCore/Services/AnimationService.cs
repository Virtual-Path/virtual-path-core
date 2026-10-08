using System;
using System.Collections.Generic;
using System.Linq;
using Silk.NET.Maths;
using VirtualPathCore.Graphics.Core;

namespace VirtualPathCore.Services;

public class AnimationService
{
    private readonly Dictionary<SceneObject, List<Keyframe>> _keyframes = new();
    private bool _isPlaying;
    private double _currentTime;
    private double _maxTime;

    public bool IsPlaying => _isPlaying;
    public double CurrentTime => _currentTime;
    public double MaxTime => _maxTime;

    public void AddKeyframe(SceneObject obj, double time, Vector3D<float> position, Quaternion<float> rotation, Vector3D<float> scale)
    {
        if (!_keyframes.ContainsKey(obj))
            _keyframes[obj] = new List<Keyframe>();

        var list = _keyframes[obj];
        list.RemoveAll(k => Math.Abs(k.Time - time) < 0.001);
        list.Add(new Keyframe(time, position, rotation, scale));
        list.Sort((a, b) => a.Time.CompareTo(b.Time));

        if (time > _maxTime)
            _maxTime = time;
    }

    public void ClearKeyframes(SceneObject obj)
    {
        if (_keyframes.ContainsKey(obj))
            _keyframes[obj].Clear();
    }

    public void Play()
    {
        _isPlaying = true;
    }

    public void Pause()
    {
        _isPlaying = false;
    }

    public void Stop()
    {
        _isPlaying = false;
        _currentTime = 0;
    }

    public void Update(double deltaSeconds)
    {
        if (!_isPlaying) return;

        _currentTime += deltaSeconds;
        if (_currentTime > _maxTime)
            _currentTime = 0; // loop

        foreach (var kvp in _keyframes)
        {
            var obj = kvp.Key;
            var frames = kvp.Value;
            if (frames.Count == 0) continue;

            Keyframe? prev = null;
            Keyframe? next = null;

            foreach (var frame in frames)
            {
                if (frame.Time <= _currentTime)
                    prev = frame;
                else if (frame.Time >= _currentTime && next == null)
                    next = frame;
            }

            if (prev != null && next != null)
            {
                float t = (float)(( _currentTime - prev.Time ) / (next.Time - prev.Time));
                obj.Transform.Position = Lerp(prev.Position, next.Position, t);
                obj.Transform.Scale = Lerp(prev.Scale, next.Scale, t);
                obj.Transform.Rotation = Slerp(prev.Rotation, next.Rotation, t);
            }
            else if (prev != null)
            {
                obj.Transform.Position = prev.Position;
                obj.Transform.Scale = prev.Scale;
                obj.Transform.Rotation = prev.Rotation;
            }
        }
    }

    private static Vector3D<float> Lerp(Vector3D<float> a, Vector3D<float> b, float t)
    {
        return new Vector3D<float>(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t);
    }

    private static Quaternion<float> Slerp(Quaternion<float> a, Quaternion<float> b, float t)
    {
        float dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
        if (dot < 0.0f)
        {
            b = new Quaternion<float>(-b.X, -b.Y, -b.Z, -b.W);
            dot = -dot;
        }

        if (dot > 0.9995f)
        {
            return new Quaternion<float>(
                a.X + t * (b.X - a.X),
                a.Y + t * (b.Y - a.Y),
                a.Z + t * (b.Z - a.Z),
                a.W + t * (b.W - a.W));
        }

        float theta0 = MathF.Acos(dot);
        float sinTheta0 = MathF.Sin(theta0);

        float theta = theta0 * t;
        float sinTheta = MathF.Sin(theta);

        float s0 = MathF.Cos(theta) - dot * sinTheta / sinTheta0;
        float s1 = sinTheta / sinTheta0;

        return new Quaternion<float>(
            s0 * a.X + s1 * b.X,
            s0 * a.Y + s1 * b.Y,
            s0 * a.Z + s1 * b.Z,
            s0 * a.W + s1 * b.W);
    }

    public class Keyframe
    {
        public double Time { get; }
        public Vector3D<float> Position { get; }
        public Quaternion<float> Rotation { get; }
        public Vector3D<float> Scale { get; }

        public Keyframe(double time, Vector3D<float> position, Quaternion<float> rotation, Vector3D<float> scale)
        {
            Time = time;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }
}
