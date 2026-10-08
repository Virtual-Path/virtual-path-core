using Silk.NET.Maths;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Services;
using VirtualPathCore.ViewModels;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 工业检测产线场景：上料 → 传送 → 检测工位 → 合格/不合格分流 → 料箱。
///
/// <para><b>设计意图</b>：让 vision 侧的检测链路能在近乎真实的条件下被验证，
/// 而不是对着三个静止的彩色方块调参。因此场景包含几类真实产线必然存在的干扰：
/// </para>
/// <list type="bullet">
/// <item>工件颜色多样但都属于同一产品族，红/绿/蓝各一件，交替经过检测位；</item>
/// <item>不合格品（暗色、带缺陷标记）混入合格流，用于验证 pass/fail 判定；</item>
/// <item>背景有同样鲜艳的颜色（指示灯、警示条），用于验证检测器不会被非工件干扰；</item>
/// <item>工位有明显的检测区域标记，给 vision 侧做 ROI 的参照。</item>
/// </list>
///
/// <para><b>坐标系</b>：Y 向上，产线沿 X 延伸，工件沿 +X 流动。
/// 传送带顶面高度 <see cref="BeltTop"/>，工件底面贴合该高度。</para>
/// </summary>
internal static class DemoScene
{
    // ---- 产线几何 ----
    private const float BeltTop = 0.5f;          // 传送带顶面高度
    private const float BeltHalfLength = 3.4f;   // 传送带半长（全长 6.8）
    private const float BeltWidth = 1.6f;        // 带宽（沿 Z）
    private const float BeltSpeed = 0.75f;       // 输送速度 单位/秒
    private const float LoopLength = BeltHalfLength * 2f;

    /// <summary>
    /// 背景墙所在的 Z。相机在 -Z 侧，因此墙必须比相机更靠 +Z，
    /// 否则相机会被包进墙里，画面只剩墙面内表面。
    /// 相机在 yaw=90°、distance=distance 时 Z ≈ -distance，
    /// 这里取 1.5f 让墙位于产线另一侧、远处成背景。
    /// </summary>
    private const float WallZ = 1.5f;

    /// <summary>检测工位中心 X。vision 侧应把 ROI 对准这个位置。</summary>
    public const float InspectionX = 0.0f;

    /// <summary>检测区半宽，用于在画面上标出检测带。</summary>
    public const float InspectionHalfWidth = 0.75f;

    // ---- 工件规格 ----
    private const float WorkpieceSize = 0.9f;   // 立方工件边长
    private const float SphereRadius = 0.45f;   // 球形工件半径（scale 语义见下）

    /// <summary>
    /// 工件在环上的初始 X 位置，等间距分布。
    /// 三个位置让同一时刻画面上最多出现两件，便于视觉侧验证"逐件计数"。
    /// </summary>
    private static readonly float[] StartX = { -2.2f, 0.6f, 1.9f };

    /// <summary>
    /// 工件定义：名称、图元、颜色、以及是否为不合格品。
    ///
    /// <para><b>不合格品的表达方式</b>：暗红近黑的底色 + 顶部一根黑色标记柱。
    /// 视觉侧若按颜色分类，会把它归入"红"；若按亮度阈值判定，
    /// 会发现它比合格红件暗得多。两种判据都能识别出它是不合格品。</para>
    /// </summary>
    private sealed record WorkpieceSpec(
        string Name,
        Func<SceneService, SceneObjectViewModel> Create,
        float R, float G, float B,
        bool IsDefect,
        float HalfHeight);

    private static readonly WorkpieceSpec[] Specs =
    {
        // 合格品：红箱、绿球、蓝桶
        new("WP_Red_Box",
            s => s.AddCube("WP_Red_Box"), 0.82f, 0.28f, 0.20f, false, WorkpieceSize / 2f),
        new("WP_Green_Sphere",
            s => s.AddSphere("WP_Green_Sphere"), 0.20f, 0.68f, 0.30f, false, SphereRadius),
        new("WP_Blue_Drum",
            s => s.AddCylinder("WP_Blue_Drum"), 0.22f, 0.46f, 0.88f, false, 0.30f),

        // 不合格品：暗色箱体 + 顶部黑色标记柱
        new("WP_Defect_Box",
            s => s.AddCube("WP_Defect_Box"), 0.26f, 0.08f, 0.06f, true, WorkpieceSize / 2f),
    };

    /// <summary>不合格品顶部标记柱的名称，供动画更新可见性。</summary>
    private const string DefectMarkerName = "WP_Defect_Marker";

    /// <summary>按 Specs 顺序排列，与 <see cref="StartX"/> 一一对应。</summary>
    private static SceneObjectViewModel[] _workpieces = Array.Empty<SceneObjectViewModel>();

    public static void Build(SceneService sceneService)
    {
        sceneService.Clear();

        BuildConveyor(sceneService);
        BuildInspectionStation(sceneService);
        BuildBins(sceneService);
        BuildEnvironment(sceneService);
        BuildWorkpieces(sceneService);
    }

    // ================= 传送带 =================

    private static void BuildConveyor(SceneService sceneService)
    {
        // 主体带面
        // 带面颜色必须明显亮于地面（0.24），否则带体与地面融为一体，
        // 工件看起来像悬空。真实传送带多为深灰橡胶，这里取略暖的深灰。
        var belt = sceneService.AddCube("Conveyor_Belt");
        belt.PositionY = BeltTop - 0.12f;
        SetScale(belt, BeltHalfLength * 2f, 0.24f, BeltWidth);
        SetColor(belt, 0.40f, 0.41f, 0.45f, 0.05f, 0.70f);

        // 带面两侧的挡边，防止工件"掉出画面"的视觉暗示
        for (int i = 0; i < 2; i++)
        {
            float z = (i == 0 ? 1f : -1f) * (BeltWidth / 2f + 0.03f);
            var rail = sceneService.AddCube($"Conveyor_Rail_{i}");
            rail.PositionY = BeltTop + 0.06f;
            rail.PositionZ = z;
            SetScale(rail, BeltHalfLength * 2f, 0.12f, 0.06f);
            SetColor(rail, 0.42f, 0.44f, 0.48f, 0.35, 0.45);
        }

        // 支腿
        for (int i = -2; i <= 2; i++)
        {
            var leg = sceneService.AddCube($"Belt_Leg_{i}");
            leg.PositionX = i * 1.5f;
            leg.PositionY = (BeltTop - 0.24f) / 2f;
            SetScale(leg, 0.16f, BeltTop - 0.24f, 0.16f);
            SetColor(leg, 0.30f, 0.31f, 0.34f, 0.30, 0.55);
        }
    }

    // ================= 检测工位 =================

    private static void BuildInspectionStation(SceneService sceneService)
    {
        // 检测区两侧的立柱，界定 vision 侧 ROI 的视觉边界
        for (int i = 0; i < 2; i++)
        {
            float x = InspectionX + (i == 0 ? -1f : 1f) * (InspectionHalfWidth + 0.25f);
            var post = sceneService.AddCube($"Station_Post_{i}");
            post.PositionX = x;
            post.PositionY = BeltTop + 0.75f;
            post.PositionZ = -(BeltWidth / 2f + 0.35f);
            SetScale(post, 0.12f, 1.5f, 0.12f);
            SetColor(post, 0.55f, 0.57f, 0.60f, 0.40, 0.40);
        }

        // 顶部横梁
        var beam = sceneService.AddCube("Station_Beam");
        beam.PositionX = InspectionX;
        beam.PositionY = BeltTop + 1.52f;
        beam.PositionZ = -(BeltWidth / 2f + 0.35f);
        SetScale(beam, 2.3f, 0.10f, 0.10f);
        SetColor(beam, 0.55f, 0.57f, 0.60f, 0.40, 0.40);

        // 检测区地面标线（两条，沿 X 方向的细长条）
        for (int i = 0; i < 2; i++)
        {
            float x = InspectionX + (i == 0 ? -1f : 1f) * InspectionHalfWidth;
            var line = sceneService.AddCube($"Station_Line_{i}");
            line.PositionX = x;
            line.PositionY = 0.012f;
            SetScale(line, 0.03f, 0.02f, BeltWidth);
            SetColor(line, 0.92f, 0.78f, 0.16f, 0.1, 0.7);   // 黄色警戒线
        }

        // 工位指示灯（三色灯塔）
        var beaconBase = sceneService.AddCylinder("Station_Beacon");
        beaconBase.PositionX = InspectionX;
        beaconBase.PositionY = BeltTop + 1.62f;
        beaconBase.PositionZ = -(BeltWidth / 2f + 0.35f);
        SetScale(beaconBase, 0.20f, 0.10f, 0.20f);
        SetColor(beaconBase, 0.20f, 0.20f, 0.22f, 0.3, 0.5);

        var beacon = sceneService.AddSphere("Station_Beacon_Lamp");
        beacon.PositionX = InspectionX;
        beacon.PositionY = BeltTop + 1.76f;
        beacon.PositionZ = -(BeltWidth / 2f + 0.35f);
        SetScale(beacon, 0.18f, 0.18f, 0.18f);
        SetColor(beacon, 0.94f, 0.24f, 0.20f, 0.05, 0.35);  // 红色报警灯
    }

    // ================= 料箱 =================

    private static void BuildBins(SceneService sceneService)
    {
        // 合格品箱（产线下游，+X 端）
        var okBin = sceneService.AddCube("Bin_OK");
        okBin.PositionX = BeltHalfLength + 1.5f;
        okBin.PositionY = 0.45f;
        SetScale(okBin, 0.9f, 0.9f, 1.0f);
        SetColor(okBin, 0.20f, 0.42f, 0.28f, 0.1f, 0.6);      // 深绿：合格标识

        // 不合格箱（产线上游，-X 端）
        var ngBin = sceneService.AddCube("Bin_NG");
        ngBin.PositionX = -BeltHalfLength - 1.5f;
        ngBin.PositionY = 0.45f;
        SetScale(ngBin, 0.9f, 0.9f, 1.0f);
        SetColor(ngBin, 0.55f, 0.16f, 0.12f, 0.1f, 0.6);      // 暗红：不合格标识
    }

    // ================= 环境（干扰项） =================

    private static void BuildEnvironment(SceneService sceneService)
    {
        // 背景墙。
        //
        // 相机位于 -Z 侧（yaw=90° 时落在 -Z），因此墙必须比相机更靠 +Z，
        // 否则会把相机包在里面、整个画面只剩墙面内表面。
        // 墙只用来给画面一个"车间"背景，不要参与检测，尺寸按覆盖视锥取。
        var wall = sceneService.AddCube("Factory_Wall");
        wall.PositionY = 2.4f;
        wall.PositionZ = WallZ;
        SetScale(wall, 26f, 6f, 0.2f);
        SetColor(wall, 0.30f, 0.31f, 0.34f, 0.0, 0.9);

        // 墙上的高饱和色块：故意与工件同色，用于验证检测器不被背景干扰。
        // 相机在 -Z 侧、墙在 +Z 侧，因此色块要贴在墙的 -Z 面（即 WallZ 减去半厚再加一点），
        // 否则会埋进墙里看不见。
        var decoy1 = sceneService.AddCube("Wall_Decoy_Red");
        decoy1.PositionX = -1.5f;
        decoy1.PositionY = 2.4f;
        decoy1.PositionZ = WallZ - 0.16f;
        SetScale(decoy1, 0.8f, 0.8f, 0.06f);
        SetColor(decoy1, 0.80f, 0.26f, 0.19f, 0.0f, 0.8);

        var decoy2 = sceneService.AddCube("Wall_Decoy_Blue");
        decoy2.PositionX = 1.5f;
        decoy2.PositionY = 2.4f;
        decoy2.PositionZ = WallZ - 0.16f;
        SetScale(decoy2, 0.8f, 0.8f, 0.06f);
        SetColor(decoy2, 0.20f, 0.44f, 0.86f, 0.0f, 0.8);

        // 地面
        var floor = sceneService.AddCube("Factory_Floor");
        floor.PositionY = -0.05f;
        SetScale(floor, 30f, 0.1f, 16f);
        SetColor(floor, 0.24f, 0.25f, 0.27f, 0.0, 0.85);
    }

    // ================= 工件 =================

    private static void BuildWorkpieces(SceneService sceneService)
    {
        _workpieces = new SceneObjectViewModel[Specs.Length];

        for (int i = 0; i < Specs.Length; i++)
        {
            var spec = Specs[i];
            var vm = spec.Create(sceneService);
            _workpieces[i] = vm;

            // 平面尺寸：球与立方同宽，桶身略细
            float planar = spec.HalfHeight >= 0.4f
                ? spec.HalfHeight * 2f          // 球：直径
                : WorkpieceSize;                // 箱/桶：方料边长

            vm.PositionX = StartX[i % StartX.Length];
            vm.PositionY = BeltTop + spec.HalfHeight;
            SetScale(vm, planar, spec.HalfHeight * 2f, planar);
            SetColor(vm, spec.R, spec.G, spec.B, 0.08f, 0.55f);

            if (!spec.IsDefect) continue;

            // 不合格品：顶部加一根醒目的黑色标记柱，紧贴工件顶面
            var marker = sceneService.AddCube(DefectMarkerName);
            marker.PositionX = vm.PositionX;
            marker.PositionY = BeltTop + 2f * spec.HalfHeight + 0.15f;
            SetScale(marker, 0.14f, 0.30f, 0.14f);
            SetColor(marker, 0.05f, 0.05f, 0.05f, 0.0f, 0.85f);
        }
    }

    /// <summary>
    /// 求指定工件规格的顶面高度（工件底面贴合带面时的 Y）。
    /// </summary>
    private static float TopY(in WorkpieceSpec spec) => BeltTop + 2f * spec.HalfHeight;

    // ================= 机位 =================

    /// <summary>
    /// 为虚拟相机设定机位。
    ///
    /// 工业检测的典型机位：略高于工件、斜向下看，检测工位落在画面中部。
    /// 传送带长轴沿 X，因此视线必须沿 Z（yaw=90°），否则正对端头看，
    /// 传送带会被透视压扁成一块板。
    /// </summary>
    public static void FrameCamera(SimpleDrawingService drawing)
    {
        // 机位按 GPU 实测确定的约定给定（MatrixSweepProgram 已验证）：
        //   Camera.Yaw = 视线方向的水平方位角，从 +X 轴起算（0° 看向 +X）
        //   相机落在视线的反方向上，pitch < 0 表示低头俯视
        //
        // 传送带长轴沿 X，因此视线必须沿 Z（yaw=90° => 相机在 -Z 侧）。
        // 距离要足够远以覆盖整条产线（全长 6.8），同时不能太远
        // 否则工件在画面里太小，颜色检测的连通域会低于 MinArea=200。
        drawing.SetCameraPose(
            target: new Vector3D<float>(0f, BeltTop + 0.15f, 0f),
            distance: 6.4f,
            pitchDegrees: -14f,
            yawDegrees: 90f);

        drawing.SetCameraFov(48f);
    }

    // ================= 动画 =================

    /// <summary>
    /// 推进一帧：工件沿传送带流动，到末端后回绕到起点。
    /// 不合格品的标记柱跟随主体移动。
    /// </summary>
    public static void Animate(SceneService sceneService, double deltaSeconds)
    {
        float travel = BeltSpeed * (float)deltaSeconds;

        for (int i = 0; i < _workpieces.Length; i++)
        {
            var vm = _workpieces[i];
            if (vm == null) continue;

            float x = vm.PositionX + travel;
            if (x > BeltHalfLength)
                x -= LoopLength;
            vm.PositionX = x;

            if (Specs[i].IsDefect)
            {
                foreach (var obj in sceneService.SceneObjects)
                {
                    if (obj.Name != DefectMarkerName) continue;
                    obj.PositionX = x;
                    break;
                }
            }
        }
    }

    // ================= 工具 =================

    private static void SetColor(
        SceneObjectViewModel vm, double r, double g, double b,
        double metallic, double roughness)
    {
        SceneObject? obj = vm.SceneObject;
        if (obj == null) return;

        obj.Material ??= new Material();
        obj.Material.Albedo = new Vector4D<float>((float)r, (float)g, (float)b, 1f);
        obj.Material.Metallic = (float)metallic;
        obj.Material.Roughness = (float)roughness;
    }

    private static void SetScale(SceneObjectViewModel vm, float x, float y, float z)
    {
        vm.ScaleX = x;
        vm.ScaleY = y;
        vm.ScaleZ = z;
    }
}