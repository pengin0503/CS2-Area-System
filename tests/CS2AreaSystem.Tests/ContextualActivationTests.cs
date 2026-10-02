using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Modules.Tourism;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class ContextualActivationTests
{
 private sealed class Store : IAreaAnchorStore {
  public readonly Dictionary<Entity,IReadOnlyList<AreaAnchorLink>> Links = new();
  public IReadOnlyList<AreaAnchorLink> Read(Entity a)=>Links.TryGetValue(a,out var v)?v:Array.Empty<AreaAnchorLink>();
  public void Write(Entity a,IReadOnlyList<AreaAnchorLink> v)=>Links[a]=v.ToArray();
 }
 private sealed class Multi : AnchorAreaModule {
  public Multi(AreaAnchorService anchors):base(AreaTypeId.Park,anchors,AreaAnchorRole.Headquarters,AreaAnchorRole.Entrance){}
  public override bool CanAssignAnchor(Entity area,AreaAnchorRole role,EntityManager entities)=>role==AreaAnchorRole.Headquarters;
 }
 [Test] public void EveryDeclaredRoleMustHaveValidEvidenceAndTourismNeedsNone() {
  var a=new Entity{Index=1,Version=1};var h=new Entity{Index=2,Version=1};var e=new Entity{Index=3,Version=1};
  var store=new Store();var anchors=new AreaAnchorService(store,x=>x!=Entity.Null,(x,y,r)=>true);var module=new Multi(anchors);
  Assert.That(module.AnchorRoles,Is.EqualTo(new[]{AreaAnchorRole.Headquarters,AreaAnchorRole.Entrance}));
  Assert.That(module.ValidateActivation(a,default),Is.False);
  anchors.Assign(a,h,AreaAnchorRole.Headquarters);Assert.That(module.ValidateActivation(a,default),Is.False);
  anchors.Assign(a,e,AreaAnchorRole.Entrance);Assert.That(module.ValidateActivation(a,default),Is.True);
  anchors.Remove(a,h,AreaAnchorRole.Headquarters);Assert.That(module.ValidateActivation(a,default),Is.False);
  Assert.That(new TourismAreaModule().ValidateActivation(a,default),Is.True);
 }
 [Test] public void AssignmentDisabledRoleStillValidatesPersistedLinksAndUndeclaredRoleIsRejected() {
  var a=new Entity{Index=1,Version=1};var h=new Entity{Index=2,Version=1};var e=new Entity{Index=3,Version=1};
  var store=new Store();Multi module=null;
  var anchors=new AreaAnchorService(store,x=>x!=Entity.Null,
   (x,y,r)=>AreaAnchorEligibility.AllowsRole(module,x,r,default,false),
   (x,y,r)=>AreaAnchorEligibility.AllowsRole(module,x,r,default,true));module=new Multi(anchors);
  Assert.That(anchors.Assign(a,e,AreaAnchorRole.Entrance),Is.False);
  Assert.That(AreaAnchorEligibility.AllowsRole(module,a,(AreaAnchorRole)99,default,true),Is.False);
  store.Links[a]=new[]{new AreaAnchorLink{Anchor=h,Role=AreaAnchorRole.Headquarters},new AreaAnchorLink{Anchor=e,Role=AreaAnchorRole.Entrance}};
  Assert.That(module.ValidateActivation(a,default),Is.True);
  Assert.That(anchors.GetAnchorLinks(a,AreaAnchorRole.Entrance).Single().Valid,Is.True);
 }
}
