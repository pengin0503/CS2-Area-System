using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Core.Modules;
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
}
