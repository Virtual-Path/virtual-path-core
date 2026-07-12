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

namespace VirtualPathCore.Services
{
    public class SimpleDrawingService : IDrawingService
    {
        private Renderer renderer = null!;
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
                throw new ArgumentException("Expected args: Renderer, SceneService");

            renderer = args[0] as Renderer ?? throw new ArgumentException("First arg must be Renderer");
            sceneService = args[1] as SceneService ?? throw new ArgumentException("Second arg must be SceneService");
            sceneService.SetHost(renderer);

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
            catch
            {
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
            catch
            {
            }

            CacheAttribLocations();
            BuildGridMesh();
            BuildGizmoMeshes();
            BuildAxisMesh();

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

        public void Update(double deltaSeconds)
        {
            if (!_isInitialized) return;

            int w = renderer.PixelWidth;
            int h = renderer.PixelHeight;

            int total = TotalViewCount();
            int halfW = w / 2;
            int halfH = h / 2;

            camera.Width = total == 4 ? halfW : (total >= 2 ? halfW : w);
            camera.Height = total == 4 ? halfH : h;
            _camTop.Width = total >= 2 ? halfW : w;
            _camTop.Height = total == 4 ? halfH : h;
            _camFront.Width = total >= 3 ? halfW : w;
            _camFront.Height = total == 4 ? halfH : h;
            _camRight.Width = halfW;
            _camRight.Height = halfH;

            orbitController.Update(deltaSeconds);
            sceneService.Scene.Update(deltaSeconds);
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

            int w = renderer.PixelWidth;
            int h = renderer.PixelHeight;
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
                renderer.RequestRender();
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
                gridMesh.Draw();
                gridPipeline.Unbind();

                gl.Disable(GLEnum.Blend);
            }

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
                Matrix4X4<float> objectToClip = m * cam.View * cam.Projection;
                Matrix4X4<float> worldToObject = m.Invert();

                pbrPipeline.SetUniform("Model", m);
                pbrPipeline.SetUniform("View", cam.View);
                pbrPipeline.SetUniform("Projection", cam.Projection);
                pbrPipeline.SetUniform("ObjectToWorld", m);
                pbrPipeline.SetUniform("ObjectToClip", objectToClip);
                pbrPipeline.SetUniform("WorldToObject", worldToObject);

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

            if (_gizmoMeshTranslateX != null)
            {
                Matrix4X4<float> mX = Matrix4X4.CreateScale(scale, scale, scale) * Matrix4X4.CreateTranslation(pos);
                gizmoPipeline.SetUniform("Model", mX);
                gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(1, 0, 0, 1));
                _gizmoMeshTranslateX.Draw();
            }

            if (_gizmoMeshTranslateY != null)
            {
                Matrix4X4<float> mY = Matrix4X4.CreateScale(scale, scale, scale) * Matrix4X4.CreateTranslation(pos);
                gizmoPipeline.SetUniform("Model", mY);
                gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(0, 1, 0, 1));
                _gizmoMeshTranslateY.Draw();
            }

            if (_gizmoMeshTranslateZ != null)
            {
                Matrix4X4<float> mZ = Matrix4X4.CreateScale(scale, scale, scale) * Matrix4X4.CreateTranslation(pos);
                gizmoPipeline.SetUniform("Model", mZ);
                gizmoPipeline.SetUniform("GizmoColor", new Vector4D<float>(0, 0, 1, 1));
                _gizmoMeshTranslateZ.Draw();
            }

            gizmoPipeline.Unbind();
        }

        public void OnMouseDown(float x, float y)
        {
            _isMouseDown = true;
            _lastMouseX = x;
            _lastMouseY = y;
            renderer.RequestRender();
        }

        public void OnMouseUp()
        {
            _isMouseDown = false;
        }

        public void OnMouseMove(float x, float y)
        {
            if (!_isMouseDown) return;
            orbitController.Orbit(x - _lastMouseX, y - _lastMouseY);
            _lastMouseX = x;
            _lastMouseY = y;
            renderer.RequestRender();
        }

        public void OnScroll(float delta)
        {
            orbitController.Zoom(delta * 2);
            renderer.RequestRender();
        }
    }
}
