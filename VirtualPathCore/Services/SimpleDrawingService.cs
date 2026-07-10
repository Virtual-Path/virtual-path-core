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
        private readonly Dictionary<string, int> _attribCache = new();

        private const float LightDirX = 0.5f;
        private const float LightDirY = -0.8f;
        private const float LightDirZ = 0.6f;

        private bool _isInitialized;
        private bool _isMouseDown;
        private float _lastMouseX, _lastMouseY;

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
            orbitController = new OrbitCameraController(camera) { Distance = 8.0f };
            sceneService.Scene.MainCamera = camera;

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

            CacheAttribLocations();

            if (sceneService.SceneObjects.Count == 0)
            {
                var cube = sceneService.AddCube("Cube 1");
                cube.PositionY = 0.5f;
                var sphere = sceneService.AddSphere("Sphere 1");
                sphere.PositionX = -1.8f;
                sphere.PositionY = 0.5f;
                orbitController.Target = new Vector3D<float>(-0.9f, 0.5f, 0.0f);
            }

            renderer.Samples = Math.Max(renderer.Samples, 4);
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

        public void Update(double deltaSeconds)
        {
            if (!_isInitialized) return;

            camera.Width = renderer.PixelWidth;
            camera.Height = renderer.PixelHeight;
            orbitController.Update(deltaSeconds);
            sceneService.Scene.Update(deltaSeconds);
        }

        public void Render(double deltaSeconds)
        {
            if (!_isInitialized) return;

            GL gl = renderer.GetContext();

            // Lazily create GPU meshes for objects that have vertex data but no mesh yet
            // (GL context is current here, safe to create GPU resources)
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

            gl.ClearColor(0.12f, 0.12f, 0.14f, 1.0f);
            gl.Clear((uint)GLEnum.ColorBufferBit | (uint)GLEnum.DepthBufferBit);

            pbrPipeline.Bind();

            var lightVec = Vector3D.Normalize(new Vector3D<float>(LightDirX, LightDirY, LightDirZ));
            pbrPipeline.SetUniform("Light0Dir", lightVec);
            pbrPipeline.SetUniform("Light0Color", new Vector3D<float>(1.0f, 0.95f, 0.9f));
            pbrPipeline.SetUniform("Light0Intensity", 1.2f);
            pbrPipeline.SetUniform("CameraPos", camera.Position);
            pbrPipeline.SetUniform("AmbientIntensity", 0.3f);

            foreach (var obj in sceneObjects)
            {
                if (!obj.Active || obj.Mesh == null) continue;

                Matrix4X4<float> m = obj.Transform.WorldMatrix;
                Matrix4X4<float> objectToClip = m * camera.View * camera.Projection;
                Matrix4X4<float> worldToObject = m.Invert();

                pbrPipeline.SetUniform("Model", m);
                pbrPipeline.SetUniform("View", camera.View);
                pbrPipeline.SetUniform("Projection", camera.Projection);
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
        }

        public void OnMouseDown(float x, float y)
        {
            _isMouseDown = true;
            _lastMouseX = x;
            _lastMouseY = y;
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
        }

        public void OnScroll(float delta)
        {
            orbitController.Zoom(delta * 2);
        }
    }
}
