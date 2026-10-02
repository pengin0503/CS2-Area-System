using System.Collections.Generic;
using System.Runtime.InteropServices;
using Colossal.Mathematics;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
namespace CS2AreaSystem.Core.Labels
{
    // Same layout as the private vanilla AreaBufferSystem.LabelVertexData, so the district name material renders it:
    // glyph position in label space, glyph colour, the two TextMeshPro UV sets and the label world position in UV2.
    [StructLayout(LayoutKind.Sequential)]
    public struct AreaLabelVertex
    {
        public Vector3 Position;
        public Color32 Color;
        public Vector2 UV0;
        public Vector2 UV1;
        public Vector3 UV2;
        public static VertexAttributeDescriptor[] Layout() => new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3)
        };
    }
    // One TextMeshPro glyph vertex (glyphs come in quads of four), with the index of the name material for its font atlas.
    public struct AreaLabelGlyphVertex
    {
        public Vector3 Position;
        public Color32 Color;
        public Vector2 UV0;
        public Vector2 UV1;
        public int Material;
    }
    // The laid-out text of one name: glyph quads and one extents box per line (the click targets, like LabelExtents).
    public sealed class AreaLabelLayout
    {
        public IReadOnlyList<AreaLabelGlyphVertex> Vertices { get; }
        public IReadOnlyList<Bounds2> Lines { get; }
        public AreaLabelLayout(IReadOnlyList<AreaLabelGlyphVertex> vertices, IReadOnlyList<Bounds2> lines)
        {
            Vertices = vertices ?? new AreaLabelGlyphVertex[0];
            Lines = lines ?? new Bounds2[0];
        }
    }
    public readonly struct AreaLabelInstance
    {
        public float3 Position { get; }
        public Color32 Tint { get; }
        public AreaLabelLayout Layout { get; }
        public AreaLabelInstance(float3 position, Color32 tint, AreaLabelLayout layout) { Position = position; Tint = tint; Layout = layout; }
    }
    // What one frame's labels show; the mesh is rebuilt only when this changes.
    public readonly struct AreaLabelEntry
    {
        public Entity Area { get; }
        public string Name { get; }
        public float3 Position { get; }
        public bool Highlighted { get; }
        public AreaLabelEntry(Entity area, string name, float3 position, bool highlighted) { Area = area; Name = name; Position = position; Highlighted = highlighted; }
        public static bool Equivalent(IReadOnlyList<AreaLabelEntry> a, IReadOnlyList<AreaLabelEntry> b)
        {
            if (a == null || b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Area != b[i].Area || a[i].Name != b[i].Name || !a[i].Position.Equals(b[i].Position) || a[i].Highlighted != b[i].Highlighted) return false;
            return true;
        }
    }
    public sealed class AreaLabelMeshData
    {
        public AreaLabelVertex[] Vertices;
        public uint[] Indices;
        public SubMeshDescriptor[] SubMeshes;
    }
    public static class AreaLabelMesh
    {
        // Count-then-fill like AreaBufferSystem.FillNameDataJob: one submesh per name material, two triangles per glyph quad.
        public static AreaLabelMeshData Build(IReadOnlyList<AreaLabelInstance> labels, int materialCount)
        {
            var counts = new int[materialCount];
            foreach (var label in labels)
                for (int i = 0; i + 3 < label.Layout.Vertices.Count; i += 4) counts[label.Layout.Vertices[i].Material] += 4;
            var subMeshes = new SubMeshDescriptor[materialCount];
            int total = 0;
            for (int m = 0; m < materialCount; m++)
            {
                subMeshes[m] = new SubMeshDescriptor(total / 4 * 6, counts[m] / 4 * 6, MeshTopology.Triangles) { firstVertex = total, vertexCount = counts[m] };
                total += counts[m];
                counts[m] = 0;
            }
            var data = new AreaLabelMeshData { Vertices = new AreaLabelVertex[total], Indices = new uint[total / 4 * 6], SubMeshes = subMeshes };
            foreach (var label in labels)
            {
                var glyphs = label.Layout.Vertices;
                for (int i = 0; i + 3 < glyphs.Count; i += 4)
                {
                    int material = glyphs[i].Material;
                    int vertex = subMeshes[material].firstVertex + counts[material];
                    int index = subMeshes[material].indexStart + counts[material] / 4 * 6;
                    counts[material] += 4;
                    data.Indices[index] = (uint)vertex;
                    data.Indices[index + 1] = (uint)(vertex + 1);
                    data.Indices[index + 2] = (uint)(vertex + 2);
                    data.Indices[index + 3] = (uint)(vertex + 2);
                    data.Indices[index + 4] = (uint)(vertex + 3);
                    data.Indices[index + 5] = (uint)vertex;
                    for (int k = 0; k < 4; k++)
                    {
                        var glyph = glyphs[i + k];
                        data.Vertices[vertex + k] = new AreaLabelVertex
                        {
                            Position = glyph.Position,
                            Color = new Color32((byte)(glyph.Color.r * label.Tint.r >> 8), (byte)(glyph.Color.g * label.Tint.g >> 8),
                                (byte)(glyph.Color.b * label.Tint.b >> 8), (byte)(glyph.Color.a * label.Tint.a >> 8)),
                            UV0 = glyph.UV0,
                            UV1 = glyph.UV1,
                            UV2 = label.Position
                        };
                    }
                }
            }
            return data;
        }
    }
}
