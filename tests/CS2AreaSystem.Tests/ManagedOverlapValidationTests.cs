using CS2AreaSystem.Core.Prefabs;
using Game.Tools;
using NUnit.Framework;
using Unity.Entities;

namespace CS2AreaSystem.Tests;

public class ManagedOverlapValidationTests
{
    private static readonly Entity Preview = new() { Index = 101, Version = 1 };
    private static readonly Entity Existing = new() { Index = 102, Version = 1 };
    private static readonly Entity Vanilla = new() { Index = 103, Version = 1 };
    private static bool Owned(Entity entity) => entity == Preview || entity == Existing;
    private static ErrorData Error(ErrorType type, Entity other, ErrorSeverity severity = ErrorSeverity.Error) =>
        new() { m_TempEntity = Preview, m_PermanentEntity = other, m_ErrorType = type, m_ErrorSeverity = severity };

    [Test] public void NativeOverlapBetweenManagedAreasBecomesWarningForBothEntities()
    {
        var result = ManagedOverlapValidation.FindOverlapOnlyEntities(new[] { Error(ErrorType.OverlapExisting, Existing) }, Owned);
        Assert.That(result, Is.EquivalentTo(new[] { Preview, Existing }));
    }
    [TestCase(ErrorType.InvalidShape)] [TestCase(ErrorType.ExceedsCityLimits)]
    public void StructuralOrCityLimitErrorsStillBlockEvenAlongsidePermittedOverlap(ErrorType type)
    {
        var result = ManagedOverlapValidation.FindOverlapOnlyEntities(new[] {
            Error(ErrorType.OverlapExisting, Existing), Error(type, Entity.Null) }, Owned);
        Assert.That(result, Does.Not.Contain(Preview));
    }
    [Test] public void VanillaAndUnidentifiedPairsAreNeverDowngraded()
    {
        foreach (var other in new[] { Vanilla, Entity.Null })
            Assert.That(ManagedOverlapValidation.FindOverlapOnlyEntities(new[] { Error(ErrorType.OverlapExisting, other) }, Owned), Is.Empty);
    }
    [Test] public void NoNativeEvidenceNeverAuthorizesRemovingAnUnknownError()
    {
        Assert.That(ManagedOverlapValidation.FindOverlapOnlyEntities(Array.Empty<ErrorData>(), Owned), Is.Empty);
    }
    [Test] public void ModuleVetoPreservesNativeErrors()
    {
        Assert.That(ManagedOverlapValidation.FindOverlapOnlyEntities(new[] { Error(ErrorType.OverlapExisting, Existing) },
            Owned, (_, _) => false), Is.Empty);
    }
    [Test] public void CancellationAndErrorsFromVanillaPreviewsRemainBlocking()
    {
        var cancelled = Error(ErrorType.OverlapExisting, Existing, ErrorSeverity.Cancel);
        Assert.That(ManagedOverlapValidation.FindOverlapOnlyEntities(new[] { cancelled }, Owned), Is.Empty);
        var unrelated = Error(ErrorType.OverlapExisting, Existing);
        unrelated.m_TempEntity = Vanilla;
        Assert.That(ManagedOverlapValidation.FindOverlapOnlyEntities(new[] { unrelated }, Owned), Is.Empty);
    }
}
