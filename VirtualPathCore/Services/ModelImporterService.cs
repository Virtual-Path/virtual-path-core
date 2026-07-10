using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Maths;
using VirtualPathCore.Graphics;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.Helpers;
using VirtualPathCore.ViewModels;

namespace VirtualPathCore.Services;

public class ModelImporterService
{
    public List<SceneObjectViewModel> ImportModel(string filePath, SceneService sceneService)
    {
        var result = new List<SceneObjectViewModel>();
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext is ".glb" or ".gltf")
        {
            ImportGltf(filePath, result);
        }

        return result;
    }

    private void ImportGltf(string filePath, List<SceneObjectViewModel> result)
    {
        try
        {
            string json;
            string baseDir = Path.GetDirectoryName(filePath)!;

            if (Path.GetExtension(filePath).ToLowerInvariant() == ".glb")
            {
                json = ReadGlbJson(filePath);
            }
            else
            {
                json = File.ReadAllText(filePath);
            }

            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("meshes", out var meshes))
                return;

            var accessors = root.TryGetProperty("accessors", out var accEl) ? accEl : default;
            var bufferViews = root.TryGetProperty("bufferViews", out var bvEl) ? bvEl : default;
            var buffers = root.TryGetProperty("buffers", out var bufEl) ? bufEl : default;

            byte[]? glbBinData = null;
            if (Path.GetExtension(filePath).ToLowerInvariant() == ".glb")
            {
                glbBinData = ReadGlbBin(filePath);
            }

            for (int m = 0; m < meshes.GetArrayLength(); m++)
            {
                var mesh = meshes[m];
                if (!mesh.TryGetProperty("primitives", out var primitives))
                    continue;

                for (int p = 0; p < primitives.GetArrayLength(); p++)
                {
                    var prim = primitives[p];

                    if (!prim.TryGetProperty("attributes", out var attrs))
                        continue;

                    var verts = new List<Vertex>();
                    var idxs = new List<uint>();

                    if (attrs.TryGetProperty("POSITION", out var posAccessorIdx))
                    {
                        var positions = ReadAccessorData<Vector3D<float>>(posAccessorIdx.GetInt32(), accessors, bufferViews, buffers, root, glbBinData, baseDir);
                        for (int i = 0; i < positions.Length; i++)
                        {
                            verts.Add(new Vertex(positions[i]));
                        }
                    }

                    if (attrs.TryGetProperty("NORMAL", out var normAccessorIdx))
                    {
                        var normals = ReadAccessorData<Vector3D<float>>(normAccessorIdx.GetInt32(), accessors, bufferViews, buffers, root, glbBinData, baseDir);
                        for (int i = 0; i < normals.Length && i < verts.Count; i++)
                        {
                            var v = verts[i];
                            v.Normal = normals[i];
                            verts[i] = v;
                        }
                    }

                    if (attrs.TryGetProperty("TEXCOORD_0", out var tcAccessorIdx))
                    {
                        var texcoords = ReadAccessorData<Vector2D<float>>(tcAccessorIdx.GetInt32(), accessors, bufferViews, buffers, root, glbBinData, baseDir);
                        for (int i = 0; i < texcoords.Length && i < verts.Count; i++)
                        {
                            var v = verts[i];
                            v.TexCoord = texcoords[i];
                            verts[i] = v;
                        }
                    }

                    if (prim.TryGetProperty("indices", out var idxAccessorIdx))
                    {
                        var indices = ReadAccessorData<uint>(idxAccessorIdx.GetInt32(), accessors, bufferViews, buffers, root, glbBinData, baseDir);
                        idxs.AddRange(indices);
                    }
                    else
                    {
                        for (uint i = 0; i < verts.Count; i++)
                            idxs.Add(i);
                    }

                    if (verts.Count > 0 && idxs.Count > 0)
                    {
                        var name = $"Mesh_{m}_{p}";
                        if (mesh.TryGetProperty("name", out var nameEl))
                            name = nameEl.GetString() ?? name;

                        var obj = new SceneObject
                        {
                            Name = name,
                            MeshBlueprint = new MeshData(verts.ToArray(), idxs.ToArray()),
                            Material = new Material
                            {
                                Albedo = new Vector4D<float>(0.8f, 0.8f, 0.8f, 1.0f),
                                Metallic = 0.0f,
                                Roughness = 0.8f
                            }
                        };
                        result.Add(new SceneObjectViewModel(obj));
                    }
                }
            }

            if (root.TryGetProperty("nodes", out var nodes))
            {
                for (int i = 0; i < nodes.GetArrayLength(); i++)
                {
                    if (nodes[i].TryGetProperty("translation", out var trans))
                    {
                        if (i < result.Count)
                        {
                            var t = result[i].SceneObject.Transform;
                            t.Position = new Vector3D<float>(
                                (float)trans[0].GetDouble(),
                                (float)trans[1].GetDouble(),
                                (float)trans[2].GetDouble());
                        }
                    }
                }
            }
        }
        catch
        {
        }
    }

    private static string ReadGlbJson(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        using var br = new BinaryReader(fs);

        var magic = br.ReadUInt32();
        var version = br.ReadUInt32();
        var totalLength = br.ReadUInt32();

        while (fs.Position < fs.Length)
        {
            var chunkLength = br.ReadUInt32();
            var chunkType = br.ReadUInt32();

            if (chunkType == 0x4E4F534A)
            {
                var jsonBytes = br.ReadBytes((int)chunkLength);
                return System.Text.Encoding.UTF8.GetString(jsonBytes);
            }

            fs.Seek(chunkLength, SeekOrigin.Current);
        }

        return "";
    }

    private static byte[] ReadGlbBin(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        using var br = new BinaryReader(fs);

        var magic = br.ReadUInt32();
        var version = br.ReadUInt32();
        var totalLength = br.ReadUInt32();

        while (fs.Position < fs.Length)
        {
            var chunkLength = br.ReadUInt32();
            var chunkType = br.ReadUInt32();

            if (chunkType == 0x004E4942)
            {
                return br.ReadBytes((int)chunkLength);
            }

            fs.Seek(chunkLength, SeekOrigin.Current);
        }

        return Array.Empty<byte>();
    }

    private static T[] ReadAccessorData<T>(int accessorIdx, System.Text.Json.JsonElement accessors,
        System.Text.Json.JsonElement bufferViews, System.Text.Json.JsonElement buffers,
        System.Text.Json.JsonElement root, byte[]? glbBinData, string baseDir) where T : unmanaged
    {
        if (accessors.ValueKind != System.Text.Json.JsonValueKind.Array || accessorIdx >= accessors.GetArrayLength())
            return Array.Empty<T>();

        var acc = accessors[accessorIdx];
        if (!acc.TryGetProperty("bufferView", out var bvIdxEl)) return Array.Empty<T>();
        int bvIdx = bvIdxEl.GetInt32();
        int count = acc.GetProperty("count").GetInt32();
        int byteOffset = acc.TryGetProperty("byteOffset", out var boEl) ? boEl.GetInt32() : 0;

        if (bufferViews.ValueKind != System.Text.Json.JsonValueKind.Array || bvIdx >= bufferViews.GetArrayLength())
            return Array.Empty<T>();

        var bv = bufferViews[bvIdx];
        int bufIdx = bv.GetProperty("buffer").GetInt32();
        int bufByteOffset = bv.TryGetProperty("byteOffset", out var bboEl) ? bboEl.GetInt32() : 0;
        int bufByteLength = bv.GetProperty("byteLength").GetInt32();
        int byteStride = bv.TryGetProperty("byteStride", out var bsEl) ? bsEl.GetInt32() : 0;

        byte[]? bufferData = null;

        if (buffers.ValueKind == System.Text.Json.JsonValueKind.Array && bufIdx < buffers.GetArrayLength())
        {
            var buf = buffers[bufIdx];
            if (buf.TryGetProperty("uri", out var uriEl))
            {
                string uri = uriEl.GetString()!;
                string dataPath = Path.Combine(baseDir, uri);
                if (uri.StartsWith("data:"))
                {
                    var parts = uri.Split(',');
                    if (parts.Length == 2)
                        bufferData = Convert.FromBase64String(parts[1]);
                }
                else if (File.Exists(dataPath))
                {
                    bufferData = File.ReadAllBytes(dataPath);
                }
            }
            else if (glbBinData != null)
            {
                bufferData = glbBinData;
            }
        }

        if (bufferData == null) return Array.Empty<T>();

        int typeSize = Unsafe.SizeOf<T>();
        int start = bufByteOffset + byteOffset;
        int stride = byteStride > 0 ? byteStride : typeSize;

        T[] result = new T[count];
        for (int i = 0; i < count; i++)
        {
            int offset = start + i * stride;
            if (offset + typeSize <= bufferData.Length)
            {
                result[i] = MemoryMarshal.Read<T>(bufferData.AsSpan(offset));
            }
        }

        return result;
    }
}
