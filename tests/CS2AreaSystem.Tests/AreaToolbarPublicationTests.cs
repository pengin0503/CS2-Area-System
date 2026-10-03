using System.Reflection;
using CS2AreaSystem.Core.Prefabs;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaToolbarPublicationTests {
 static bool Publish(AreaTypeReadiness readiness, int type, bool attach, Action<int> publish) {
  var method = typeof(AreaTypeReadiness).GetMethod("PublishToolbarIfReady");
  Assert.That(method, Is.Not.Null, "registration and toolbar publication must have distinct readiness gates");
  return (bool)method.Invoke(readiness, new object[] { type, attach, publish });
 }
 [Test] public void RegisteredButNotReadyTypeDoesNotPublishOrChangeToolbarVersion() {
  var readiness = new AreaTypeReadiness(); var published = new HashSet<int>(); int version = 0;
  void Mark(int type) { if (published.Add(type)) version++; }
  Assert.That(AreaPrefabRegistrationPolicy.ShouldAttempt(true, true), Is.False, "registered prefab needs no duplicate AddPrefab");
  readiness.Set(1, false); readiness.Set(2, true);
  Assert.That(Publish(readiness, 1, true, Mark), Is.False);
  Assert.That(version, Is.Zero); Assert.That(published, Is.Empty);
  Assert.That(Publish(readiness, 2, true, Mark), Is.True);
  Assert.That(version, Is.EqualTo(1)); Assert.That(published, Is.EquivalentTo(new[] { 2 }));
  readiness.Set(1, true);
  Assert.That(Publish(readiness, 1, true, Mark), Is.True);
  Assert.That(version, Is.EqualTo(2));
  Publish(readiness, 1, true, Mark);
  Assert.That(version, Is.EqualTo(2), "a ready type is still only registered once");
 }
 [Test] public void ToolbarFailureStillRegistersCorePrefabsWithoutPublishing() {
  var toolbar = new AreaToolbarExposureState(); AreaToolbarDecision decision = default;
  for (int i = 0; i < AreaToolbarExposureState.MaxFailedAttempts; i++) decision = toolbar.Update(() => false);
  Assert.That(decision.RegisterPrefabs, Is.True); Assert.That(decision.AttachToolbar, Is.False);
  var readiness = new AreaTypeReadiness(); readiness.Set(1, true); int version = 0;
  Assert.That(Publish(readiness, 1, decision.AttachToolbar, _ => version++), Is.False);
  Assert.That(version, Is.Zero);
 }
 [Test] public void NativeRegistrationNeverPublishesBeforeReadinessGate() {
  const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
  var type = typeof(AreaPrefabRegistrationSystem);
  var register = type.GetMethod("EnsurePrefabRegistered", flags);
  Assert.That(IlCallScanner.Find(register, m => m.Name == "MarkToolbarTypeRegistered"), Is.Empty,
   "AddPrefab success and TryGetEntity are registration, not readiness");
  var update = type.GetMethod("OnUpdate", flags);
  var ready = IlCallScanner.Find(update, m => m.Name == "IsPrefabReady");
  var publish = IlCallScanner.Find(update, m => m.Name == "PublishToolbarIfReady");
  Assert.That(publish, Has.Count.EqualTo(1)); Assert.That(ready, Has.Count.EqualTo(1));
  Assert.That(publish.Single().Offset, Is.GreaterThan(ready.Single().Offset));
 }
}
