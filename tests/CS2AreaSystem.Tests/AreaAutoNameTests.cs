using CS2AreaSystem.Core.Labels;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaAutoNameTests {
 private static readonly Entity a=new(){Index=1,Version=1}, b=new(){Index=2,Version=1};
 [Test] public void NextUsesTheSmallestFreeNumber() {
  Assert.That(AreaAutoName.Next("公園エリア",new[]{"公園エリア 1","公園エリア 3"}),Is.EqualTo("公園エリア 2"));
  Assert.That(AreaAutoName.Next("公園エリア",Array.Empty<string>()),Is.EqualTo("公園エリア 1"));
  Assert.That(AreaAutoName.Next("公園エリア",new[]{"産業エリア 1","Central Park",null!}),Is.EqualTo("公園エリア 1"),"other types and player names do not block");
 }
 [Test] public void NextFallsBackForMissingTypeName() {
  Assert.That(AreaAutoName.Next(null,Array.Empty<string>()),Is.EqualTo("Area 1"));
  Assert.That(AreaAutoName.Next("  ",new[]{"Area 1"}),Is.EqualTo("Area 2"));
 }
 [Test] public void NextIgnoresNearMisses() {
  Assert.That(AreaAutoName.Next("公園エリア",new[]{"公園エリア 01","公園エリア 1 ","公園エリア1"}),Is.EqualTo("公園エリア 1"));
 }
 [Test] public void SelectionOnlyWhileAreaToolIsActive() {
  Assert.That(AreaAutoName.SelectionAfterPlacement(false,new[]{a,b}),Is.EqualTo(Entity.Null));
  Assert.That(AreaAutoName.SelectionAfterPlacement(true,new[]{a,b}),Is.EqualTo(b),"the last placed Area wins");
  Assert.That(AreaAutoName.SelectionAfterPlacement(true,Array.Empty<Entity>()),Is.EqualTo(Entity.Null));
 }
}
