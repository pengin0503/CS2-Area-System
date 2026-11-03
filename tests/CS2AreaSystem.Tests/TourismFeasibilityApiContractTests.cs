using System.Reflection;
using Game;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Zones;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
namespace CS2AreaSystem.Tests;
// Pins the 1.6.2f1 members the Tourism v1 feasibility study relies on (docs/verification/tourism-commercial-contracts.md,
// tourism-operation-effect-contracts.md, tourism-observation-contracts.md). Shapes, constants and vanilla registration
// order only: candidate costs, job ordering and save remapping are traced from the decompiled sources in those documents,
// and none of this proves an in-game effect.
public class TourismFeasibilityApiContractTests {
 private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;
 private static void Field<T>(string name, Type type) {
  var field = typeof(T).GetField(name, Instance);
  Assert.That(field, Is.Not.Null, typeof(T).FullName + "." + name);
  Assert.That(field!.FieldType, Is.EqualTo(type), typeof(T).FullName + "." + name);
 }
 private static int Interval<T>() where T : GameSystemBase =>
  ((T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(T))).GetUpdateInterval(SystemUpdatePhase.GameSimulation);

 // ---- C1: Tourism commercial (derived zone + derived buildings)
 [Test] public void TheFourTourismResourcesExistAndAreCommercial() {
  foreach (var resource in new[] { Resource.Lodging, Resource.Meals, Resource.Entertainment, Resource.Recreation })
   Assert.That(EconomyUtils.IsCommercialResource(resource), Is.True, resource.ToString());
  Assert.That(EconomyUtils.IsCommercialResource(Resource.Food), Is.True, "ordinary goods are commercial too, so the allowed set must be an explicit intersection");
 }
 [Test] public void BuildingSoldResourcesComeFromTheZonePrefab() {
  Field<SpawnableBuilding>("m_ZoneType", typeof(ZonePrefab));
  Field<ZoneProperties>("m_AllowedSold", typeof(ResourceInEditor[]));
  Field<ZonePropertiesData>("m_AllowedSold", typeof(Resource));
  Field<BuildingPropertyData>("m_AllowedSold", typeof(Resource));
  Assert.That(typeof(IZoneBuildingComponent).IsAssignableFrom(typeof(ZoneProperties)), Is.True, "ZoneProperties.InitializeBuilding writes BuildingPropertyData for zoned buildings");
  Field<SpawnableBuildingData>("m_ZonePrefab", typeof(Entity));
  Field<SpawnableBuildingData>("m_Level", typeof(byte));
 }
 [Test] public void SpawnAndLevelUpSelectCandidatesByZoneType() {
  Field<BuildingSpawnGroupData>("m_ZoneType", typeof(ZoneType));
  Assert.That(typeof(ISharedComponentData).IsAssignableFrom(typeof(BuildingSpawnGroupData)), Is.True, "candidate chunks are filtered by this shared component");
  Field<ZoneType>("m_Index", typeof(ushort));
  Field<ZoneData>("m_ZoneType", typeof(ZoneType));
  Assert.That(typeof(ZoneSystem).GetMethod("GetPrefab", new[] { typeof(ZoneType) })?.ReturnType, Is.EqualTo(typeof(Entity)));
  Assert.That(typeof(PrefabSystem).GetMethod("AddPrefab", new[] { typeof(PrefabBase), typeof(string), typeof(PrefabBase), typeof(ComponentBase) })?.ReturnType,
   Is.EqualTo(typeof(bool)), "runtime registration path already used by AreaPrefabRegistrationSystem");
  Assert.That(typeof(ZoneSpawnSystem).GetNestedType("EvaluateSpawnAreas"), Is.Not.Null);
  Assert.That(typeof(Game.Buildings.ZoneCheckSystem), Is.Not.Null, "condemns a building whose lot cells do not carry its own zone type");
 }
 [Test] public void LevelUpSwapsThePrefabOfTheSameEntityThroughUnderConstruction() {
  Field<Game.Objects.UnderConstruction>("m_NewPrefab", typeof(Entity));
  Field<Game.Objects.UnderConstruction>("m_Progress", typeof(byte));
  Assert.That(typeof(BuildingConstructionSystem), Is.Not.Null, "applies m_NewPrefab to PrefabRef and removes UnderConstruction");
 }
 [Test] public void RoomCountIsAPublicFunctionOfTheBuildingPrefab() {
  var method = typeof(LodgingProviderSystem).GetMethod("GetRoomCount", BindingFlags.Public | BindingFlags.Static);
  Assert.That(method, Is.Not.Null);
  Assert.That(method!.GetParameters().Select(p => p.ParameterType), Is.EqualTo(new[] { typeof(int2), typeof(int), typeof(BuildingPropertyData) }));
  var properties = new BuildingPropertyData { m_SpaceMultiplier = 1.5f };
  Assert.That(LodgingProviderSystem.GetRoomCount(new int2(2, 3), 2, properties), Is.EqualTo(18), "lot cells x level x space multiplier, truncated");
 }

 // ---- O1: decision points for promotion, circulation and lodging
 [Test] public void TouristTargetSelectionIsASingleNativePathfindPerHousehold() {
  Assert.That(Enum.IsDefined(typeof(SetupTargetType), SetupTargetType.TouristFindTarget), Is.True);
  Assert.That(Enum.IsDefined(typeof(SetupTargetType), SetupTargetType.Leisure), Is.True);
  Assert.That(Enum.IsDefined(typeof(SetupTargetType), SetupTargetType.Attraction), Is.True);
  Assert.That(typeof(CitizenPathfindSetup).GetMethod("SetupTouristTarget"), Is.Not.Null);
  Assert.That(typeof(CitizenPathfindSetup).GetNestedType("SetupTouristTargetJob", BindingFlags.NonPublic), Is.Not.Null, "candidate costs live in a private Burst job: no public weight hook");
  Assert.That(typeof(PathfindSetupSystem).GetMethod("FindTargets", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(SetupTargetType), typeof(PathfindSetupSystem.SetupData).MakeByRefType() }, null),
   Is.Not.Null, "the target-type switch is private and has no extension case");
  Assert.That(Interval<TouristFindTargetSystem>(), Is.EqualTo(16));
  Assert.That(Interval<TouristHouseholdBehaviorSystem>(), Is.EqualTo(64));
 }
 [Test] public void ARedirectCanOnlyUseThePublicQueueAndPathInformation() {
  var getQueue = typeof(PathfindSetupSystem).GetMethod("GetQueue", new[] { typeof(object), typeof(int), typeof(int) });
  Assert.That(getQueue?.ReturnType, Is.EqualTo(typeof(NativeQueue<SetupQueueItem>)));
  Assert.That(typeof(PathfindSetupSystem).GetMethod("AddQueueWriter", new[] { typeof(JobHandle) }), Is.Not.Null);
  Assert.That(typeof(SetupQueueItem).GetConstructor(new[] { typeof(Entity), typeof(PathfindParameters), typeof(SetupQueueTarget), typeof(SetupQueueTarget) }), Is.Not.Null);
  Field<SetupQueueTarget>("m_Entity", typeof(Entity));
  Field<SetupQueueTarget>("m_Type", typeof(SetupTargetType));
  Field<PathInformation>("m_Destination", typeof(Entity));
  Field<PathInformation>("m_State", typeof(PathFlags));
  Assert.That(Enum.IsDefined(typeof(PathFlags), PathFlags.Pending), Is.True);
  Field<Target>("m_Target", typeof(Entity));
  Assert.That(typeof(IComponentData).IsAssignableFrom(typeof(LodgingSeeker)), Is.True);
 }
 [Test] public void LeisureAndAttractionMeetingsCarryTheirOwnTargets() {
  Field<Leisure>("m_TargetAgent", typeof(Entity));
  Field<Leisure>("m_LastPossibleFrame", typeof(uint));
  Field<CoordinatedMeeting>("m_Target", typeof(Entity));
  Field<CoordinatedMeeting>("m_Status", typeof(MeetingStatus));
  Assert.That(Enum.GetNames(typeof(MeetingStatus)), Does.Contain("Waiting").And.Contain("Traveling").And.Contain("Attending"));
  Assert.That(Enum.IsDefined(typeof(Game.Agents.LeisureType), Game.Agents.LeisureType.Attractions), Is.True);
 }
 [Test] public void VanillaOrdersTouristBehaviourLeisureAndTargetSearchInGameSimulation() {
  var order = ModSystemOrderTests.Schedule(SystemUpdatePhase.GameSimulation);
  int behaviour = order.IndexOf(typeof(TouristHouseholdBehaviorSystem)), leisure = order.IndexOf(typeof(LeisureSystem)), find = order.IndexOf(typeof(TouristFindTargetSystem));
  Assert.That(behaviour, Is.GreaterThanOrEqualTo(0));
  Assert.That(leisure, Is.GreaterThan(behaviour));
  Assert.That(find, Is.GreaterThan(leisure));
 }

 // ---- A1: observation inputs
 [Test] public void LodgingAndTouristFieldsHaveTheTracedShapes() {
  Field<TouristHousehold>("m_Hotel", typeof(Entity));
  Field<TouristHousehold>("m_LeavingTime", typeof(uint));
  Field<LodgingProvider>("m_FreeRooms", typeof(int));
  Field<LodgingProvider>("m_Price", typeof(int));
  Field<Renter>("m_Renter", typeof(Entity));
  Field<PropertyRenter>("m_Property", typeof(Entity));
  Field<AttractivenessProvider>("m_Attractiveness", typeof(int));
  Assert.That(typeof(LodgingProviderSystem).GetField("kUpdatesPerDay", BindingFlags.Public | BindingFlags.Static)?.GetValue(null), Is.EqualTo(32));
 }
}
