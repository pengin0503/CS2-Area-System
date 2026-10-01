using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class ParkLeisureObservationTests {
 private static readonly Entity building = new() { Index = 1, Version = 1 }, tenant = new() { Index = 2, Version = 1 };
 private static ParkLeisureProviderState Provider(Entity entity) => new() { Entity = entity, HasPrefab = true, HasLeisureData = true };
 [Test] public void TenantWithoutLeisureDataDoesNotBorrowItsBuildingsProvider() {
  var tenantState = Provider(tenant); tenantState.HasLeisureData = false;
  Assert.That(ParkLeisureObservation.TryResolveProvider(tenant, building, building, tenantState, Provider(building), out _), Is.False);
  tenantState.HasPrefab = false;
  Assert.That(ParkLeisureObservation.TryResolveProvider(tenant, building, building, tenantState, Provider(building), out var actual), Is.True);
  Assert.That(actual, Is.EqualTo(building));
 }
 [Test] public void LeisureIntentWithoutResolvedTargetOrProviderIsNotAnAdmission() {
  var provider = Provider(building);
  Assert.That(ParkLeisureObservation.TryResolveProvider(Entity.Null, building, Entity.Null, default, provider, out _), Is.False);
  Assert.That(ParkLeisureObservation.TryResolveProvider(tenant, building, Entity.Null, default, default, out _), Is.False);
 }
 [Test] public void TenantProviderIsUsedOnlyWhenItRentsTheCurrentBuilding() {
  Assert.That(ParkLeisureObservation.TryResolveProvider(tenant, building, building, Provider(tenant), default, out var actual), Is.True);
  Assert.That(actual, Is.EqualTo(tenant));
  Assert.That(ParkLeisureObservation.TryResolveProvider(tenant, building, tenant, Provider(tenant), default, out _), Is.False);
 }
 [Test] public void CurrentBuildingProviderCanServeLeisureWithADistinctTarget() {
  Assert.That(ParkLeisureObservation.TryResolveProvider(tenant, building, Entity.Null, default, Provider(building), out var actual), Is.True);
  Assert.That(actual, Is.EqualTo(building), "vanilla supports building fallback without target equality");
 }
 [TestCase(true, false, false)] [TestCase(false, true, false)] [TestCase(false, false, true)]
 public void UnusableSelectedProviderDoesNotFallBackAndStartAVisit(bool inactive, bool unavailable, bool outOfStock) {
  var provider = Provider(tenant); provider.Inactive = inactive; provider.ServiceUnavailable = unavailable; provider.OutOfStock = outOfStock;
  Assert.That(ParkLeisureObservation.TryResolveProvider(tenant, building, building, provider, Provider(building), out _), Is.False);
  provider.Entity = building;
  Assert.That(ParkLeisureObservation.TryResolveProvider(tenant, building, Entity.Null, default, provider, out _), Is.False);
 }
}
