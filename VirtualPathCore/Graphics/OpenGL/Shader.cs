using System;
using Silk.NET.OpenGLES;

namespace VirtualPathCore.Graphics.OpenGL
{
    /// <summary>
    /// 表示一个 OpenGL 着色器对象
    /// </summary>
    public class Shader : GraphicsResource
    {
        /// <summary>
        /// 初始化一个新的 <see cref="Shader"/> 实例
        /// </summary>
        /// <param name="graphicsHost">图形主机</param>
        /// <param name="shaderType">着色器的类型（顶点着色器或片段着色器）</param>
        /// <param name="source">着色器的源代码</param>
        /// <exception cref="Exception">如果着色器编译失败，将抛出异常，并包含编译错误信息</exception>
        public Shader(IGraphicsHost<GL> graphicsHost, ShaderType shaderType, string source) : base(graphicsHost)
        {
            Handle = GL.CreateShader(shaderType);

            GL.ShaderSource(Handle, source);
            GL.CompileShader(Handle);

            GL.GetShader(Handle, ShaderParameterName.CompileStatus, out int success);
            if (success == 0)
            {
                string error = GL.GetShaderInfoLog(Handle);
                GL.DeleteShader(Handle);

                throw new Exception($"{shaderType}: {error}");
            }
        }

        /// <summary>
        /// 释放使用的资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected override void Destroy(bool disposing = false)
        {
            GL.DeleteShader(Handle);
        }
    }
}
