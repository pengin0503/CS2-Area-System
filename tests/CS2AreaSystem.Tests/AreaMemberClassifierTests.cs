using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using CS2AreaSystem.Core.Persistence;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class AreaMemberClassifierTests {
 private sealed class Classifier : IAreaMemberClassifier {
  public AreaMemberRole Role; public bool Fails;
  public AreaMemberRole Classify(Entity member,Entity area){if(Fails)throw new InvalidOperationException("module failure");return Role;}
 }
 [Test] public void ModuleControlsClassificationAndUnavailableModuleKeepsGeometricRelation() {
  var classifier=new Classifier{Role=AreaMemberRole.None};
  Assert.That(AreaMembershipClassification.Include(classifier,default,default,_=>{}),Is.False);
  classifier.Role=AreaMemberRole.Member;
  Assert.That(AreaMembershipClassification.Include(classifier,default,default,_=>{}),Is.True);
  Assert.That(AreaMembershipClassification.Include(null,default,default,_=>{}),Is.True);
 }
 [Test] public void ClassifierExceptionIsIsolatedToItsRelation() {
  Exception observed=null;
  Assert.That(AreaMembershipClassification.Include(new Classifier{Fails=true},default,default,e=>observed=e),Is.False);
  Assert.That(observed?.Message,Is.EqualTo("module failure"));
 }
 private sealed class Module : IAreaModule {
  public bool GetterFails; public IAreaMemberClassifier Classifier;
  public IAreaMemberClassifier MemberClassifier => GetterFails ? throw new InvalidOperationException("getter failure") : Classifier;
  public AreaTypeDescriptor DescribeType() => throw new NotSupportedException();
  public bool ValidateActivation(Entity area, in CS2AreaSystem.Core.Modules.AreaActivationContext context) => true;
  public IAreaPanelProvider PanelProvider => null;
  public bool AllowsOverlapWith(int otherTypeId) => true;
  public void OnAreaCreated(Entity area) { } public void OnAreaActivated(Entity area) { }
  public void OnAreaSuspended(Entity area) { } public void OnAreaDeleted(Entity area) { }
 }
 [Test] public void ClassifierGetterExceptionIsIsolatedAndReported() {
  Exception observed=null;
  Assert.That(AreaMembershipClassification.IncludeModule(new Module{GetterFails=true},default,default,e=>observed=e),Is.False);
  Assert.That(observed?.Message,Is.EqualTo("getter failure"));
  Assert.That(AreaMembershipClassification.IncludeModule(null,default,default,_=>Assert.Fail()),Is.True,"unknown module keeps geometry relation");
 }
 [Test] public void FailingModuleDoesNotRemoveHealthyRelationsForTheSameMember() {
  var member=new Entity{Index=1,Version=1};
  var areas=new[]{
   new ContainingArea(new Entity{Index=10,Version=1},10,1,new Module{GetterFails=true}),
   new ContainingArea(new Entity{Index=20,Version=1},20,2,new Module{Classifier=new Classifier{Role=AreaMemberRole.Member}}),
   new ContainingArea(new Entity{Index=30,Version=1},30,9999,null)
  };
  var failed=new List<int>();
  var relations=AreaMembershipClassification.Collect(areas,member,(type,_)=>failed.Add(type));
  Assert.That(relations.Select(r=>r.AreaStableId),Is.EqualTo(new ulong[]{20,30}));
  Assert.That(failed,Is.EqualTo(new[]{1}));
 }
 [Test] public void LoadRecoveryCompletesWhenAModuleGetterThrows() {
  var sequence=new AreaLoadSequence();
  var index=new AreaMembershipIndex();
  var member=new Entity{Index=1,Version=1};
  var areas=new[]{
   new ContainingArea(default,10,1,new Module{GetterFails=true}),
   new ContainingArea(default,20,2,new Module{Classifier=new Classifier{Role=AreaMemberRole.Member}})
  };
  Assert.That(sequence.TryRecover(true,()=>{},()=>index.ReplaceMembership(member,AreaMembershipClassification.Collect(areas,member,(_,_)=>{})),()=>{}),Is.True);
  Assert.That(sequence.Ready,Is.True);
  Assert.That(index.GetMembers(20),Does.Contain(member));
  Assert.That(index.GetMembers(10),Is.Empty);
 }
}
