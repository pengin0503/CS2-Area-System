using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Persistence;
using CS2AreaSystem.Tests.Support;
using Game.Areas;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
namespace CS2AreaSystem.Tests;

public class AreaNativeReadinessTests {
    [Test] public void CoordinatorCompletesNativeWriterBeforeInspectingVersionAndRecovering() {
        var update = typeof(AreaLoadCoordinatorSystem).GetMethod("OnUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var calls = IlCallScanner.Find(update, _ => true);
        Assert.That(calls.FindIndex(c => c.Target.Name == "GetSearchTree"), Is.LessThan(calls.FindIndex(c => c.Target.Name == "Complete")));
        Assert.That(calls.FindIndex(c => c.Target.Name == "Complete"), Is.LessThan(calls.FindIndex(c => c.Target.Name == "get_LastSystemVersion")));
        Assert.That(calls.FindIndex(c => c.Target.DeclaringType == typeof(AreaNativeReadiness) && c.Target.Name == "Observe"),
            Is.LessThan(calls.FindIndex(c => c.Target.Name == "TryRecover")));
        Assert.That(calls.Any(c => c.Target.DeclaringType == typeof(AreaQuarantine) && c.Target.Name == "Add"), Is.True);
    }
    private static readonly Entity Healthy = new() { Index = 1, Version = 1 }, Broken = new() { Index = 2, Version = 1 };
    private static AreaNativeGeometryEvidence Geometry(float offset = 0, uint nodeVersion = 0, uint triangleVersion = 0) {
        var nodes = new[] { new Node(new float3(offset, 0, 0), 0), new Node(new float3(offset + 1, 0, 0), 0), new Node(new float3(offset, 0, 1), 0) };
        var triangles = new[] { new Triangle { m_Indices = new int3(0, 1, 2) } };
        return AreaNativeGeometryEvidence.Read(true, 3, 1, i => nodes[i], i => triangles[i], nodeVersion, triangleVersion);
    }
    [Test] public void OneReadyAreaWaitsForMissingPeerThenRecoversWithOnlyThePeerQuarantined() {
        var readiness = new AreaNativeReadiness(); readiness.BeginLoad(100);
        var identity = new AreaIdentityLifecycle(); identity.BeginLoad();
        var sequence = new AreaLoadSequence(); var operations = new List<string>(); int diagnostics = 0;
        void Recover(uint version) {
            bool canRecover = true;
            foreach (var (area, ready) in new[] { (Healthy, true), (Broken, false) }) {
                if (identity.Quarantine.Contains(area)) continue;
                var result = readiness.Observe(area, ready, version, Geometry());
                if (result == AreaNativeReadinessResult.Waiting) canRecover = false;
                if (result == AreaNativeReadinessResult.Quarantine && identity.Quarantine.Add(area, AreaQuarantineReason.NativeGeometryUnavailable)) diagnostics++;
            }
            sequence.TryRecover(canRecover,
                () => { identity.Observe(new[] { new AreaIdentityRecord(Healthy, 7), new AreaIdentityRecord(Broken, 900) }); operations.Add("ids"); operations.Add("saved validation"); },
                () => { Assert.That(identity.Quarantine.Contains(Healthy), Is.False); Assert.That(identity.Quarantine.Contains(Broken), Is.True); operations.Add("membership"); },
                () => operations.Add("activation"));
        }
        Recover(101); Assert.That(sequence.Ready, Is.False); Assert.That(diagnostics, Is.Zero);
        Recover(102); Recover(103);
        Assert.That(sequence.Ready, Is.True); Assert.That(diagnostics, Is.EqualTo(1), "no repeated diagnostic");
        Assert.That(identity.Quarantine.Count, Is.EqualTo(1));
        Assert.That(identity.StableIds.Allocate(), Is.EqualTo(901ul), "quarantined saved identity is still reserved");
        Assert.That(operations, Is.EqualTo(new[] { "ids", "saved validation", "membership", "activation" }));
    }
    [Test] public void RepeatedPollingOfTheSameNativePassNeverActsAsATimeout() {
        var readiness = new AreaNativeReadiness(); readiness.BeginLoad(100);
        for (int poll = 0; poll < 1000; poll++)
            Assert.That(readiness.Observe(Broken, false, 101, Geometry()), Is.EqualTo(AreaNativeReadinessResult.Waiting));
        Assert.That(readiness.Observe(Broken, false, 102, Geometry()), Is.EqualTo(AreaNativeReadinessResult.Quarantine));
    }
    [Test] public void GeometryChangesAndUnprocessedWritesKeepNormalInitializationWaiting() {
        var readiness = new AreaNativeReadiness(); readiness.BeginLoad(100);
        Assert.That(readiness.Observe(Broken, false, 101, Geometry()), Is.EqualTo(AreaNativeReadinessResult.Waiting));
        Assert.That(readiness.Observe(Broken, false, 102, Geometry(4)), Is.EqualTo(AreaNativeReadinessResult.Waiting));
        Assert.That(readiness.Observe(Broken, true, 103, Geometry(4, 104)), Is.EqualTo(AreaNativeReadinessResult.Waiting), "search predates latest geometry");
        Assert.That(readiness.Observe(Broken, true, 105, Geometry(4, 104)), Is.EqualTo(AreaNativeReadinessResult.Ready));
    }
    [Test] public void UnrelatedChunkWritesDoNotRestartPersistentMissingAreaEvidence() {
        var readiness = new AreaNativeReadiness(); readiness.BeginLoad(100);
        Assert.That(readiness.Observe(Broken, false, 101, Geometry(0, 99)), Is.EqualTo(AreaNativeReadinessResult.Waiting));
        Assert.That(readiness.Observe(Broken, false, 103, Geometry(0, 102)), Is.EqualTo(AreaNativeReadinessResult.Quarantine),
            "chunk versions can advance for a different Area; unchanged actual geometry remains the same evidence");
    }
    [Test] public void NativeVersionWrapAndANewLoadCannotReuseMissingEvidence() {
        var readiness = new AreaNativeReadiness(); readiness.BeginLoad(uint.MaxValue - 2);
        Assert.That(readiness.Observe(Broken, false, uint.MaxValue - 1, Geometry(0, uint.MaxValue - 3, uint.MaxValue - 3)), Is.EqualTo(AreaNativeReadinessResult.Waiting));
        Assert.That(readiness.Observe(Broken, false, 1, Geometry(0, uint.MaxValue - 3, uint.MaxValue - 3)), Is.EqualTo(AreaNativeReadinessResult.Quarantine));
        readiness.BeginLoad(1);
        Assert.That(readiness.Observe(Broken, false, 1, Geometry()), Is.EqualTo(AreaNativeReadinessResult.Waiting));
        Assert.That(readiness.Observe(Broken, false, 2, Geometry()), Is.EqualTo(AreaNativeReadinessResult.Waiting));
    }
    [Test] public void NormalLoadWaitsForANativePassThenRecoversExactlyOnce() {
        var readiness = new AreaNativeReadiness(); readiness.BeginLoad(100);
        var sequence = new AreaLoadSequence(); int recovered = 0;
        foreach (uint version in new uint[] { 100, 101, 102 }) {
            var decision = readiness.Observe(Healthy, true, version, Geometry());
            sequence.TryRecover(decision == AreaNativeReadinessResult.Ready, () => recovered++, () => { }, () => { });
            if (version == 100) Assert.That(recovered, Is.Zero);
        }
        Assert.That(recovered, Is.EqualTo(1));
    }
    [Test] public void MissingEmptyAndInvalidGeometryHaveSpecificEvidenceWithoutChangingInputs() {
        var missing = AreaNativeGeometryEvidence.Read(true, -1, -1, null, null, 0, 0);
        Assert.That(missing.Problem, Is.EqualTo(AreaNativeGeometryProblem.MissingBuffers));
        var empty = AreaNativeGeometryEvidence.Read(true, 0, 0, null, null, 0, 0);
        Assert.That(empty.Problem, Is.EqualTo(AreaNativeGeometryProblem.EmptyGeometry));
        var node = new Node(new float3(float.NaN, 0, 0), 0);
        var invalid = AreaNativeGeometryEvidence.Read(true, 3, 1, _ => node, _ => new Triangle { m_Indices = new int3(0, 1, 5) }, 0, 0);
        Assert.That(invalid.Problem, Is.EqualTo(AreaNativeGeometryProblem.InvalidGeometry));
        Assert.That(float.IsNaN(node.m_Position.x), Is.True, "diagnosis does not repair saved geometry");
        var readiness = new AreaNativeReadiness(); readiness.BeginLoad(100);
        Assert.That(readiness.Observe(Broken, true, 101, empty), Is.EqualTo(AreaNativeReadinessResult.Waiting), "native count entry alone does not imply usable triangles");
        Assert.That(readiness.Observe(Broken, true, 102, empty), Is.EqualTo(AreaNativeReadinessResult.Quarantine));
    }
}
