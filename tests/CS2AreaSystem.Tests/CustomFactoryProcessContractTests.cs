using System.Reflection;
using CS2AreaSystem.Modules.Industrial;
using Game.Economy;
using Game.Prefabs;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
// Phase 3 D2 Task 1: the reference factory reuses one vanilla process relationship (Timber -> Furniture) instead of
// authoring a recipe (docs/verification/custom-factory-process-contract.md). These checks pin the 1.6.2f1 members and pure
// vanilla functions that make that safe; they do not prove the live company prefab amounts, which are prefab data.
public class CustomFactoryProcessContractTests
{
 private static void Field<T>(string name, Type type) {
  var field = typeof(T).GetField(name, BindingFlags.Public | BindingFlags.Instance);
  Assert.That(field, Is.Not.Null, typeof(T).FullName + "." + name);
  Assert.That(field!.FieldType, Is.EqualTo(type), typeof(T).FullName + "." + name);
 }
 [Test] public void TheContractNamesOneExistingManufacturedResource() {
  Assert.That(IndustrialReferenceFactoryContract.PrefabName, Is.EqualTo("AS_FurnitureFactory"));
  Assert.That(IndustrialReferenceFactoryContract.Output, Is.EqualTo(Resource.Furniture));
  Assert.That(IndustrialReferenceFactoryContract.VanillaInput, Is.EqualTo(Resource.Timber));
  Assert.That(Enum.IsDefined(typeof(Resource), IndustrialReferenceFactoryContract.Output), Is.True, "an existing vanilla Resource member");
  ulong output = (ulong)IndustrialReferenceFactoryContract.Output;
  Assert.That(output & (output - 1), Is.EqualTo(0UL), "exactly one resource bit");
  Assert.That(EconomyUtils.IsOfficeResource(Resource.Furniture), Is.False, "not an office product (no OfficeProperty/OfficeCompany)");
  Assert.That(EconomyUtils.IsExtractorResource(Resource.Furniture), Is.False, "not raw extraction (no ExtractorProperty)");
  Assert.That(IndustrialFacilityReader.ManufacturesGoods(Resource.Furniture), Is.True, "classified as a producer by the generic reader");
 }
 [Test] public void VanillaMakesFurnitureOnlyFromTimber() {
  Assert.That(EconomyUtils.IsProducedFrom(Resource.Furniture, Resource.Timber), Is.True);
  for (int bit = 0; bit < 64; bit++) {
   var material = (Resource)(1UL << bit);
   if (material == Resource.Timber || !Enum.IsDefined(typeof(Resource), material)) continue;
   Assert.That(EconomyUtils.IsProducedFrom(Resource.Furniture, material), Is.False, material.ToString());
  }
 }
 [Test] public void EditorResourcesMapToTheRuntimeBitsAndAnEmptyInputListAcceptsAnyRecipe() {
  Assert.That(EconomyUtils.GetResource(ResourceInEditor.Furniture), Is.EqualTo(Resource.Furniture));
  Assert.That(EconomyUtils.GetResource(ResourceInEditor.Timber), Is.EqualTo(Resource.Timber));
  // BuildingProperties.GetPropertyData: m_AllowedInput defaults to all resources when the list is empty.
  Assert.That(EconomyUtils.GetResources(Array.Empty<ResourceInEditor>(), EconomyUtils.GetAllResources()), Is.EqualTo(EconomyUtils.GetAllResources()));
  Assert.That(EconomyUtils.GetResources(new[] { ResourceInEditor.Furniture }, Resource.NoResource), Is.EqualTo(Resource.Furniture));
 }
 [Test] public void ThePrefabShapeTheContractReliesOnExists() {
  Field<BuildingProperties>("m_AllowedManufactured", typeof(ResourceInEditor[]));
  Field<BuildingProperties>("m_AllowedInput", typeof(ResourceInEditor[]));
  Field<BuildingProperties>("m_AllowedSold", typeof(ResourceInEditor[]));
  Field<BuildingProperties>("m_AllowedStored", typeof(ResourceInEditor[]));
  Field<BuildingPropertyData>("m_AllowedManufactured", typeof(Resource));
  Field<BuildingPropertyData>("m_AllowedSold", typeof(Resource));
  Field<BuildingPropertyData>("m_AllowedStored", typeof(Resource));
  Field<BuildingPropertyData>("m_ResidentialProperties", typeof(int));
  Field<SignatureBuilding>("m_ZoneType", typeof(ZonePrefab));
  Field<SpawnableBuildingData>("m_ZonePrefab", typeof(Entity));
  Field<ZoneData>("m_AreaType", typeof(Game.Zones.AreaType));
  Field<ZoneData>("m_ZoneFlags", typeof(ZoneFlags));
  Assert.That((byte)ZoneFlags.Office, Is.EqualTo(8));
  Field<IndustrialProcessData>("m_Input1", typeof(ResourceStack));
  Field<IndustrialProcessData>("m_Input2", typeof(ResourceStack));
  Field<IndustrialProcessData>("m_Output", typeof(ResourceStack));
  Field<ProcessingCompany>("process", typeof(IndustrialProcess));
  Assert.That(typeof(SignatureBuildingData).IsValueType, Is.True);
  Assert.That(typeof(BuildingPrefab).IsAssignableFrom(typeof(BuildingPrefab)), Is.True);
 }
}
