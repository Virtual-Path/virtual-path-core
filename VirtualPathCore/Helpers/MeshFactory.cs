using System;
using System.Linq;
using Silk.NET.Maths;
using VirtualPathCore.Graphics;

namespace VirtualPathCore.Helpers
{
    /// <summary>
    /// 提供用于生成不同类型网格（Mesh）的静态工厂方法
    /// </summary>
    public static unsafe class MeshFactory
    {
        /// <summary>
        /// 获取一个立方体的顶点和索引数据
        /// </summary>
        /// <param name="vertices">输出的顶点数组</param>
        /// <param name="indices">输出的索引数组</param>
        /// <param name="size">立方体的大小，默认为 0.5f</param>
        public static void GetCube(out Vertex[] vertices, out uint[] indices, float size = 0.5f)
        {
            vertices =
            [
                // 前面
                new(new(-size, -size, size), new(0.0f, 0.0f, 1.0f), texCoord: new(0.0f, 0.0f)),
                new(new(size, -size, size), new(0.0f, 0.0f, 1.0f), texCoord: new(1.0f, 0.0f)),
                new(new(size, size, size), new(0.0f, 0.0f, 1.0f), texCoord: new(1.0f, 1.0f)),
                new(new(size, size, size), new(0.0f, 0.0f, 1.0f), texCoord: new(1.0f, 1.0f)),
                new(new(-size, size, size), new(0.0f, 0.0f, 1.0f), texCoord: new(0.0f, 1.0f)),
                new(new(-size, -size, size), new(0.0f, 0.0f, 1.0f), texCoord: new(0.0f, 0.0f)),

                // 后面
                new(new(-size, -size, -size), new(0.0f, 0.0f, -1.0f), texCoord: new(1.0f, 0.0f)),
                new(new(-size, size, -size), new(0.0f, 0.0f, -1.0f), texCoord: new(1.0f, 1.0f)),
                new(new(size, size, -size), new(0.0f, 0.0f, -1.0f), texCoord: new(0.0f, 1.0f)),
                new(new(size, size, -size), new(0.0f, 0.0f, -1.0f), texCoord: new(0.0f, 1.0f)),
                new(new(size, -size, -size), new(0.0f, 0.0f, -1.0f), texCoord: new(0.0f, 0.0f)),
                new(new(-size, -size, -size), new(0.0f, 0.0f, -1.0f), texCoord: new(1.0f, 0.0f)),

                // 上面
                new(new(-size, size, -size), new(0.0f, 1.0f, 0.0f), texCoord: new(0.0f, 1.0f)),
                new(new(-size, size, size), new(0.0f, 1.0f, 0.0f), texCoord: new(0.0f, 0.0f)),
                new(new(size, size, size), new(0.0f, 1.0f, 0.0f), texCoord: new(1.0f, 0.0f)),
                new(new(size, size, size), new(0.0f, 1.0f, 0.0f), texCoord: new(1.0f, 0.0f)),
                new(new(size, size, -size), new(0.0f, 1.0f, 0.0f), texCoord: new(1.0f, 1.0f)),
                new(new(-size, size, -size), new(0.0f, 1.0f, 0.0f), texCoord: new(0.0f, 1.0f)),

                // 下面
                new(new(-size, -size, -size), new(0.0f, -1.0f, 0.0f), texCoord: new(0.0f, 0.0f)),
                new(new(size, -size, -size), new(0.0f, -1.0f, 0.0f), texCoord: new(1.0f, 0.0f)),
                new(new(size, -size, size), new(0.0f, -1.0f, 0.0f), texCoord: new(1.0f, 1.0f)),
                new(new(size, -size, size), new(0.0f, -1.0f, 0.0f), texCoord: new(1.0f, 1.0f)),
                new(new(-size, -size, size), new(0.0f, -1.0f, 0.0f), texCoord: new(0.0f, 1.0f)),
                new(new(-size, -size, -size), new(0.0f, -1.0f, 0.0f), texCoord: new(0.0f, 0.0f)),

                // 右面
                new(new(size, -size, -size), new(1.0f, 0.0f, 0.0f), texCoord: new(1.0f, 0.0f)),
                new(new(size, size, -size), new(1.0f, 0.0f, 0.0f), texCoord: new(1.0f, 1.0f)),
                new(new(size, size, size), new(1.0f, 0.0f, 0.0f), texCoord: new(0.0f, 1.0f)),
                new(new(size, size, size), new(1.0f, 0.0f, 0.0f), texCoord: new(0.0f, 1.0f)),
                new(new(size, -size, size), new(1.0f, 0.0f, 0.0f), texCoord: new(0.0f, 0.0f)),
                new(new(size, -size, -size), new(1.0f, 0.0f, 0.0f), texCoord: new(1.0f, 0.0f)),

                // 左面
                new(new(-size, -size, -size), new(-1.0f, 0.0f, 0.0f), texCoord: new(0.0f, 0.0f)),
                new(new(-size, -size, size), new(-1.0f, 0.0f, 0.0f), texCoord: new(1.0f, 0.0f)),
                new(new(-size, size, size), new(-1.0f, 0.0f, 0.0f), texCoord: new(1.0f, 1.0f)),
                new(new(-size, size, size), new(-1.0f, 0.0f, 0.0f), texCoord: new(1.0f, 1.0f)),
                new(new(-size, size, -size), new(-1.0f, 0.0f, 0.0f), texCoord: new(0.0f, 1.0f)),
                new(new(-size, -size, -size), new(-1.0f, 0.0f, 0.0f), texCoord: new(0.0f, 0.0f))
            ];

            indices = vertices.Select((a, b) => (uint)b).ToArray();
        }

    /// <summary>
    /// 获取一个球体的顶点和索引数据
    /// </summary>
    public static void GetSphere(out Vertex[] vertices, out uint[] indices, float radius = 0.5f, int sectors = 16, int stacks = 16)
    {
        var vertList = new System.Collections.Generic.List<Vertex>();
        var idxList = new System.Collections.Generic.List<uint>();

        float pi = MathF.PI;
        float sectorStep = 2 * pi / sectors;
        float stackStep = pi / stacks;

        for (int i = 0; i <= stacks; i++)
        {
            float stackAngle = pi / 2 - i * stackStep;
            float xy = radius * MathF.Cos(stackAngle);
            float z = radius * MathF.Sin(stackAngle);

            for (int j = 0; j <= sectors; j++)
            {
                float sectorAngle = j * sectorStep;
                float x = xy * MathF.Cos(sectorAngle);
                float y = xy * MathF.Sin(sectorAngle);

                Vector3D<float> normal = Vector3D.Normalize(new Vector3D<float>(x, y, z));
                Vector2D<float> uv = new((float)j / sectors, (float)i / stacks);
                vertList.Add(new Vertex(new Vector3D<float>(x, y, z), normal, texCoord: uv));
            }
        }

        for (int i = 0; i < stacks; i++)
        {
            uint k1 = (uint)(i * (sectors + 1));
            uint k2 = (uint)((i + 1) * (sectors + 1));

            for (int j = 0; j < sectors; j++)
            {
                if (i != 0)
                {
                    idxList.Add(k1);
                    idxList.Add(k2);
                    idxList.Add(k1 + 1);
                }
                if (i != stacks - 1)
                {
                    idxList.Add(k1 + 1);
                    idxList.Add(k2);
                    idxList.Add(k2 + 1);
                }
                k1++;
                k2++;
            }
        }

        vertices = vertList.ToArray();
        indices = idxList.ToArray();
    }

    /// <summary>
    /// 获取一个平面的顶点和索引数据
    /// </summary>
    /// <param name="vertices">输出的顶点数组</param>
    /// <param name="indices">输出的索引数组</param>
    public static void GetCanvas(out Vertex[] vertices, out uint[] indices)
        {
            vertices =
            [
                new(new(-1.0f, 1.0f, 0.0f), new(0.0f, 0.0f, 1.0f), texCoord: new(0.0f, 1.0f)),
                new(new(-1.0f, -1.0f, 0.0f), new(0.0f, 0.0f, 1.0f), texCoord: new(0.0f, 0.0f)),
                new(new(1.0f, -1.0f, 0.0f), new(0.0f, 0.0f, 1.0f), texCoord: new(1.0f, 0.0f)),
                new(new(1.0f, -1.0f, 0.0f), new(0.0f, 0.0f, 1.0f), texCoord: new(1.0f, 0.0f)),
                new(new(1.0f, 1.0f, 0.0f), new(0.0f, 0.0f, 1.0f), texCoord: new(1.0f, 1.0f)),
                new(new(-1.0f, 1.0f, 0.0f), new(0.0f, 0.0f, 1.0f), texCoord: new(0.0f, 1.0f))
            ];

            indices = vertices.Select((a, b) => (uint)b).ToArray();
        }
    }
}
