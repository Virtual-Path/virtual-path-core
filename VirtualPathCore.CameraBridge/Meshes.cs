using Silk.NET.Maths;
using VirtualPathCore.Graphics;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 验证用的程序化几何体。法线/切线/副切线在顶点里显式给出，
/// 与 <c>PBR.vert</c> 里 <c>VS_TBN</c> 的用法一致。
/// </summary>
internal static class Meshes
{
    /// <summary>轴对齐立方体：每面 4 顶点，共 24 顶点 / 36 索引。</summary>
    public static (Vertex[] Vertices, uint[] Indices) CreateCube(
        float halfSize, Vector4D<float> color)
    {
        var verts = new List<Vertex>(24);
        var idx = new List<uint>(36);

        void AddFace(Vector3D<float> n, Vector3D<float> t, Vector3D<float> b)
        {
            uint b0 = (uint)verts.Count;
            Vector3D<float> c = n * halfSize;
            Vector3D<float> u = t * halfSize;
            Vector3D<float> v = b * halfSize;

            verts.Add(new Vertex(c - u - v, n, t, b, color, new Vector2D<float>(0, 0)));
            verts.Add(new Vertex(c + u - v, n, t, b, color, new Vector2D<float>(1, 0)));
            verts.Add(new Vertex(c + u + v, n, t, b, color, new Vector2D<float>(1, 1)));
            verts.Add(new Vertex(c - u + v, n, t, b, color, new Vector2D<float>(0, 1)));

            idx.Add(b0); idx.Add(b0 + 1); idx.Add(b0 + 2);
            idx.Add(b0); idx.Add(b0 + 2); idx.Add(b0 + 3);
        }

        AddFace(new Vector3D<float>(1, 0, 0), new Vector3D<float>(0, 0, -1), new Vector3D<float>(0, 1, 0));
        AddFace(new Vector3D<float>(-1, 0, 0), new Vector3D<float>(0, 0, 1), new Vector3D<float>(0, 1, 0));
        AddFace(new Vector3D<float>(0, 1, 0), new Vector3D<float>(1, 0, 0), new Vector3D<float>(0, 0, 1));
        AddFace(new Vector3D<float>(0, -1, 0), new Vector3D<float>(1, 0, 0), new Vector3D<float>(0, 0, -1));
        AddFace(new Vector3D<float>(0, 0, 1), new Vector3D<float>(1, 0, 0), new Vector3D<float>(0, 1, 0));
        AddFace(new Vector3D<float>(0, 0, -1), new Vector3D<float>(-1, 0, 0), new Vector3D<float>(0, 1, 0));

        return (verts.ToArray(), idx.ToArray());
    }

    /// <summary>XZ 平面上的地台，法线朝 +Y。</summary>
    public static (Vertex[] Vertices, uint[] Indices) CreateGround(
        float halfSize, Vector4D<float> color)
    {
        var n = new Vector3D<float>(0, 1, 0);
        var t = new Vector3D<float>(1, 0, 0);
        var b = new Vector3D<float>(0, 0, 1);

        var verts = new[]
        {
            new Vertex(new Vector3D<float>(-halfSize, 0, -halfSize), n, t, b, color, new Vector2D<float>(0, 0)),
            new Vertex(new Vector3D<float>( halfSize, 0, -halfSize), n, t, b, color, new Vector2D<float>(1, 0)),
            new Vertex(new Vector3D<float>( halfSize, 0,  halfSize), n, t, b, color, new Vector2D<float>(1, 1)),
            new Vertex(new Vector3D<float>(-halfSize, 0,  halfSize), n, t, b, color, new Vector2D<float>(0, 1)),
        };
        uint[] idx = { 0, 1, 2, 0, 2, 3 };

        return (verts, idx);
    }
}
