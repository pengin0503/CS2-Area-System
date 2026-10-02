using CS2AreaSystem.Core.Modules;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Wave A2: diagnostic target keys are opaque and resolve only against the module's current candidates.
public class AreaDiagnosticTargetTests {
 private static Entity E(int index, int version = 1) => new() { Index = index, Version = version };
 [Test] public void CurrentCandidateResolves() {
  var target = E(70, 3);
  Assert.That(AreaDiagnosticTargetKey.TryResolve(AreaDiagnosticTargetKey.Format(target), new[] { E(5), target }, out var resolved), Is.True);
  Assert.That(resolved, Is.EqualTo(target));
 }
 [Test] public void SameIndexWithDifferentVersionFails() {
  var key = AreaDiagnosticTargetKey.Format(E(70, 3));
  Assert.That(AreaDiagnosticTargetKey.TryResolve(key, new[] { E(70, 4) }, out var resolved), Is.False, "a recycled entity slot is another entity");
  Assert.That(resolved, Is.EqualTo(Entity.Null));
 }
 [TestCase(null)] [TestCase("")] [TestCase("70")] [TestCase("70.")] [TestCase(".3")] [TestCase("x.y")] [TestCase("70.3.1")] [TestCase("-70.3")] [TestCase(" 70.3")]
 public void MalformedKeyFails(string? key) {
  Assert.That(AreaDiagnosticTargetKey.TryResolve(key!, new[] { E(70, 3) }, out var resolved), Is.False);
  Assert.That(resolved, Is.EqualTo(Entity.Null));
 }
 [Test] public void ValidLookingKeyAbsentFromTheAllowedListFails() {
  Assert.That(AreaDiagnosticTargetKey.TryResolve(AreaDiagnosticTargetKey.Format(E(71)), new[] { E(70), E(72) }, out _), Is.False,
   "parsing a raw Entity key is never authorization");
  Assert.That(AreaDiagnosticTargetKey.TryResolve(AreaDiagnosticTargetKey.Format(E(71)), Array.Empty<Entity>(), out _), Is.False);
  Assert.That(AreaDiagnosticTargetKey.TryResolve(AreaDiagnosticTargetKey.Format(E(71)), null!, out _), Is.False);
 }
 [Test] public void NullEntityIsNeverATarget() {
  Assert.That(AreaDiagnosticTargetKey.TryResolve(AreaDiagnosticTargetKey.Format(Entity.Null), new[] { Entity.Null }, out _), Is.False);
 }
}
