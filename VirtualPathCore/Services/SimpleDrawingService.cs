using System;
using System.Collections.Generic;
using System.IO;
using VirtualPathCore.Contracts.Services;
using VirtualPathCore.Graphics;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Graphics.OpenGL;
using VirtualPathCore.Helpers;
using Silk.NET.Maths;
using Silk.NET.OpenGLES;
using Shader = VirtualPathCore.Graphics.OpenGL.Shader;
using Camera = VirtualPathCore.Graphics.Core.Camera;
using VirtualPathCore.ViewModels;

namespace VirtualPathCore.Services
{
    public class SimpleDrawingService : IDrawingService
    {
        /// <summary>
        /// 图形宿主。抽象成接口而非具体 <see cref="Renderer"/>，使同一套绘制逻辑
        /// 既能跑在 Avalonia 视口里，也能跑在无 UI 进程的离屏宿主里（虚拟相机）。
        /// </summary>
        private IGraphicsHost<GL> renderer = null!;
        private IRenderSurface surface = null!;

        /// <summary>成像表面尺寸。视口宿主取控件尺寸，离屏宿主取 FBO 尺寸。</summary>
        private int SurfaceWidth => surface.SurfaceWidth;

        /// <inheritdoc cref="SurfaceWidth"/>
        private int SurfaceHeight => surface.SurfaceHeight;

        /// <summary>请求重绘。无 UI 宿主据此决定是否渲染下一帧。</summary>
        private void RequestRender() => surface.RequestRender();
        private SceneService sceneService = null!;
        private Camera camera = null!;
        private OrbitCameraController orbitController = null!;

        private RenderPipeline pbrPipeline = null!;
        private RenderPipeline? gridPipeline;
        private RenderPipeline? gizmoPipeline;
        private Mesh? gridMesh;
        private readonly Dictionary<string, int> _attribCache = new();
        private readonly Dictionary<string, int> _gridAttribCache = new();
        private readonly Dictionary<string, int> _gizmoAttribCache = new();

        private bool _isInitialized;
        private bool _isMouseDown;
        private float _lastMouseX, _lastMouseY;
        private bool _gizmoInitialized;
        private Mesh? _gizmoMeshTranslateX;
        private Mesh? _gizmoMeshTranslateY;
        private Mesh? _gizmoMeshTranslateZ;
        private Mesh? _gizmoMeshRotateX;
        private Mesh? _gizmoMeshRotateY;
        private Mesh? _gizmoMeshRotateZ;
        private Mesh? _gizmoMeshScaleX;
        private Mesh? _gizmoMeshScaleY;
        private Mesh? _gizmoMeshScaleZ;

        // Viewport cameras
    private Camera _camTop = null!;
    private Camera _camFront = null!;
    private Camera _camRight = null!;

    // Axis indicator
    private Camera _axisCamera = null!;
    private Mesh? _axisMesh;

        public void Load(object[] args)
        {
            if (args == null || args.Length < 2)
                throw new ArgumentException("Expected args: IGraphicsHost<GL>, SceneService");

            // 接受任意 IGraphicsHost<GL> 实现：Avalonia 的 Renderer，或无 UI 的离屏宿主
            renderer = args[0] as IGraphicsHost<GL>
                ?? throw new ArgumentException("First arg must implement IGraphicsHost<GL>");
            sceneService = args[1] as SceneService ?? throw new ArgumentException("Second arg must be SceneService");
            sceneService.SetHost(renderer);

            // 表面尺寸与重绘请求是宿主职责（IRenderSurface），绘图服务不关心宿主是窗口还是离屏。
            // 两个接口合一传入的宿主（Avalonia Renderer）直接满足；分离时也可显式提供。
            surface = args.Length > 2 && args[2] is IRenderSurface explicitSurface
                ? explicitSurface
                : renderer as IRenderSurface
                  ?? throw new ArgumentException(
                      "Graphics host must also implement IRenderSurface (surface size + RequestRender)");

            camera = new Camera
            {
                Position = new Vector3D<float>(0.0f, 2.0f, 8.0f),
                Fov = 45.0f
            };
            _camTop = new Camera
            {
                Position = new Vector3D<float>(0, 10, 0),
                Fov = 45.0f,
                ProjectionType = ProjectionType.Orthographic,
                OrthoSize = 5.0f
            };
            _camTop.SetRotation(-90, 0);
            _camFront = new Camera
            {
                Position = new Vector3D<float>(0, 0, 10),
                Fov = 45.0f,
                ProjectionType = ProjectionType.Orthographic,
                OrthoSize = 5.0f
            };
            _camFront.SetRotation(0, -90);
            _camRight = new Camera
            {
                Position = new Vector3D<float>(10, 0, 0),
                Fov = 45.0f,
                ProjectionType = ProjectionType.Orthographic,
                OrthoSize = 5.0f
            };
            _camRight.SetRotation(0, -180);

            _axisCamera = new Camera { Fov = 45.0f, OrthoSize = 2.0f, Near = 0.01f, Far = 100.0f };

            orbitController = new OrbitCameraController(camera) { Distance = 8.0f };
            sceneService.Scene.MainCamera = camera;

            sceneService.Scene.PropertyChanged += OnScenePropertyChanged;

            string shaderPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Shaders");
            try
            {
                using Shader vs = new(renderer, ShaderType.VertexShader, File.ReadAllText(Path.Combine(shaderPath, "PBR.vert")));
                using Shader fs = new(renderer, ShaderType.FragmentShader, File.ReadAllText(Path.Combine(shaderPath, "PBR.frag")));
                pbrPipeline = new RenderPipeline(renderer, vs, fs);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load PBR shaders", ex);
            }

            try
            {
                string gridVertPath = Path.Combine(shaderPath, "Grid.vert");
                string gridFragPath = Path.Combine(shaderPath, "Grid.frag");
                if (File.Exists(gridVertPath) && File.Exists(gridFragPath))
                {
                    using Shader gvs = new(renderer, ShaderType.VertexShader, File.ReadAllText(gridVertPath));
                    using Shader gfs = new(renderer, ShaderType.FragmentShader, File.ReadAllText(gridFragPath));
                    gridPipeline = new RenderPipeline(renderer, gvs, gfs);
                    _gridAttribCache["In_Position"] = gridPipeline.GetAttribLocation("In_Position");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SimpleDrawingService] Failed to load grid shaders: {ex.Message}");
            }

            try
            {
                string gVertPath = Path.Combine(shaderPath, "Gizmo.vert");
                string gFragPath = Path.Combine(shaderPath, "Gizmo.frag");
                if (File.Exists(gVertPath) && File.Exists(gFragPath))
                {
                    using Shader gvs = new(renderer, ShaderType.VertexShader, File.ReadAllText(gVertPath));
                    using Shader gfs = new(renderer, ShaderType.FragmentShader, File.ReadAllText(gFragPath));
                    gizmoPipeline = new RenderPipeline(renderer, gvs, gfs);
                    _gizmoAttribCache["In_Position"] = gizmoPipeline.GetAttribLocation("In_Position");
                    _gizmoAttribCache["In_Color"] = gizmoPipeline.GetAttribLocation("In_Color");
                    _gizmoInitialized = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SimpleDrawingService] Failed to load gizmo shaders: {ex.Message}");
            }

            CacheAttribLocations();
            BuildGridMesh();
            BuildGizmoMeshes();
            BuildAxisMesh();
            BuildOriginMarker();

            if (sceneService.SceneObjects.Count == 0)
            {
                var cube = sceneService.AddCube("Cube 1");
                cube.PositionY = 0.5f;
                var sphere = sceneService.AddSphere("Sphere 1");
                sphere.PositionX = -1.8f;
                sphere.PositionY = 0.5f;
                orbitController.Target = new Vector3D<float>(-0.9f, 0.5f, 0.0f);
            }

            _isInitialized = true;
        }

        private Mesh? _originMarkerMesh;

        private void BuildOriginMarker()
        {
            var cube = SceneObjectViewModel.CreateCube("OriginMarker");
            if (cube.SceneObject.MeshBlueprint != null)
            {
                _originMarkerMesh = new Mesh(renderer, cube.SceneObject.MeshBlueprint.Vertices, cube.SceneObject.MeshBlueprint.Indices);
                _originMarkerMesh.SetupAttributes(
                    _attribCache["In_Position"], _attribCache["In_Normal"],
                    _attribCache["In_Tangent"], _attribCache["In_Bitangent"],
                    _attribCache["In_Color"], _attribCache["In_TexCoord"]);
            }
        }

        private void CacheAttribLocations()
        {
            _attribCache["In_Position"] = pbrPipeline.GetAttribLocation("In_Position");
            _attribCache["In_Normal"] = pbrPipeline.GetAttribLocation("In_Normal");
            _attribCache["In_Tangent"] = pbrPipeline.GetAttribLocation("In_Tangent");
            _attribCache["In_Bitangent"] = pbrPipeline.GetAttribLocation("In_Bitangent");
            _attribCache["In_Color"] = pbrPipeline.GetAttribLocation("In_Color");
            _attribCache["In_TexCoord"] = pbrPipeline.GetAttribLocation("In_TexCoord");
        }

        private Mesh CreateSimpleMesh(Vertex[] vertices, uint[] indices, Dictionary<string, int> attribCache)
        {
            var mesh = new Mesh(renderer, vertices, indices);
            mesh.SetupAttributes(
                attribCache.GetValueOrDefault("In_Position", -1),
                -1, -1, -1,
                attribCache.GetValueOrDefault("In_Color", -1),
                -1);
            return mesh;
        }

        private void BuildGridMesh()
        {
            if (gridPipeline == null) return;
            var scene = sceneService.Scene;
            float size = scene.GridSize;
            int divs = scene.GridSubdivisions;
            float half = size / 2f;
            float step = size / divs;

            var verts = new List<Vertex>();
            var idx = new List<uint>();

            for (int i = 0; i <= divs; i++)
            {
                float t = -half + i * step;
                verts.Add(new Vertex(new Vector3D<float>(t, 0, -half)));
                verts.Add(new Vertex(new Vector3D<float>(t, 0, half)));
                verts.Add(new Vertex(new Vector3D<float>(-half, 0, t)));
                verts.Add(new Vertex(new Vector3D<float>(half, 0, t)));
            }

            for (uint j = 0; j < verts.Count; j++)
                idx.Add(j);

            gridMesh = CreateSimpleMesh(verts.ToArray(), idx.ToArray(), _gridAttribCache);
        }

        private void BuildGizmoMeshes()
        {
            if (!_gizmoInitialized) return;

            float len = 0.5f;
            float head = 0.1f;

            var vertsX = new List<Vertex>();
            var idxX = new List<uint>();
            vertsX.Add(new Vertex(Vector3D<float>.Zero, color: new Vector4D<float>(1, 0, 0, 1)));
            vertsX.Add(new Vertex(new Vector3D<float>(len - head, 0, 0), color: new Vector4D<float>(1, 0, 0, 1)));
            vertsX.Add(new Vertex(new Vector3D<float>(len, 0, 0), color: new Vector4D<float>(1, 0, 0, 1)));
            vertsX.Add(new Vertex(new Vector3D<float>(len - head * 2, -head * 0.5f, 0), color: new Vector4D<float>(1, 0, 0, 1)));
            vertsX.Add(new Vertex(new Vector3D<float>(len - head * 2, head * 0.5f, 0), color: new Vector4D<float>(1, 0, 0, 1)));
            for (uint j = 0; j < 5; j++) idxX.Add(j);

            var vertsY = new List<Vertex>();
            var idxY = new List<uint>();
            vertsY.Add(new Vertex(Vector3D<float>.Zero, color: new Vector4D<float>(0, 1, 0, 1)));
            vertsY.Add(new Vertex(new Vector3D<float>(0, len - head, 0), color: new Vector4D<float>(0, 1, 0, 1)));
            vertsY.Add(new Vertex(new Vector3D<float>(0, len, 0), color: new Vector4D<float>(0, 1, 0, 1)));
            vertsY.Add(new Vertex(new Vector3D<float>(-head * 0.5f, len - head * 2, 0), color: new Vector4D<float>(0, 1, 0, 1)));
            vertsY.Add(new Vertex(new Vector3D<float>(head * 0.5f, len - head * 2, 0), color: new Vector4D<float>(0, 1, 0, 1)));
            for (uint j = 0; j < 5; j++) idxY.Add(j);

            var vertsZ = new List<Vertex>();
            var idxZ = new List<uint>();
            vertsZ.Add(new Vertex(Vector3D<float>.Zero, color: new Vector4D<float>(0, 0, 1, 1)));
            vertsZ.Add(new Vertex(new Vector3D<float>(0, 0, len - head), color: new Vector4D<float>(0, 0, 1, 1)));
            vertsZ.Add(new Vertex(new Vector3D<float>(0, 0, len), color: new Vector4D<float>(0, 0, 1, 1)));
            vertsZ.Add(new Vertex(new Vector3D<float>(0, -head * 0.5f, len - head * 2), color: new Vector4D<float>(0, 0, 1, 1)));
            vertsZ.Add(new Vertex(new Vector3D<float>(0, head * 0.5f, len - head * 2), color: new Vector4D<float>(0, 0, 1, 1)));
            for (uint j = 0; j < 5; j++) idxZ.Add(j);

            _gizmoMeshTranslateX = CreateSimpleMesh(vertsX.ToArray(), idxX.ToArray(), _gizmoAttribCache);
            _gizmoMeshTranslateY = CreateSimpleMesh(vertsY.ToArray(), idxY.ToArray(), _gizmoAttribCache);
            _gizmoMeshTranslateZ = CreateSimpleMesh(vertsZ.ToArray(), idxZ.ToArray(), _gizmoAttribCache);

            // Rotation gizmos (circles)
            float radius = 0.6f;
            int segments = 32;

            var vertsRotX = new List<Vertex>();
            var idxRotX = new List<uint>();
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * 2 * MathF.PI / segments;
                float y = radius * MathF.Cos(angle);
                float z = radius * MathF.Sin(angle);
                vertsRotX.Add(new Vertex(new Vector3D<float>(0, y, z), color: new Vector4D<float>(1, 0, 0, 1)));
            }
            for (uint j = 0; j < (uint)vertsRotX.Count; j++) idxRotX.Add(j);

            var vertsRotY = new List<Vertex>();
            var idxRotY = new List<uint>();
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * 2 * MathF.PI / segments;
                float x = radius * MathF.Cos(angle);
                float z = radius * MathF.Sin(angle);
                vertsRotY.Add(new Vertex(new Vector3D<float>(x, 0, z), color: new Vector4D<float>(0, 1, 0, 1)));
            }
            for (uint j = 0; j < (uint)vertsRotY.Count; j++) idxRotY.Add(j);

            var vertsRotZ = new List<Vertex>();
            var idxRotZ = new List<uint>();
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * 2 * MathF.PI / segments;
                float x = radius * MathF.Cos(angle);
                float y = radius * MathF.Sin(angle);
                vertsRotZ.Add(new Vertex(new Vector3D<float>(x, y, 0), color: new Vector4D<float>(0, 0, 1, 1)));
            }
            for (uint j = 0; j < (uint)vertsRotZ.Count; j++) idxRotZ.Add(j);

            _gizmoMeshRotateX = CreateSimpleMesh(vertsRotX.ToArray(), idxRotX.ToArray(), _gizmoAttribCache);
            _gizmoMeshRotateY = CreateSimpleMesh(vertsRotY.ToArray(), idxRotY.ToArray(), _gizmoAttribCache);
            _gizmoMeshRotateZ = CreateSimpleMesh(vertsRotZ.ToArray(), idxRotZ.ToArray(), _gizmoAttribCache);

            // Scale gizmos (lines with cubes at ends)
            float scaleLen = 0.5f;
            float cubeSize = 0.05f;

            var vertsScaleX = new List<Vertex>();
            var idxScaleX = new List<uint>();
            vertsScaleX.Add(new Vertex(Vector3D<float>.Zero, color: new Vector4D<float>(1, 0, 0, 1)));
            vertsScaleX.Add(new Vertex(new Vector3D<float>(scaleLen, 0, 0), color: new Vector4D<float>(1, 0, 0, 1)));
            vertsScaleX.Add(new Vertex(new Vector3D<float>(scaleLen - cubeSize, -cubeSize, -cubeSize), color: new Vector4D<float>(1, 0, 0, 1)));
            vertsScaleX.Add(new Vertex(new Vector3D<float>(scaleLen, cubeSize, -cubeSize), color: new Vector4D<float>(1, 0, 0, 1)));
            vertsScaleX.Add(new Vertex(new Vector3D<float>(scaleLen, cubeSize, cubeSize), color: new Vector4D<float>(1, 0, 0, 1)));
            vertsScaleX.Add(new Vertex(new Vector3D<float>(scaleLen - cubeSize, -cubeSize, cubeSize), color: new Vector4D<float>(1, 0, 0, 1)));
            vertsScaleX.Add(new Vertex(new Vector3D<float>(scaleLen, -cubeSize, -cubeSize), color: new Vector4D<float>(1, 0, 0, 1)));
            vertsScaleX.Add(new Vertex(new Vector3D<float>(scaleLen, -cubeSize, cubeSize), color: new Vector4D<float>(1, 0, 0, 1)));
            for (uint j = 0; j < (uint)vertsScaleX.Count; j++) idxScaleX.Add(j);

            var vertsScaleY = new List<Vertex>();
            var idxScaleY = new List<uint>();
            vertsScaleY.Add(new Vertex(Vector3D<float>.Zero, color: new Vector4D<float>(0, 1, 0, 1)));
            vertsScaleY.Add(new Vertex(new Vector3D<float>(0, scaleLen, 0), color: new Vector4D<float>(0, 1, 0, 1)));
            vertsScaleY.Add(new Vertex(new Vector3D<float>(-cubeSize, scaleLen - cubeSize, -cubeSize), color: new Vector4D<float>(0, 1, 0, 1)));
            vertsScaleY.Add(new Vertex(new Vector3D<float>(cubeSize, scaleLen, -cubeSize), color: new Vector4D<float>(0, 1, 0, 1)));
            vertsScaleY.Add(new Vertex(new Vector3D<float>(cubeSize, scaleLen, cubeSize), color: new Vector4D<float>(0, 1, 0, 1)));
            vertsScaleY.Add(new Vertex(new Vector3D<float>(-cubeSize, scaleLen - cubeSize, cubeSize), color: new Vector4D<float>(0, 1, 0, 1)));
            vertsScaleY.Add(new Vertex(new Vector3D<float>(-cubeSize, scaleLen, -cubeSize), color: new Vector4D<float>(0, 1, 0, 1)));
            vertsScaleY.Add(new Vertex(new Vector3D<float>(-cubeSize, scaleLen, cubeSize), color: new Vector4D<float>(0, 1, 0, 1)));
            for (uint j = 0; j < (uint)vertsScaleY.Count; j++) idxScaleY.Add(j);

            var vertsScaleZ = new List<Vertex>();
            var idxScaleZ = new List<uint>();
            vertsScaleZ.Add(new Vertex(Vector3D<float>.Zero, color: new Vector4D<float>(0, 0, 1, 1)));
            vertsScaleZ.Add(new Vertex(new Vector3D<float>(0, 0, scaleLen), color: new Vector4D<float>(0, 0, 1, 1)));
            vertsScaleZ.Add(new Vertex(new Vector3D<float>(-cubeSize, -cubeSize, scaleLen - cubeSize), color: new Vector4D<float>(0, 0, 1, 1)));
            vertsScaleZ.Add(new Vertex(new Vector3D<float>(cubeSize, -cubeSize, scaleLen), color: new Vector4D<float>(0, 0, 1, 1)));
            vertsScaleZ.Add(new Vertex(new Vector3D<float>(cubeSize, cubeSize, scaleLen), color: new Vector4D<float>(0, 0, 1, 1)));
            vertsScaleZ.Add(new Vertex(new Vector3D<float>(-cubeSize, cubeSize, scaleLen - cubeSize), color: new Vector4D<float>(0, 0, 1, 1)));
            vertsScaleZ.Add(new Vertex(new Vector3D<float>(-cubeSize, -cubeSize, scaleLen), color: new Vector4D<float>(0, 0, 1, 1)));
            vertsScaleZ.Add(new Vertex(new Vector3D<float>(-cubeSize, cubeSize, scaleLen), color: new Vector4D<float>(0, 0, 1, 1)));
            for (uint j = 0; j < (uint)vertsScaleZ.Count; j++) idxScaleZ.Add(j);

            _gizmoMeshScaleX = CreateSimpleMesh(vertsScaleX.ToArray(), idxScaleX.ToArray(), _gizmoAttribCache);
            _gizmoMeshScaleY = CreateSimpleMesh(vertsScaleY.ToArray(), idxScaleY.ToArray(), _gizmoAttribCache);
            _gizmoMeshScaleZ = CreateSimpleMesh(vertsScaleZ.ToArray(), idxScaleZ.ToArray(), _gizmoAttribCache);
        }

        private void BuildAxisMesh()
        {
            if (!_gizmoInitialized) return;

            float len = 0.8f;
            var verts = new List<Vertex>();
            var idx = new List<uint>();

            verts.Add(new Vertex(Vector3D<float>.Zero, color: new Vector4D<float>(1, 1, 1, 1)));
            verts.Add(new Vertex(new Vector3D<float>(len, 0, 0), color: new Vector4D<float>(1, 1, 1, 1)));
            verts.Add(new Vertex(Vector3D<float>.Zero, color: new Vector4D<float>(1, 1, 1, 1)));
            verts.Add(new Vertex(new Vector3D<float>(0, len, 0), color: new Vector4D<float>(1, 1, 1, 1)));
            verts.Add(new Vertex(Vector3D<float>.Zero, color: new Vector4D<float>(1, 1, 1, 1)));
            verts.Add(new Vertex(new Vector3D<float>(0, 0, len), color: new Vector4D<float>(1, 1, 1, 1)));

            for (uint j = 0; j < (uint)verts.Count; j++)
                idx.Add(j);

            _axisMesh = CreateSimpleMesh(verts.ToArray(), idx.ToArray(), _gizmoAttribCache);
        }

        private int ActiveExtraCount()
        {
            var s = sceneService.Scene;
            return (s.ShowTopView ? 1 : 0) + (s.ShowFrontView ? 1 : 0) + (s.ShowRightView ? 1 : 0);
        }

        private int TotalViewCount() => 1 + ActiveExtraCount();

        /// <summary>
        /// 设置主相机的观察目标与环绕参数。
        ///
        /// 编辑器里用户用鼠标拖拽改变视角，走的是 <see cref="Orbit"/> / <see cref="Pan"/> / <see cref="Zoom"/>；
        /// 而无 UI 的宿主（如离屏"虚拟相机"）没有鼠标，需要能直接指定机位。
        ///
        /// 角度约定：<paramref name="yawDegrees"/> / <paramref name="pitchDegrees"/> 描述的是
        /// <b>视线方向</b>（即 <see cref="Camera.Yaw"/> / <see cref="Camera.Pitch"/> 的定义），
        /// 因此直接写入相机即可被每帧的 <c>orbitController.Update()</c> 正确消费 ——
        /// 不要在这里调用 <c>Camera.LookAt</c>，那会再次改写 Yaw 并与控制器互相覆盖。
        /// </summary>
        /// <param name="target">观察目标点（轨道中心）。</param>
        /// <param name="distance">相机到目标点的距离。</param>
        /// <param name="pitchDegrees">视线俯仰角（度），正值抬头、负值俯视。</param>
        /// <param name="yawDegrees">
        /// 视线方位角（度），从 +X 轴起算：0° 表示看向 +X，90° 表示看向 +Z。
        /// 相机位于视线的反方向上。
        /// </param>
        public void SetCameraPose(Vector3D<float> target, float distance, float pitchDegrees, float yawDegrees)
        {
            if (camera == null)
                return;

            orbitController!.Target = target;
            orbitController.Distance = distance;

            camera.Pitch = pitchDegrees;
            camera.Yaw = yawDegrees;

            // 立即应用一次，避免要等下一帧 Update 才生效
            orbitController.Update(0.0);
            RequestRender();
        }

        /// <summary>设置主相机的垂直视场角（度）。</summary>
        public void SetCameraFov(float fovDegrees)
        {
            if (camera == null)
                return;

            camera.Fov = fovDegrees;
            RequestRender();
        }

        /// <summary>当前主相机位置（供宿主读取或做投影计算）。</summary>
        public Vector3D<float> CameraPosition => camera?.Position ?? Vector3D<float>.Zero;

        public void Update(double deltaSeconds)
        {
            if (!_isInitialized) return;

            int w = SurfaceWidth;
            int h = SurfaceHeight;

            int total = TotalViewCount();
            int halfW = w / 2;
            int halfH = h / 2;

            camera.Width = total == 4 ? halfW : (total >= 2 ? halfW : w);
            camera.Height = total == 4 ? halfH : h;
            _camTop.Width = total >= 2 ? halfW : w;
            _camTop.Height = total >= 3 ? halfH : h;
            _camFront.Width = total >= 3 ? halfW : (total == 2 ? halfW : w);
            _camFront.Height = total == 4 ? halfH : (total == 3 ? halfH : h);
            _camRight.Width = total >= 2 ? halfW : w;
            _camRight.Height = total == 4 ? halfH : (total == 3 ? halfH : h);

            orbitController.Update(deltaSeconds);
            sceneService.Scene.Update(deltaSeconds);
            sceneService.Animation?.Update(deltaSeconds);
        }

        public void Render(double deltaSeconds)
        {
            if (!_isInitialized) return;

            GL gl = renderer.GetContext();
            var sceneObjects = sceneService.Scene.Objects;

            foreach (var obj in sceneObjects)
            {
                if (obj.Mesh == null && obj.MeshBlueprint != null)
                {
                    var mesh = new Mesh(renderer, obj.MeshBlueprint.Vertices, obj.MeshBlueprint.Indices);
                    mesh.SetupAttributes(
                        _attribCache["In_Position"], _attribCache["In_Normal"],
                        _attribCache["In_Tangent"], _attribCache["In_Bitangent"],
                        _attribCache["In_Color"], _attribCache["In_TexCoord"]);
                    obj.Mesh = mesh;
                    obj.MeshBlueprint = null;
                }
            }

            int w = SurfaceWidth;
            int h = SurfaceHeight;
            int total = TotalViewCount();

            if (total == 1)
            {
                RenderScene(0, 0, w, h, camera, sceneObjects, gl);
                if (gizmoPipeline != null && _axisMesh != null)
                    RenderAxisIndicator(gl, 0, 0);
                return;
            }

            int halfW = w / 2;
            int halfH = h / 2;

            if (total == 2)
            {
                var s = sceneService.Scene;
                Camera extraCam = s.ShowTopView ? _camTop : s.ShowFrontView ? _camFront : _camRight;
                RenderScene(0, 0, halfW, h, camera, sceneObjects, gl);
                if (gizmoPipeline != null && _axisMesh != null)
                    RenderAxisIndicator(gl, 0, 0);
                RenderScene(halfW, 0, halfW, h, extraCam, sceneObjects, gl);
            }
            else if (total == 3)
            {
                var s = sceneService.Scene;
                // Perspective on left, remaining two stacked on right
                var extras = new List<Camera>();
                if (s.ShowTopView) extras.Add(_camTop);
                if (s.ShowFrontView) extras.Add(_camFront);
                if (s.ShowRightView) extras.Add(_camRight);
                RenderScene(0, 0, halfW, h, camera, sceneObjects, gl);
                if (gizmoPipeline != null && _axisMesh != null)
                    RenderAxisIndicator(gl, 0, 0);
                RenderScene(halfW, halfH, halfW, halfH, extras[0], sceneObjects, gl);
                RenderScene(halfW, 0, halfW, halfH, extras[1], sceneObjects, gl);
            }
            else // total == 4
            {
                RenderScene(0, halfH, halfW, halfH, camera, sceneObjects, gl);
                if (gizmoPipeline != null && _axisMesh != null)
                    RenderAxisIndicator(gl, 0, halfH);
                RenderScene(halfW, halfH, halfW, halfH, _camTop, sceneObjects, gl);
                RenderScene(0, 0, halfW, halfH, _camFront, sceneObjects, gl);
                RenderScene(halfW, 0, halfW, halfH, _camRight, sceneObjects, gl);
            }
        }

        private void OnScenePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Scene.ShowTopView) ||
                e.PropertyName == nameof(Scene.ShowFrontView) ||
                e.PropertyName == nameof(Scene.ShowRightView) ||
                e.PropertyName == nameof(Scene.ShowGrid))
            {
                RequestRender();
            }
        }

        private void ClearViewport(int x, int y, int width, int height, GL gl)
        {
            gl.Viewport(x, y, (uint)width, (uint)height);
            gl.Scissor(x, y, (uint)width, (uint)height);
            gl.Enable(GLEnum.ScissorTest);
            gl.ClearColor(0.08f, 0.08f, 0.1f, 1.0f);
            gl.Clear((uint)GLEnum.ColorBufferBit | (uint)GLEnum.DepthBufferBit);
            gl.Disable(GLEnum.ScissorTest);
        }

        private void RenderAxisIndicator(GL gl, int vpOriginX, int vpOriginY)
        {
            int size = 100;
            int margin = 12;

            int ax = vpOriginX + margin;
            int ay = vpOriginY + margin;

            gl.Viewport(ax, ay, (uint)size, (uint)size);
            gl.Scissor(ax, ay, (uint)size, (uint)size);
            gl.Enable(GLEnum.ScissorTest);
            gl.Clear((uint)GLEnum.DepthBufferBit);

            Vector3D<float> dir = Vector3D.Normalize(camera.Position - orbitController.Target);
            _axisCamera.SetPosition(dir.X * 3, dir.Y * 3, dir.Z * 3);
            _axisCamera.LookAt(Vector3D<float>.Zero);
            _axisCamera.Width = size;
            _axisCamera.Height = size;

            float s = 0.8f;
            gizmoPipeline!.Bind();
            gizmoPipeline.SetUniform("View", _axisCamera.View);
            gizmoPipeline.SetUniform("Projection", _axisCamera.Projection);

            GLEnum lineMode = GLEnum.Lines;
            gizmoPipeline.SetUniform("Model", Matrix4X4.CreateScale(s, s, s));
            gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(1, 0, 0, 1));
            _axisMesh!.Draw(lineMode);

            gizmoPipeline.SetUniform("Model", Matrix4X4.CreateRotationZ(MathF.PI / 2) * Matrix4X4.CreateScale(s, s, s));
            gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(0, 1, 0, 1));
            _axisMesh!.Draw(lineMode);

            gizmoPipeline.SetUniform("Model", Matrix4X4.CreateRotationY(-MathF.PI / 2) * Matrix4X4.CreateScale(s, s, s));
            gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(0, 0, 1, 1));
            _axisMesh!.Draw(lineMode);

            gizmoPipeline.Unbind();
            gl.Disable(GLEnum.ScissorTest);
        }

        private void RenderScene(int x, int y, int width, int height,
            Camera cam, IReadOnlyList<SceneObject> sceneObjects, GL gl)
        {
            gl.Viewport(x, y, (uint)width, (uint)height);
            gl.Scissor(x, y, (uint)width, (uint)height);
            gl.Enable(GLEnum.ScissorTest);

            gl.ClearColor(0.15f, 0.15f, 0.17f, 1.0f);
            gl.Clear((uint)GLEnum.ColorBufferBit | (uint)GLEnum.DepthBufferBit);

            var scene = sceneService.Scene;

            if (scene.ShowGrid && gridPipeline != null && gridMesh != null)
            {
                gl.Enable(GLEnum.Blend);
                gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
                gl.Disable(GLEnum.CullFace);

                gridPipeline.Bind();
                gridPipeline.SetUniform("View", cam.View);
                gridPipeline.SetUniform("Projection", cam.Projection);
                gridPipeline.SetUniform("GridColor", scene.GridColor);
                gridPipeline.SetUniform("CameraPos", cam.Position);
                // GL_LINES: BuildGridMesh emits vertices in pairs, one segment per pair.
                // Mesh.Draw defaults to Triangles, which turns the whole grid into
                // degenerate slivers spanning unrelated vertices -- that is the
                // ragged grey blob the floor used to render as.
                gridMesh.Draw(GLEnum.Lines);
                gridPipeline.Unbind();

                gl.Disable(GLEnum.Blend);
            }

            // Reference marker at origin
            if (_originMarkerMesh != null)
            {
                var originMat = Matrix4X4.CreateScale(0.05f, 0.05f, 0.05f);
                pbrPipeline.Bind();
                pbrPipeline.SetUniform("Model", originMat);
                pbrPipeline.SetUniform("ObjectToClip", originMat * cam.View * cam.Projection);
                pbrPipeline.SetUniform("Albedo", new Vector4D<float>(0.3f, 0.3f, 0.3f, 1.0f));
                _originMarkerMesh.Draw();
                pbrPipeline.Unbind();
            }

            // 背面剔除：MeshFactory 生成的三角形绕序是朝外的，可以正常剔除背面。
            //
            // 实测依据（MinimalRenderProgram，清屏后逐像素比对，并读回 GL 状态）：
            //   CullFace 关闭   -> 覆盖 10.29%，覆盖区均值 rgb=( 92.0, 20.0, 20.0)
            //   CullFace(Back)  -> 覆盖 10.29%，均值 rgb=( 92.0, 20.0, 20.0)  与关闭时完全一致
            //   CullFace(Front) -> 覆盖 10.29%，均值 rgb=(201.9, 48.1, 48.1)  换成了远壳
            // 覆盖率三者相同是因为立方体是凸体，近壳与远壳轮廓几乎重合；
            // 真正的判据是"剔除背面与关闭剔除逐像素相同"，即近壳本就是正面，
            // 没有可见面被丢掉。GL 状态读回确认 CullFaceMode 确为 GL_BACK(1029)。
            //
            // 注：曾一度改成关闭剔除 + 在 PBR.frag 里用 gl_FrontFacing 翻转背面法线。
            // 那是在调试期临时给 Camera.View 加了 Transpose 的状态下测得的（cull_back
            // 覆盖 0%），当时误判成"绕序朝内"。View 转置回退、PBR.vert 法线恢复后
            // 重新实测，绕序本身一直是正确的，故保留背面剔除、也不用 gl_FrontFacing。
            gl.Enable(GLEnum.CullFace);
            gl.CullFace(GLEnum.Back);

            pbrPipeline.Bind();

            var lightVec = Vector3D.Normalize(new Vector3D<float>(0.5f, -0.8f, 0.6f));
            pbrPipeline.SetUniform("Light0Dir", lightVec);
            pbrPipeline.SetUniform("Light0Color", new Vector3D<float>(1.0f, 0.95f, 0.9f));
            pbrPipeline.SetUniform("Light0Intensity", 1.2f);
            pbrPipeline.SetUniform("CameraPos", cam.Position);
            pbrPipeline.SetUniform("AmbientIntensity", 0.3f);

            foreach (var obj in sceneObjects)
            {
                if (!obj.Active || obj.Mesh == null) continue;

                Matrix4X4<float> m = obj.Transform.WorldMatrix;

                // 合成顺序：model * view * projection。
                //
                // 由 MatrixSweepProgram 在 GPU 上穷举 8 种组合
                // （transpose × View 是否转置 × 四种乘法顺序）后确定：
                // 本组合下各向同性的球在屏幕上宽高比 = 1.04（正圆），
                // 其余组合均为 0.01~1.01 的压扁或完全不可见。
                Matrix4X4<float> objectToClip = m * cam.View * cam.Projection;

                // 法线矩阵。
                //
                // PBR.vert 里写的是 transpose(mat3(WorldToObject)) * In_Normal，
                // 即"对物体->世界矩阵取逆转置"。而这里的矩阵是行向量布局，
                // Matrix4X4.Invert() 返回的已经是行向量约定下的逆矩阵，
                // 再交给着色器转置会得到错误的法线方向（表现为所有面光照一致、
                // 完全没有明暗层次）。
                //
                // 因此这里直接给出法线矩阵本身，并在着色器侧去掉 transpose。
                Matrix4X4<float> normalMatrix = m.Invert();

                pbrPipeline.SetUniform("Model", m);
                pbrPipeline.SetUniform("View", cam.View);
                pbrPipeline.SetUniform("Projection", cam.Projection);
                pbrPipeline.SetUniform("ObjectToWorld", m);
                pbrPipeline.SetUniform("ObjectToClip", objectToClip);
                pbrPipeline.SetUniform("WorldToObject", normalMatrix);

                Vector4D<float> albedo = obj.Material?.Albedo ?? new Vector4D<float>(1.0f, 0.5f, 0.2f, 1.0f);
                float metallic = obj.Material?.Metallic ?? 0.1f;
                float roughness = obj.Material?.Roughness ?? 0.5f;

                pbrPipeline.SetUniform("Albedo", albedo);
                pbrPipeline.SetUniform("Metallic", metallic);
                pbrPipeline.SetUniform("Roughness", roughness);

                obj.Mesh.Draw();
            }

            pbrPipeline.Unbind();
            gl.Disable(GLEnum.CullFace);

            if (gizmoPipeline != null && _gizmoInitialized && sceneService.SelectedObject != null)
            {
                RenderGizmoForCamera(cam, gl);
            }

            gl.Disable(GLEnum.ScissorTest);
        }

        private void RenderGizmoForCamera(Camera cam, GL gl)
        {
            if (!_gizmoInitialized || sceneService.SelectedObject == null) return;

            var obj = sceneService.SelectedObject.SceneObject;
            Vector3D<float> pos = obj.Transform.Position;

            float dist = Vector3D.Distance(cam.Position, pos);
            float scale = dist * 0.15f;

            gizmoPipeline!.Bind();
            gizmoPipeline.SetUniform("View", cam.View);
            gizmoPipeline.SetUniform("Projection", cam.Projection);

            var gizmoMode = sceneService.GizmoMode;

            if (gizmoMode == GizmoMode.Translate)
            {
                if (_gizmoMeshTranslateX != null)
                {
                    Matrix4X4<float> mX = Matrix4X4.CreateScale(scale, scale, scale) * Matrix4X4.CreateTranslation(pos);
                    gizmoPipeline.SetUniform("Model", mX);
                    gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(1, 0, 0, 1));
                    // GL_LINES: the translate arrows are segment pairs, not triangles.
                _gizmoMeshTranslateX.Draw(GLEnum.Lines);
                }

                if (_gizmoMeshTranslateY != null)
                {
                    Matrix4X4<float> mY = Matrix4X4.CreateScale(scale, scale, scale) * Matrix4X4.CreateTranslation(pos);
                    gizmoPipeline.SetUniform("Model", mY);
                    gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(0, 1, 0, 1));
                    _gizmoMeshTranslateY.Draw(GLEnum.Lines);
                }

                if (_gizmoMeshTranslateZ != null)
                {
                    Matrix4X4<float> mZ = Matrix4X4.CreateScale(scale, scale, scale) * Matrix4X4.CreateTranslation(pos);
                    gizmoPipeline.SetUniform("Model", mZ);
                    gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(0, 0, 1, 1));
                    _gizmoMeshTranslateZ.Draw(GLEnum.Lines);
                }
            }
            else if (gizmoMode == GizmoMode.Rotate)
            {
                if (_gizmoMeshRotateX != null)
                {
                    Matrix4X4<float> mX = Matrix4X4.CreateScale(scale, scale, scale) * Matrix4X4.CreateTranslation(pos);
                    gizmoPipeline.SetUniform("Model", mX);
                    gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(1, 0, 0, 1));
                    _gizmoMeshRotateX.Draw(GLEnum.LineLoop);
                }

                if (_gizmoMeshRotateY != null)
                {
                    Matrix4X4<float> mY = Matrix4X4.CreateScale(scale, scale, scale) * Matrix4X4.CreateTranslation(pos);
                    gizmoPipeline.SetUniform("Model", mY);
                    gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(0, 1, 0, 1));
                    _gizmoMeshRotateY.Draw(GLEnum.LineLoop);
                }

                if (_gizmoMeshRotateZ != null)
                {
                    Matrix4X4<float> mZ = Matrix4X4.CreateScale(scale, scale, scale) * Matrix4X4.CreateTranslation(pos);
                    gizmoPipeline.SetUniform("Model", mZ);
                    gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(0, 0, 1, 1));
                    _gizmoMeshRotateZ.Draw(GLEnum.LineLoop);
                }
            }
            else if (gizmoMode == GizmoMode.Scale)
            {
                if (_gizmoMeshScaleX != null)
                {
                    Matrix4X4<float> mX = Matrix4X4.CreateScale(scale, scale, scale) * Matrix4X4.CreateTranslation(pos);
                    gizmoPipeline.SetUniform("Model", mX);
                    gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(1, 0, 0, 1));
                    // GL_LINES: the scale handles are a shaft segment plus a wireframe cube
                // whose corners are stored in edge order.
                _gizmoMeshScaleX.Draw(GLEnum.Lines);
                }

                if (_gizmoMeshScaleY != null)
                {
                    Matrix4X4<float> mY = Matrix4X4.CreateScale(scale, scale, scale) * Matrix4X4.CreateTranslation(pos);
                    gizmoPipeline.SetUniform("Model", mY);
                    gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(0, 1, 0, 1));
                    _gizmoMeshScaleY.Draw(GLEnum.Lines);
                }

                if (_gizmoMeshScaleZ != null)
                {
                    Matrix4X4<float> mZ = Matrix4X4.CreateScale(scale, scale, scale) * Matrix4X4.CreateTranslation(pos);
                    gizmoPipeline.SetUniform("Model", mZ);
                    gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(0, 0, 1, 1));
                    _gizmoMeshScaleZ.Draw(GLEnum.Lines);
                }
            }

            gizmoPipeline.Unbind();
        }

        public void OnMouseDown(float x, float y)
        {
            _isMouseDown = true;
            _lastMouseX = x;
            _lastMouseY = y;
            RequestRender();
        }

        public void OnMouseUp()
        {
            _isMouseDown = false;
        }

        private bool _isRightMouseDown = false;

        public void OnMouseMove(float x, float y)
        {
            if (!_isMouseDown) return;

            // Simple heuristic: if right button is held, pan; otherwise orbit.
            // In a real app we'd track actual button state from event args.
            if (_isRightMouseDown)
            {
                orbitController.Pan(x - _lastMouseX, y - _lastMouseY);
            }
            else
            {
                orbitController.Orbit(x - _lastMouseX, y - _lastMouseY);
            }

            _lastMouseX = x;
            _lastMouseY = y;
            RequestRender();
        }

        public void OnRightMouseDown(float x, float y)
        {
            _isRightMouseDown = true;
            OnMouseDown(x, y);
        }

        public void OnRightMouseUp()
        {
            _isRightMouseDown = false;
            OnMouseUp();
        }

        public void OnScroll(float delta)
        {
            orbitController.Zoom(delta * 2);
            RequestRender();
        }

        public void Orbit(float dx, float dy)
        {
            orbitController.Orbit(dx, dy);
            RequestRender();
        }

        public void Pan(float dx, float dy)
        {
            orbitController.Pan(dx, dy);
            RequestRender();
        }

        public void Zoom(float delta)
        {
            orbitController.Zoom(delta * 2);
            RequestRender();
        }
    }
}
