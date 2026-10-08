using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VirtualPathCore.Graphics;
using VirtualPathCore.Graphics.Core;
using VirtualPathCore.ViewModels;

namespace VirtualPathCore.Services;

public class ModelExporterService
{
    public void ExportScene(string filePath, IReadOnlyList<SceneObjectViewModel> objects)
    {
        var gltf = new Dictionary<string, object>();
        var meshes = new List<Dictionary<string, object>>();
        var accessors = new List<Dictionary<string, object>>();
        var bufferViews = new List<Dictionary<string, object>>();
        var buffers = new List<Dictionary<string, object>>();
        var nodes = new List<Dictionary<string, object>>();

        int accessorIndex = 0;
        int bufferViewIndex = 0;
        int bufferIndex = 0;

        foreach (var obj in objects)
        {
            if (obj.SceneObject.MeshBlueprint == null) continue;

            var vertices = obj.SceneObject.MeshBlueprint.Vertices;
            var indices = obj.SceneObject.MeshBlueprint.Indices;

            // Position accessor
            var positions = new List<float>();
            foreach (var v in vertices)
            {
                positions.Add(v.Position.X);
                positions.Add(v.Position.Y);
                positions.Add(v.Position.Z);
            }
            var positionBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes<float>(positions.ToArray());
            var positionBufferView = new Dictionary<string, object>
            {
                ["buffer"] = bufferIndex,
                ["byteOffset"] = 0,
                ["byteLength"] = positionBytes.Length,
                ["target"] = 34962 // ARRAY_BUFFER
            };
            bufferViews.Add(positionBufferView);
            var positionAccessor = new Dictionary<string, object>
            {
                ["bufferView"] = bufferViewIndex,
                ["componentType"] = 5126, // FLOAT
                ["count"] = vertices.Length,
                ["type"] = "VEC3",
                ["max"] = GetMax(positions),
                ["min"] = GetMin(positions)
            };
            accessors.Add(positionAccessor);
            int positionAccessorIndex = accessorIndex++;

            // Normal accessor
            var normals = new List<float>();
            foreach (var v in vertices)
            {
                normals.Add(v.Normal.X);
                normals.Add(v.Normal.Y);
                normals.Add(v.Normal.Z);
            }
            var normalBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes<float>(normals.ToArray());
            var normalBufferView = new Dictionary<string, object>
            {
                ["buffer"] = bufferIndex,
                ["byteOffset"] = positionBytes.Length,
                ["byteLength"] = normalBytes.Length,
                ["target"] = 34962
            };
            bufferViews.Add(normalBufferView);
            var normalAccessor = new Dictionary<string, object>
            {
                ["bufferView"] = bufferViewIndex + 1,
                ["componentType"] = 5126,
                ["count"] = vertices.Length,
                ["type"] = "VEC3"
            };
            accessors.Add(normalAccessor);
            int normalAccessorIndex = accessorIndex++;

            // Index accessor
            var indexBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes<uint>(indices);
            var indexBufferView = new Dictionary<string, object>
            {
                ["buffer"] = bufferIndex,
                ["byteOffset"] = positionBytes.Length + normalBytes.Length,
                ["byteLength"] = indexBytes.Length,
                ["target"] = 34963 // ELEMENT_ARRAY_BUFFER
            };
            bufferViews.Add(indexBufferView);
            var indexAccessor = new Dictionary<string, object>
            {
                ["bufferView"] = bufferViewIndex + 2,
                ["componentType"] = 5125, // UNSIGNED_INT
                ["count"] = indices.Length,
                ["type"] = "SCALAR"
            };
            accessors.Add(indexAccessor);
            int indexAccessorIndex = accessorIndex++;

            var prim = new Dictionary<string, object>
            {
                ["attributes"] = new Dictionary<string, int>
                {
                    ["POSITION"] = positionAccessorIndex,
                    ["NORMAL"] = normalAccessorIndex
                },
                ["indices"] = indexAccessorIndex,
                ["mode"] = 4 // TRIANGLES
            };

            meshes.Add(new Dictionary<string, object>
            {
                ["name"] = obj.Name,
                ["primitives"] = new List<Dictionary<string, object>> { prim }
            });

            nodes.Add(new Dictionary<string, object>
            {
                ["name"] = obj.Name,
                ["mesh"] = meshes.Count - 1,
                ["translation"] = new float[] { obj.PositionX, obj.PositionY, obj.PositionZ },
                ["scale"] = new float[] { obj.ScaleX, obj.ScaleY, obj.ScaleZ }
            });

            bufferViews.Add(positionBufferView);
            bufferViews.Add(normalBufferView);
            bufferViews.Add(indexBufferView);
            bufferViewIndex += 3;

            // Combine all buffer data
            var combinedBuffer = new List<byte>();
            combinedBuffer.AddRange(positionBytes);
            combinedBuffer.AddRange(normalBytes);
            combinedBuffer.AddRange(indexBytes);

            var buffer = new Dictionary<string, object>
            {
                ["byteLength"] = combinedBuffer.Count,
                ["uri"] = $"data:application/octet-stream;base64,{Convert.ToBase64String(combinedBuffer.ToArray())}"
            };
            buffers.Add(buffer);
        }

        gltf["asset"] = new Dictionary<string, int> { ["version"] = 2 };
        gltf["meshes"] = meshes;
        gltf["accessors"] = accessors;
        gltf["bufferViews"] = bufferViews;
        gltf["buffers"] = buffers;
        gltf["nodes"] = nodes;
        gltf["scenes"] = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object> { ["nodes"] = nodes.Select((n, i) => i).ToList() }
        };

        string json = JsonSerializer.Serialize(gltf, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }

    private static float[] GetMin(List<float> values)
    {
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        for (int i = 0; i < values.Count; i += 3)
        {
            if (values[i] < minX) minX = values[i];
            if (values[i + 1] < minY) minY = values[i + 1];
            if (values[i + 2] < minZ) minZ = values[i + 2];
        }
        return new[] { minX, minY, minZ };
    }

    private static float[] GetMax(List<float> values)
    {
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
        for (int i = 0; i < values.Count; i += 3)
        {
            if (values[i] > maxX) maxX = values[i];
            if (values[i + 1] > maxY) maxY = values[i + 1];
            if (values[i + 2] > maxZ) maxZ = values[i + 2];
        }
        return new[] { maxX, maxY, maxZ };
    }
}
