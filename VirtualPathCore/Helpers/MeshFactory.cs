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
        /// 修补顶点数据中的切线/副切线。
        ///
        /// <see cref="Vertex"/> 的 <c>Tangent</c> / <c>Bitangent</c> 默认为零向量，
        /// 而 <c>PBR.vert</c> 会对它们执行 <c>normalize()</c>。
        /// <c>normalize(vec3(0))</c> 在 GLSL 中返回 <b>NaN</b>，
        /// NaN 写入 varying 后会污染相邻像素，导致几何体在画面上被撕成细长"刀片"。
        ///
        /// 这里按"当前面法线与世界上方向叉乘"补一个与法线正交的切线，
        /// 副切线取其叉乘，构成右手 TBN 基。未使用法线贴图时该值不影响着色，
        /// 但能保证着色器输入合法。
        /// </summary>
        /// <param name="vertices">待修补的顶点数组（原地修改）</param>
        public static void EnsureTangents(Vertex[] vertices)
        {
            if (vertices == null)
                return;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vertex v = vertices[i];

                Vector3D<float> n = v.Normal;
                float nLen = MathF.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
                if (nLen > 1e-6f)
                    n /= nLen;

                // 参考轴：与法线夹角最大的坐标轴，避免叉乘退化
                Vector3D<float> up = MathF.Abs(n.Y) < 0.99f
                    ? new Vector3D<float>(0f, 1f, 0f)
                    : new Vector3D<float>(1f, 0f, 0f);

                var t = Vector3D.Normalize(Vector3D.Cross(up, n));
                var b = Vector3D.Cross(n, t);

                v.Tangent = t;
                v.Bitangent = b;
                vertices[i] = v;
            }
        }

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

            EnsureTangents(vertices);
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
        EnsureTangents(vertices);
        indices = idxList.ToArray();
    }

    /// <summary>
    /// 获取一个圆柱体的顶点和索引数据
    /// </summary>
    public static void GetCylinder(out Vertex[] vertices, out uint[] indices, float radius = 0.5f, float height = 1.0f, int segments = 24)
    {
        var vertList = new System.Collections.Generic.List<Vertex>();
        var idxList = new System.Collections.Generic.List<uint>();
        float halfH = height / 2;

        // Side vertices
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2 * MathF.PI / segments;
            float x = radius * MathF.Cos(angle);
            float y = radius * MathF.Sin(angle);
            Vector3D<float> n = new(MathF.Cos(angle), MathF.Sin(angle), 0);
            vertList.Add(new Vertex(new(x, y, halfH), n, texCoord: new((float)i / segments, 1)));
            vertList.Add(new Vertex(new(x, y, -halfH), n, texCoord: new((float)i / segments, 0)));
        }
        for (int i = 0; i < segments; i++)
        {
            int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
            idxList.Add((uint)a); idxList.Add((uint)c); idxList.Add((uint)b);
            idxList.Add((uint)b); idxList.Add((uint)c); idxList.Add((uint)d);
        }

        // Top cap
        int topCenter = vertList.Count;
        vertList.Add(new Vertex(new(0, 0, halfH), new(0, 0, 1)));
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2 * MathF.PI / segments;
            vertList.Add(new Vertex(new(radius * MathF.Cos(angle), radius * MathF.Sin(angle), halfH), new(0, 0, 1)));
        }
        for (int i = 0; i < segments; i++)
        {
            idxList.Add((uint)(topCenter + i + 1));
            idxList.Add((uint)(topCenter));
            idxList.Add((uint)(topCenter + i + 2));
        }

        // Bottom cap
        int botCenter = vertList.Count;
        vertList.Add(new Vertex(new(0, 0, -halfH), new(0, 0, -1)));
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2 * MathF.PI / segments;
            vertList.Add(new Vertex(new(radius * MathF.Cos(angle), radius * MathF.Sin(angle), -halfH), new(0, 0, -1)));
        }
        for (int i = 0; i < segments; i++)
        {
            idxList.Add((uint)(botCenter));
            idxList.Add((uint)(botCenter + i + 1));
            idxList.Add((uint)(botCenter + i + 2));
        }

        vertices = vertList.ToArray();
        EnsureTangents(vertices);
        indices = idxList.ToArray();
    }

    /// <summary>
    /// 获取一个圆锥体的顶点和索引数据
    /// </summary>
    public static void GetCone(out Vertex[] vertices, out uint[] indices, float radius = 0.5f, float height = 1.0f, int segments = 24)
    {
        var vertList = new System.Collections.Generic.List<Vertex>();
        var idxList = new System.Collections.Generic.List<uint>();
        float halfH = height / 2;

        // Tip
        int tip = vertList.Count;
        vertList.Add(new Vertex(new(0, 0, halfH), new(0, 0, 1)));

        // Base ring
        int baseStart = vertList.Count;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2 * MathF.PI / segments;
            float x = radius * MathF.Cos(angle);
            float y = radius * MathF.Sin(angle);
            Vector3D<float> n = Vector3D.Normalize(new Vector3D<float>(MathF.Cos(angle), MathF.Sin(angle), 0.5f));
            vertList.Add(new Vertex(new(x, y, -halfH), n));
        }

        for (int i = 0; i < segments; i++)
        {
            idxList.Add((uint)(baseStart + i));
            idxList.Add((uint)tip);
            idxList.Add((uint)(baseStart + i + 1));
        }

        // Base cap
        int botCenter = vertList.Count;
        vertList.Add(new Vertex(new(0, 0, -halfH), new(0, 0, -1)));
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2 * MathF.PI / segments;
            vertList.Add(new Vertex(new(radius * MathF.Cos(angle), radius * MathF.Sin(angle), -halfH), new(0, 0, -1)));
        }
        for (int i = 0; i < segments; i++)
        {
            idxList.Add((uint)(botCenter));
            idxList.Add((uint)(botCenter + i + 1));
            idxList.Add((uint)(botCenter + i + 2));
        }

        vertices = vertList.ToArray();
        EnsureTangents(vertices);
        indices = idxList.ToArray();
    }

    /// <summary>
    /// 获取一个圆环体的顶点和索引数据
    /// </summary>
    public static void GetTorus(out Vertex[] vertices, out uint[] indices, float mainRadius = 0.6f, float tubeRadius = 0.2f, int segments = 24, int sides = 12)
    {
        var vertList = new System.Collections.Generic.List<Vertex>();
        var idxList = new System.Collections.Generic.List<uint>();

        for (int i = 0; i <= segments; i++)
        {
            float u = i * 2 * MathF.PI / segments;
            float cosU = MathF.Cos(u), sinU = MathF.Sin(u);

            for (int j = 0; j <= sides; j++)
            {
                float v = j * 2 * MathF.PI / sides;
                float cosV = MathF.Cos(v), sinV = MathF.Sin(v);

                float x = (mainRadius + tubeRadius * cosV) * cosU;
                float y = (mainRadius + tubeRadius * cosV) * sinU;
                float z = tubeRadius * sinV;

                Vector3D<float> n = Vector3D.Normalize(new Vector3D<float>(cosV * cosU, cosV * sinU, sinV));
                vertList.Add(new Vertex(new(x, y, z), n, texCoord: new((float)i / segments, (float)j / sides)));
            }
        }

        for (int i = 0; i < segments; i++)
        {
            int row = i * (sides + 1);
            int next = (i + 1) * (sides + 1);
            for (int j = 0; j < sides; j++)
            {
                int a = row + j, b = next + j, c = row + j + 1, d = next + j + 1;
                idxList.Add((uint)a); idxList.Add((uint)c); idxList.Add((uint)b);
                idxList.Add((uint)b); idxList.Add((uint)c); idxList.Add((uint)d);
            }
        }

        vertices = vertList.ToArray();
        EnsureTangents(vertices);
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

            EnsureTangents(vertices);
            indices = vertices.Select((a, b) => (uint)b).ToArray();
        }
    }
}
