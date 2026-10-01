using CS2AreaSystem.Core.Prefabs;
using Game.Tools;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// #60: the full native re-validation runs only for a preview whose bounds touch another Area it could be permitted to
// overlap; any other preview keeps exactly the native result, so re-validating it could change nothing.
public class ManagedOverlapGateTests
{
    private static readonly Entity Preview = new() { Index = 101, Version = 1 };
    private static readonly Entity Managed = new() { Index = 102, Version = 1 };
    private static readonly Entity Vanilla = new() { Index = 103, Version = 1 };
    private static readonly Entity Hidden = new() { Index = 104, Version = 1 };
    private static readonly Entity Lot = new() { Index = 105, Version = 1 };
    private static bool Permits(Entity a, Entity b) => (a == Preview && (b == Managed || b == Hidden)) || (b == Preview && (a == Managed || a == Hidden));
    private static bool Skip(Entity e) => e == Hidden;
    private static Entity TopLevel(Entity e) => e == Lot ? Vanilla : e;
    private static bool Gate(params Entity[] nearby) => ManagedOverlapGate.MayHavePermittedOverlap(Preview, nearby, Skip, TopLevel, Permits);
    [Test] public void NothingNearbyNeedsNoRevalidation() => Assert.That(Gate(), Is.False);
    [Test] public void ThePreviewItselfIsNotAPartner() => Assert.That(Gate(Preview, Preview), Is.False);
    [Test] public void VanillaNeighboursKeepTheNativeResultWithoutRevalidation() => Assert.That(Gate(Vanilla, Lot), Is.False);
    [Test] public void HiddenAreasAreIgnoredLikeNativeValidationDoes() => Assert.That(Gate(Hidden), Is.False, "an edited original is hidden");
    [Test] public void APermittedManagedNeighbourNeedsRevalidation() => Assert.That(Gate(Vanilla, Managed), Is.True);
    [Test] public void ErrorsCarryTheTopLevelOwnerSoTheGateResolvesItToo()
    {
        Assert.That(ManagedOverlapGate.MayHavePermittedOverlap(Preview, new[] { Lot }, Skip, e => e == Lot ? Managed : e, Permits), Is.True);
    }
    [Test] public void ReusesNoStateBetweenPreviews()
    {
        Assert.That(Gate(Managed), Is.True);
        Assert.That(Gate(Vanilla), Is.False);
    }
    [Test] public void SystemGatesTheFullValidation()
    {
        var source = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../src/CS2AreaSystem/Core/Prefabs/ManagedAreaOverlapSystem.cs"));
        int gate = source.IndexOf("ManagedOverlapGate.MayHavePermittedOverlap(", StringComparison.Ordinal);
        int full = source.IndexOf("ApplyManagedPolicy(", source.IndexOf("protected override void OnUpdate()", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.That(gate, Is.GreaterThan(0));
        Assert.That(source, Does.Contain("if (!AnyPreviewMayOverlap(entities)) { SkippedValidations++; return; }"));
        Assert.That(source, Does.Contain("FullValidations++"));
        Assert.That(full, Is.GreaterThan(0));
    }
}
