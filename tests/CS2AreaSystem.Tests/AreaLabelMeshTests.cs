using System.Runtime.InteropServices;
using Colossal.Mathematics;
using CS2AreaSystem.Core.Labels;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
namespace CS2AreaSystem.Tests;
public class AreaLabelMeshTests {
 private static readonly Color32 White=new(255,255,255,255);
 private static IEnumerable<AreaLabelGlyphVertex> Quad(int material,float x,Color32 color)=>Enumerable.Range(0,4).Select(i=>new AreaLabelGlyphVertex{
  Position=new Vector3(x+i,i,0),Color=color,UV0=new Vector2(i,0),UV1=new Vector2(0,i),Material=material});
 private static AreaLabelLayout Layout(params IEnumerable<AreaLabelGlyphVertex>[] quads)=>
  new(quads.SelectMany(q=>q).ToArray(),new[]{new Bounds2(new float2(-1,-1),new float2(1,1))});
 [Test] public void QuadsBecomeTwoTrianglesPerMaterialSubMesh() {
  var data=AreaLabelMesh.Build(new[]{new AreaLabelInstance(new float3(10,0,20),White,Layout(Quad(1,0,White),Quad(0,100,White),Quad(1,200,White)))},2);
  Assert.That(data.SubMeshes.Select(s=>(s.firstVertex,s.vertexCount,s.indexStart,s.indexCount)),Is.EqualTo(new[]{(0,4,0,6),(4,8,6,12)}));
  Assert.That(data.Vertices,Has.Length.EqualTo(12));
  Assert.That(data.Indices,Is.EqualTo(new uint[]{0,1,2,2,3,0, 4,5,6,6,7,4, 8,9,10,10,11,8}));
  Assert.That(data.Vertices[0].Position.x,Is.EqualTo(100f),"material 0 glyphs come first");
  Assert.That(data.Vertices[4].Position.x,Is.EqualTo(0f));
  Assert.That(data.Vertices[8].Position.x,Is.EqualTo(200f),"glyph order is kept inside a material");
  Assert.That(data.SubMeshes.All(s=>s.topology==MeshTopology.Triangles),Is.True);
 }
 [Test] public void VertexCarriesLabelPositionAndTintedColor() {
  var data=AreaLabelMesh.Build(new[]{
   new AreaLabelInstance(new float3(1,2,3),new Color32(128,128,128,128),Layout(Quad(0,0,new Color32(255,128,64,200)))),
   new AreaLabelInstance(new float3(7,8,9),White,Layout(Quad(0,0,White)))},1);
  Assert.That(data.Vertices[0].Color,Is.EqualTo(new Color32(127,64,32,100)),"(glyph * tint) >> 8 like FillNameDataJob");
  Assert.That(data.Vertices[0].UV2,Is.EqualTo(new Vector3(1,2,3)));
  Assert.That(data.Vertices[2].UV0,Is.EqualTo(new Vector2(2,0)));
  Assert.That(data.Vertices[3].UV1,Is.EqualTo(new Vector2(0,3)));
  Assert.That(data.Vertices[4].UV2,Is.EqualTo(new Vector3(7,8,9)));
  Assert.That(data.Vertices[4].Color,Is.EqualTo(new Color32(254,254,254,254)));
 }
 [Test] public void VertexLayoutMatchesVanillaLabelVertexData() {
  Assert.That(Marshal.SizeOf<AreaLabelVertex>(),Is.EqualTo(44));
  var layout=AreaLabelVertex.Layout();
  Assert.That(layout.Select(a=>(a.attribute,a.format,a.dimension,a.stream)),Is.EqualTo(new[]{
   (VertexAttribute.Position,VertexAttributeFormat.Float32,3,0),(VertexAttribute.Color,VertexAttributeFormat.UNorm8,4,0),
   (VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2,0),(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,2,0),
   (VertexAttribute.TexCoord2,VertexAttributeFormat.Float32,3,0)}));
 }
 [Test] public void EmptyInputBuildsEmptySubMeshes() {
  var data=AreaLabelMesh.Build(Array.Empty<AreaLabelInstance>(),2);
  Assert.That(data.Vertices,Is.Empty); Assert.That(data.Indices,Is.Empty);
  Assert.That(data.SubMeshes.Select(s=>(s.vertexCount,s.indexCount)),Is.EqualTo(new[]{(0,0),(0,0)}));
  Assert.That(AreaLabelMesh.Build(new[]{new AreaLabelInstance(float3.zero,White,Layout())},1).Vertices,Is.Empty,"a blank name draws nothing");
 }
 [Test] public void SnapshotDetectsRenameMoveHighlightAndRemoval() {
  Entity a=new(){Index=1,Version=1}, b=new(){Index=2,Version=1};
  var current=new[]{new AreaLabelEntry(a,"Park 1",new float3(1,0,1),false),new AreaLabelEntry(b,"Park 2",new float3(5,0,5),false)};
  Assert.That(AreaLabelEntry.Equivalent(current,current.ToArray()),Is.True);
  Assert.That(AreaLabelEntry.Equivalent(current,new[]{current[0],new AreaLabelEntry(b,"Park Area",new float3(5,0,5),false)}),Is.False,"renamed (or name cleared)");
  Assert.That(AreaLabelEntry.Equivalent(current,new[]{current[0],new AreaLabelEntry(b,"Park 2",new float3(6,0,5),false)}),Is.False,"moved");
  Assert.That(AreaLabelEntry.Equivalent(current,new[]{current[0],new AreaLabelEntry(b,"Park 2",new float3(5,0,5),true)}),Is.False,"highlighted");
  Assert.That(AreaLabelEntry.Equivalent(current,new[]{current[0]}),Is.False,"removed");
  Assert.That(AreaLabelEntry.Equivalent(null,Array.Empty<AreaLabelEntry>()),Is.False,"no previous build");
 }
}
