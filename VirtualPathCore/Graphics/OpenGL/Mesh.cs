using System.Collections.Generic;
using System.Runtime.InteropServices;
using Silk.NET.OpenGLES;

namespace VirtualPathCore.Graphics.OpenGL;

/// <summary>
/// 表示一个OpenGL网格对象，用于管理顶点数据和索引数据
/// </summary>
public unsafe class Mesh : GraphicsResource
{
    private static readonly Dictionary<string, nint> _offsetCache = new();

    private static nint GetFieldOffset(string fieldName)
    {
        if (!_offsetCache.TryGetValue(fieldName, out nint offset))
        {
            offset = Marshal.OffsetOf<Vertex>(fieldName);
            _offsetCache[fieldName] = offset;
        }
        return offset;
    }

    /// <summary>
    /// 初始化 <see cref="Mesh"/> 类的新实例
    /// </summary>
    /// <param name="graphicsHost">图形宿主对象，用于管理OpenGL上下文</param>
    /// <param name="vertices">顶点数据数组</param>
    /// <param name="indices">索引数据数组</param>
    public Mesh(IGraphicsHost<GL> graphicsHost, Vertex[] vertices, uint[] indices) : base(graphicsHost)
    {
        Handle = GL.GenVertexArray();
        ArrayBuffer = GL.GenBuffer();
        IndexBuffer = GL.GenBuffer();
        IndexLength = indices.Length;

        GL.BindVertexArray(Handle);

        GL.BindBuffer(GLEnum.ArrayBuffer, ArrayBuffer);
        GL.BufferData<Vertex>(GLEnum.ArrayBuffer, (uint)(vertices.Length * sizeof(Vertex)), vertices, GLEnum.StaticDraw);

        GL.BindBuffer(GLEnum.ElementArrayBuffer, IndexBuffer);
        GL.BufferData<uint>(GLEnum.ElementArrayBuffer, (uint)(indices.Length * sizeof(uint)), indices, GLEnum.StaticDraw);

        GL.BindVertexArray(0);
    }

    /// <summary>
    /// 获取顶点数组缓冲区对象的句柄
    /// </summary>
    public uint ArrayBuffer { get; }

    /// <summary>
    /// 获取索引缓冲区对象的句柄
    /// </summary>
    public uint IndexBuffer { get; }

    /// <summary>
    /// 获取索引数据的长度
    /// </summary>
    public int IndexLength { get; }

    /// <summary>
    /// 销毁网格对象，释放相关的OpenGL资源
    /// </summary>
    /// <param name="disposing">指示是否显式释放资源</param>
    protected override void Destroy(bool disposing = false)
    {
        GL.DeleteVertexArray(Handle);
        GL.DeleteBuffer(ArrayBuffer);
        GL.DeleteBuffer(IndexBuffer);
    }

    /// <summary>
    /// 设置顶点属性指针，指定顶点数据的布局
    /// </summary>
    /// <param name="index">顶点属性的索引</param>
    /// <param name="size">每个顶点属性的分量数量</param>
    /// <param name="fieldName">顶点属性在顶点结构体中的字段名称</param>
    public void VertexAttributePointer(uint index, int size, string fieldName)
    {
        GL.BindVertexArray(Handle);

        GL.BindBuffer(GLEnum.ArrayBuffer, ArrayBuffer);
        GL.VertexAttribPointer(index, size, GLEnum.Float, false, (uint)sizeof(Vertex), (void*)GetFieldOffset(fieldName));
        GL.EnableVertexAttribArray(index);
        GL.BindBuffer(GLEnum.ArrayBuffer, 0);

        GL.BindVertexArray(0);
    }

    /// <summary>
    /// 设置所有标准顶点属性指针。应在Mesh创建后调用一次。
    /// </summary>
    public void SetupAttributes(
        int posLoc, int normalLoc, int tangentLoc, int bitangentLoc,
        int colorLoc, int texCoordLoc)
    {
        if (posLoc >= 0) VertexAttributePointer((uint)posLoc, 3, nameof(Vertex.Position));
        if (normalLoc >= 0) VertexAttributePointer((uint)normalLoc, 3, nameof(Vertex.Normal));
        if (tangentLoc >= 0) VertexAttributePointer((uint)tangentLoc, 3, nameof(Vertex.Tangent));
        if (bitangentLoc >= 0) VertexAttributePointer((uint)bitangentLoc, 3, nameof(Vertex.Bitangent));
        if (colorLoc >= 0) VertexAttributePointer((uint)colorLoc, 4, nameof(Vertex.Color));
        if (texCoordLoc >= 0) VertexAttributePointer((uint)texCoordLoc, 2, nameof(Vertex.TexCoord));
    }

    /// <summary>
    /// 绘制网格
    /// </summary>
    public void Draw(GLEnum mode = GLEnum.Triangles)
    {
        GL.BindVertexArray(Handle);
        GL.DrawElements(mode, (uint)IndexLength, GLEnum.UnsignedInt, null);
        GL.BindVertexArray(0);
    }
}
