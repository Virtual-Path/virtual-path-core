using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VirtualPathCore.Contracts.Services;
using VirtualPathCore.Graphics.OpenGL;
using Silk.NET.Maths;

namespace VirtualPathCore.Services
{
    /// <summary>
    /// 提供外部绘图服务，实现 IDrawingService 接口
    /// 该服务负责与外部 OpenGL 库进行交互，以执行绘图操作
    /// </summary>
    public unsafe partial class ExternalDrawingService : IDrawingService
    {
        static ExternalDrawingService()
        {
            NativeLibrary.SetDllImportResolver(typeof(ExternalDrawingService).Assembly, (libraryName, assembly, searchPath) =>
            {
                libraryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{libraryName}.dll" : $"lib{libraryName}.so";

                string libPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Dependencies", libraryName);

                NativeLibrary.TryLoad(libPath, out nint handle);

                return handle;
            });
        }

        #region External Drawing Service
        public delegate void* GetProcAddress(string proc);

        [LibraryImport("VirtualPathCore.OpenGL")]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
        private static partial void MakeContext(GetProcAddress getProcAddress, out long id);

        [LibraryImport("VirtualPathCore.OpenGL")]
        private static partial void LoadScene(long id);

        [LibraryImport("VirtualPathCore.OpenGL")]
        private static partial void UpdateScene(long id, double deltaSeconds, Vector2D<float>* size);

        [LibraryImport("VirtualPathCore.OpenGL")]
        private static partial void DrawScene(long id, double deltaSeconds);
        #endregion

        private Renderer renderer = null!;
        private long rendererId;

        /// <summary>
        /// 加载外部绘图服务所需的资源，例如渲染器和创建 OpenGL 上下文
        /// </summary>
        /// <param name="args">绘图服务所需的参数，包括 Renderer 对象</param>
        public void Load(object[] args)
        {
            if (args == null || args.Length < 1 || args[0] is not Renderer r)
            {
                return;
            }

            renderer = r;

            try
            {
                MakeContext((proc) =>
                {
                    if (renderer.GetContext().Context.TryGetProcAddress(proc, out nint addr))
                    {
                        return (void*)addr;
                    }

                    return (void*)0;

                }, out rendererId);

                LoadScene(rendererId);
            }
            catch
            {
                // External native library not available - service will be inactive
                rendererId = 0;
            }
        }

        /// <summary>
        /// 更新外部绘图服务的状态，包括窗口大小和经过的时间
        /// </summary>
        /// <param name="deltaSeconds">自上次更新以来经过的时间，以秒为单位</param>
        public void Update(double deltaSeconds)
        {
            if (rendererId == 0) return;

            Vector2D<float> size = new((float)renderer.Bounds.Width, (float)renderer.Bounds.Height);

            UpdateScene(rendererId, deltaSeconds, &size);
        }

        /// <summary>
        /// 渲染场景到屏幕
        /// </summary>
        /// <param name="deltaSeconds">自上次渲染以来经过的时间，以秒为单位</param>
        public void Render(double deltaSeconds)
        {
            if (rendererId == 0) return;

            DrawScene(rendererId, deltaSeconds);
        }
    }
}
