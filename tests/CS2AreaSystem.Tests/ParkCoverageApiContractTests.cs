using System.Reflection;
using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Prefabs;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Pins the 1.6.2f1 members the Park coverage modifier relies on (docs/verification/park-coverage-modifier-contract.md).
// Shapes and vanilla registration order only: ECS job ordering, change versions and serialization are proven from the
// decompiled sources in that document and still need the in-game release gate.
public class ParkCoverageApiContractTests {
 private static void Field<T>(string name, Type type) {
  var field = typeof(T).GetField(name, BindingFlags.Public | BindingFlags.Instance);
  Assert.That(field, Is.Not.Null, typeof(T).FullName + "." + name);
  Assert.That(field!.FieldType, Is.EqualTo(type), typeof(T).FullName + "." + name);
 }
 [Test] public void ModifiedServiceCoverageHasExactlyTheThreeFloatFieldsAndIsSaved() {
  Field<ModifiedServiceCoverage>("m_Range", typeof(float));
  Field<ModifiedServiceCoverage>("m_Capacity", typeof(float));
  Field<ModifiedServiceCoverage>("m_Magnitude", typeof(float));
  Assert.That(typeof(ModifiedServiceCoverage).GetFields(BindingFlags.Public | BindingFlags.Instance).Length, Is.EqualTo(3));
  Assert.That(typeof(IComponentData).IsAssignableFrom(typeof(ModifiedServiceCoverage)), Is.True);
  Assert.That(typeof(ISerializable).IsAssignableFrom(typeof(ModifiedServiceCoverage)), Is.True, "saved by the native serializer, so it must be restored first");
  Assert.That(typeof(ModifiedServiceCoverage).GetConstructor(new[] { typeof(CoverageData) }), Is.Not.Null, "native baseline starts from prefab CoverageData");
  Field<CoverageData>("m_Capacity", typeof(float));
  Field<CoverageData>("m_Magnitude", typeof(float));
 }
 [Test] public void TheNativeWriterRecomputesFromPrefabDataOnEveryPass() {
  var method = typeof(Game.Simulation.ParkAISystem).GetMethod("GetModifiedServiceCoverage", BindingFlags.Public | BindingFlags.Static);
  Assert.That(method, Is.Not.Null);
  Assert.That(method!.ReturnType, Is.EqualTo(typeof(ModifiedServiceCoverage)));
  Assert.That(method.GetParameters().Select(p => p.ParameterType), Is.EqualTo(new[] {
   typeof(Game.Buildings.Park), typeof(ParkData), typeof(CoverageData), typeof(DynamicBuffer<Game.City.CityModifier>) }), "no previous component value is an input");
  var updates = typeof(Game.Simulation.ParkAISystem).GetField("kUpdatesPerDay", BindingFlags.Public | BindingFlags.Static);
  Assert.That(updates?.FieldType, Is.EqualTo(typeof(int)));
  Assert.That(updates!.IsInitOnly, Is.True);
 }
 [Test] public void TheCoverageConsumerRunsBeforeTheNativeWriterInGameSimulation() {
  var order = ModSystemOrderTests.Schedule(SystemUpdatePhase.GameSimulation);
  int consumer = order.IndexOf(typeof(Game.Simulation.ServiceCoverageSystem)), writer = order.IndexOf(typeof(Game.Simulation.ParkAISystem));
  Assert.That(consumer, Is.GreaterThanOrEqualTo(0));
  Assert.That(writer, Is.GreaterThan(consumer), "a pass directly after ParkAISystem is read no earlier than the next frame");
  Assert.That(ModSystemOrderTests.Schedule(SystemUpdatePhase.Serialize), Does.Contain(typeof(Game.Serialization.SerializerSystem)));
 }
 [Test] public void FreshnessAndDependencyApisExistInTheSuppliedEntities() {
  Assert.That(typeof(ComponentSystemBase).GetProperty("LastSystemVersion")?.PropertyType, Is.EqualTo(typeof(uint)));
  Assert.That(typeof(EntityManager).GetMethod("GetChunk", new[] { typeof(Entity) })?.ReturnType, Is.EqualTo(typeof(ArchetypeChunk)));
  Assert.That(typeof(EntityManager).GetMethods().Any(m => m.Name == "CompleteDependencyBeforeRO" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0), Is.True);
  Assert.That(typeof(ArchetypeChunk).GetMethods().Any(m => m.Name == "GetChangeVersion" && m.IsGenericMethodDefinition &&
   m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsByRef), Is.True);
  Assert.That(typeof(Game.UpdateSystem).GetMethods().Any(m => m.Name == "UpdateAfter" && m.GetGenericArguments().Length == 2), Is.True);
  Assert.That(typeof(GameSystemBase).GetMethod("GetUpdateOffset")?.IsVirtual, Is.True);
 }
}
