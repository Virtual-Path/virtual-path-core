using Silk.NET.Maths;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Services;
using VirtualPathCore.ViewModels;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 演示用的工业产线场景：传送带 + 三个工件 + 料箱 + 工位指示灯。
///
/// 每个物体都通过 <see cref="SceneService"/> 的公开 API 创建，与编辑器里
/// 点"添加立方体"走同一条路径 —— 这正是要验证的点：编辑器能搭的场景，
/// 无 UI 进程也能搭。
///
/// 坐标系：Y 向上，传送带沿 X 延伸，工件沿 +X 流动并回绕。
/// </summary>
internal static class DemoScene
{
    private const float BeltSpeed = 0.9f;        // 单位/秒
    private const float BeltHalfLength = 3.0f;   // 目标半长
    private const float BeltTop = 0.5f;
    private const float LoopLength = BeltHalfLength * 2f;

    /// <summary>工件初始 X 位置（落在传送带范围内）。</summary>
    private static readonly float[] WorkpieceStartX = { -1.2f, 0f, 1.2f };

    /// <summary>
    /// 工件目标边长。
    ///
    /// <para><b>尺寸换算</b>：<c>MeshFactory</c> 生成的图元以 0.5 为半边长
    /// （即未缩放时全长 1），而 <c>Transform.Scale</c> 是再乘的倍数，
    /// 因此实际全长 = <c>Scale × 2 × 0.5 = Scale</c>。
    /// 要得到目标边长 <c>S</c>，这里应填 <c>S</c> 本身（不是 <c>S/2</c>）。</para>
    ///
    /// <para>已用 meshprobe 实测验证：<c>scale=6</c> 得到全长 12 的传送带。</para>
    /// </summary>
    private const float WorkpieceSize = 1.0f;

    /// <summary>工件名称，顺序与 <see cref="WorkpieceStartX"/> 一致。</summary>
    private static readonly string[] WorkpieceNames =
        { "Workpiece_Box", "Workpiece_Ball", "Workpiece_Drum" };

    public static void Build(SceneService sceneService)
    {
        sceneService.Clear();

        // ---- 传送带主体 ----
        // Scale 即最终边长（图元未缩放时全长 1），故全长 6 => Scale = 6
        var belt = sceneService.AddCube("Conveyor_Belt");
        belt.PositionY = BeltTop - 0.15f;
        SetScale(belt, BeltHalfLength * 2f, 0.3f, 2.0f);
        SetColor(belt, 0.22f, 0.23f, 0.27f);

        // ---- 支腿 ----
        for (int i = -1; i <= 1; i++)
        {
            var leg = sceneService.AddCube($"Belt_Leg_{i}");
            leg.PositionX = i * 2.2f;
            leg.PositionY = (BeltTop - 0.3f) / 2f;
            SetScale(leg, 0.2f, BeltTop - 0.3f, 1.5f);
            SetColor(leg, 0.34f, 0.35f, 0.38f);
        }

        // ---- 三个工件：颜色各异，便于在视觉算法里做颜色识别 ----
        var box = sceneService.AddCube(WorkpieceNames[0]);
        box.PositionX = WorkpieceStartX[0];
        box.PositionY = BeltTop + WorkpieceSize / 2f;
        SetScale(box, WorkpieceSize, WorkpieceSize, WorkpieceSize);
        SetColor(box, 0.86f, 0.30f, 0.22f);      // 红

        var ball = sceneService.AddSphere(WorkpieceNames[1]);
        ball.PositionX = WorkpieceStartX[1];
        ball.PositionY = BeltTop + WorkpieceSize / 2f;
        SetScale(ball, WorkpieceSize, WorkpieceSize, WorkpieceSize);
        SetColor(ball, 0.22f, 0.72f, 0.32f);     // 绿

        var drum = sceneService.AddCylinder(WorkpieceNames[2]);
        drum.PositionX = WorkpieceStartX[2];
        drum.PositionY = BeltTop + WorkpieceSize / 2f;
        SetScale(drum, WorkpieceSize * 0.9f, WorkpieceSize / 2f, WorkpieceSize * 0.9f);
        SetColor(drum, 0.24f, 0.50f, 0.92f);     // 蓝

        // ---- 料箱（静态参照物，在画面左侧） ----
        var bin = sceneService.AddCube("Parts_Bin");
        bin.PositionX = -2.4f;
        bin.PositionY = 0.4f;
        SetScale(bin, 0.8f, 0.8f, 0.8f);
        SetColor(bin, 0.58f, 0.52f, 0.36f);

        // ---- 工位指示灯（料箱上方的黄色柱体） ----
        var beacon = sceneService.AddCylinder("Beacon");
        beacon.PositionX = -2.4f;
        beacon.PositionY = 1.05f;
        SetScale(beacon, 0.25f, 0.25f, 0.25f);
        SetColor(beacon, 0.90f, 0.78f, 0.18f);
    }

    /// <summary>
    /// 为虚拟相机设定机位。
    ///
    /// 工业检测的典型机位：略高于工件、斜向下看，保证传送带全段可见。
    /// </summary>
    public static void FrameCamera(SimpleDrawingService drawing)
    {
        // 机位按 GPU 实测确定的约定给定（MatrixSweepProgram 已验证）：
        //   Camera.Yaw = 视线方向的水平方位角，从 +X 轴起算（0° 看向 +X）
        //   OrbitCameraController 把相机放在视线的反方向上
        //   pitch < 0 表示低头俯视
        //
        // 传送带长轴沿 X、宽 2（沿 Z）。必须从侧��（视线沿 Z）看，
        // 否则正对着端头看，6 的长度被透视压扁成一块宽板。
        // yaw=90° => 视线朝 +Z，相机落在 -Z 侧。
        drawing.SetCameraPose(
            target: new Vector3D<float>(0f, 0.5f, 0f),
            distance: 9f,
            pitchDegrees: -18f,
            yawDegrees: 90f);

        drawing.SetCameraFov(50f);
    }

    /// <summary>
    /// 推进动画：工件沿传送带流动，到末端后回绕到起点左侧。
    /// </summary>
    public static void Animate(SceneService sceneService, double deltaSeconds)
    {
        float travel = BeltSpeed * (float)deltaSeconds;

        for (int i = 0; i < WorkpieceNames.Length; i++)
        {
            SceneObjectViewModel? vm = Find(sceneService, WorkpieceNames[i]);
            if (vm == null)
                continue;

            float x = vm.PositionX + travel;
            if (x > BeltHalfLength)
                x -= LoopLength;
            vm.PositionX = x;
        }
    }

    private static SceneObjectViewModel? Find(SceneService sceneService, string name)
    {
        foreach (var vm in sceneService.SceneObjects)
        {
            if (vm.Name == name)
                return vm;
        }
        return null;
    }

    /// <summary>
    /// 设置物体颜色。
    ///
    /// ViewModel 层未暴露 Color，实际存放在 <c>SceneObject.Material.Albedo</c>
    /// （见 <c>SimpleDrawingService</c> 渲染时的取值路径）。
    /// </summary>
    private static void SetColor(SceneObjectViewModel vm, float r, float g, float b)
    {
        SceneObject? obj = vm.SceneObject;
        if (obj == null)
            return;

        obj.Material ??= new Material();
        obj.Material.Albedo = new Vector4D<float>(r, g, b, 1f);
        obj.Material.Metallic = 0.1f;
        obj.Material.Roughness = 0.55f;
    }

    private static void SetScale(SceneObjectViewModel vm, float x, float y, float z)
    {
        vm.ScaleX = x;
        vm.ScaleY = y;
        vm.ScaleZ = z;
    }
}
