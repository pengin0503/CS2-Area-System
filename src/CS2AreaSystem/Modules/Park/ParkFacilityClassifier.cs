using Game.Agents;
using Game.Buildings;
using Game.Prefabs;
using Unity.Entities;
using CS2AreaSystem.Core.Semantics;
namespace CS2AreaSystem.Modules.Park
{
    // Typed facts about one member building, read from vanilla components/prefab data. Pure scoring code
    // consumes this struct so it stays testable without an ECS world.
    public struct ParkFacilityTraits
    {
        public bool IsBuilding, HasRoadAccess, HasPark, HasLeisureProvider, HasAttractivenessProvider;
        public bool HasParkMaintenance;
        public Entity Prefab;
        // HasLeisureData: the building prefab carries LeisureProviderData (LeisureType/efficiency below are valid).
        public bool HasLeisureData;
        public LeisureType LeisureType;
        // LeisureType bits offered by commercial renters (cafés, shops) inside the building.
        public uint CommercialLeisureMask;
        public int LeisureEfficiency, Attractiveness;
        public short ParkMaintenance, ParkMaintenancePool;
    }
    [System.Flags]
    public enum ParkFacilityRoles : byte { None = 0, CoreParkFacility = 1, Leisure = 2, Attraction = 4, SupportingAmenity = 8 }
    public static class ParkFacilityClassifier
    {
        public static bool IsHeadquartersEligible(in ParkFacilityTraits traits,AreaAssetSemanticRegistry registry=null) => traits.IsBuilding &&
            (registry?.Resolve(traits.Prefab,AreaAssetSemantic.ParkHeadquarters,traits.HasParkMaintenance)??traits.HasParkMaintenance);
        public static uint LeisureBit(LeisureType type) => 1u << (int)type;
        // Park-local roles; a building may hold several. Unrelated residential/industrial/office members get None.
        public static ParkFacilityRoles Classify(in ParkFacilityTraits traits,AreaAssetSemanticRegistry registry=null)
        {
            if (!traits.IsBuilding) return ParkFacilityRoles.None;
            var roles = ParkFacilityRoles.None;
            if (traits.HasPark) roles |= ParkFacilityRoles.CoreParkFacility;
            bool leisure=traits.HasLeisureProvider||traits.HasLeisureData,attraction=traits.HasAttractivenessProvider||traits.Attractiveness>0;
            if (registry?.Resolve(traits.Prefab,AreaAssetSemantic.ParkLeisure,leisure)??leisure) roles |= ParkFacilityRoles.Leisure;
            if (registry?.Resolve(traits.Prefab,AreaAssetSemantic.ParkAttraction,attraction)??attraction) roles |= ParkFacilityRoles.Attraction;
            if (traits.CommercialLeisureMask != 0) roles |= ParkFacilityRoles.SupportingAmenity;
            return roles;
        }
        // A Park visit needs leisure at a Park, leisure or attraction building (design §8.4). Commercial spending
        // at supporting amenities is not a Park visit in v1 (deep commercial attribution is deferred).
        public static bool IsVisitRelevant(in ParkFacilityTraits traits,AreaAssetSemanticRegistry registry=null) =>
            (Classify(traits,registry) & (ParkFacilityRoles.CoreParkFacility | ParkFacilityRoles.Leisure | ParkFacilityRoles.Attraction)) != 0;
        // Entrance baseline (design §5.3): a member building with road/path access that is recognizably a Park,
        // leisure provider or attraction. Display names are never consulted.
        public static bool IsPlausibleEntrance(in ParkFacilityTraits traits) =>
            traits.IsBuilding && traits.HasRoadAccess &&
            (traits.HasPark || traits.HasLeisureProvider || traits.HasAttractivenessProvider || traits.Attractiveness > 0);
    }
    public static class ParkFacilityReader
    {
        // Lightweight traits for per-frame/per-visit paths (entrance policy, visit targets): component presence and
        // prefab data only. It never reads Renter buffers or runtime Park/Attractiveness values, which would force
        // main-thread completion of the vanilla jobs writing them.
        public static ParkFacilityTraits ReadCore(Entity entity, EntityManager entities, bool includeRoadAccess)
        {
            var traits = new ParkFacilityTraits();
            if (!entities.Exists(entity) || !entities.HasComponent<Building>(entity)) return traits;
            traits.IsBuilding = true;
            traits.HasParkMaintenance = entities.HasComponent<Game.Buildings.ParkMaintenance>(entity);
            if (entities.HasComponent<PrefabRef>(entity)) traits.Prefab = entities.GetComponentData<PrefabRef>(entity).m_Prefab;
            traits.HasRoadAccess = !includeRoadAccess || entities.GetComponentData<Building>(entity).m_RoadEdge != Entity.Null;
            traits.HasPark = entities.HasComponent<Game.Buildings.Park>(entity);
            traits.HasLeisureProvider = entities.HasComponent<Game.Buildings.LeisureProvider>(entity);
            traits.HasAttractivenessProvider = entities.HasComponent<AttractivenessProvider>(entity);
            if (!entities.HasComponent<PrefabRef>(entity)) return traits;
            var prefab = entities.GetComponentData<PrefabRef>(entity).m_Prefab;
            if (!entities.Exists(prefab)) return traits;
            if (entities.HasComponent<LeisureProviderData>(prefab))
            {
                var leisure = entities.GetComponentData<LeisureProviderData>(prefab);
                traits.HasLeisureProvider = true; traits.HasLeisureData = true;
                traits.LeisureType = leisure.m_LeisureType; traits.LeisureEfficiency = leisure.m_Efficiency;
            }
            if (entities.HasComponent<AttractionData>(prefab)) traits.Attractiveness = entities.GetComponentData<AttractionData>(prefab).m_Attractiveness;
            return traits;
        }
        public static ParkFacilityTraits ReadAnchor(Entity entity, EntityManager entities) => ReadCore(entity, entities, true);
        public static ParkFacilityTraits Read(Entity entity, EntityManager entities)
        {
            var traits = ReadCore(entity, entities, true);
            if (!traits.IsBuilding) return traits;
            if (entities.HasComponent<Game.Buildings.Park>(entity))
            {
                traits.HasPark = true;
                traits.ParkMaintenance = entities.GetComponentData<Game.Buildings.Park>(entity).m_Maintenance;
            }
            traits.HasLeisureProvider = entities.HasComponent<Game.Buildings.LeisureProvider>(entity);
            if (entities.HasComponent<AttractivenessProvider>(entity))
            {
                traits.HasAttractivenessProvider = true;
                traits.Attractiveness = entities.GetComponentData<AttractivenessProvider>(entity).m_Attractiveness;
            }
            if (!entities.HasComponent<PrefabRef>(entity)) return traits;
            var prefab = entities.GetComponentData<PrefabRef>(entity).m_Prefab;
            if (!entities.Exists(prefab)) return traits;
            if (entities.HasComponent<LeisureProviderData>(prefab))
            {
                var leisure = entities.GetComponentData<LeisureProviderData>(prefab);
                traits.HasLeisureProvider = true;
                traits.HasLeisureData = true;
                traits.LeisureType = leisure.m_LeisureType;
                traits.LeisureEfficiency = leisure.m_Efficiency;
            }
            if (entities.HasBuffer<Renter>(entity))
            {
                var renters = entities.GetBuffer<Renter>(entity, true);
                for (int i = 0; i < renters.Length; i++)
                {
                    var renter = renters[i].m_Renter;
                    if (!entities.Exists(renter) || !entities.HasComponent<PrefabRef>(renter)) continue;
                    var renterPrefab = entities.GetComponentData<PrefabRef>(renter).m_Prefab;
                    if (entities.Exists(renterPrefab) && entities.HasComponent<LeisureProviderData>(renterPrefab))
                        traits.CommercialLeisureMask |= ParkFacilityClassifier.LeisureBit(entities.GetComponentData<LeisureProviderData>(renterPrefab).m_LeisureType);
                }
            }
            if (entities.HasComponent<ParkData>(prefab)) traits.ParkMaintenancePool = entities.GetComponentData<ParkData>(prefab).m_MaintenancePool;
            if (!traits.HasAttractivenessProvider && entities.HasComponent<AttractionData>(prefab))
                traits.Attractiveness = entities.GetComponentData<AttractionData>(prefab).m_Attractiveness;
            return traits;
        }
    }
}
namespace CS2AreaSystem.Modules.Park
{
    // Derived, runtime-only aggregate of one Park's member buildings.
    public sealed class ParkFacilitySnapshot
    {
        public int RelevantFacilities, ParkFacilities, Attractions;
        public float AttractivenessSum, LeisureEfficiencySum, WeightedCategories;
        public int DistinctCategories;
        public ParkMetric VanillaCondition;
        public static ParkFacilitySnapshot Build(System.Collections.Generic.IEnumerable<ParkFacilityTraits> members,AreaAssetSemanticRegistry registry=null)
        {
            var snapshot = new ParkFacilitySnapshot();
            uint leisureTypes = 0, commercial = 0;
            bool attraction = false;
            double condition = 0; int conditionCount = 0;
            foreach (var traits in members)
            {
                var roles = ParkFacilityClassifier.Classify(traits,registry);
                if (roles == ParkFacilityRoles.None) continue;
                snapshot.RelevantFacilities++;
                if ((roles & ParkFacilityRoles.CoreParkFacility) != 0)
                {
                    snapshot.ParkFacilities++;
                    if (traits.ParkMaintenancePool > 0)
                    { condition += 100.0 * System.Math.Min(1.0, System.Math.Max(0.0, (double)traits.ParkMaintenance / traits.ParkMaintenancePool)); conditionCount++; }
                    if (!traits.HasLeisureData) leisureTypes |= ParkFacilityClassifier.LeisureBit(LeisureType.CityPark);
                }
                if (traits.HasLeisureData)
                {
                    leisureTypes |= ParkFacilityClassifier.LeisureBit(traits.LeisureType);
                    snapshot.LeisureEfficiencySum += System.Math.Max(0, traits.LeisureEfficiency);
                }
                if ((roles & ParkFacilityRoles.Attraction) != 0) { attraction = true; snapshot.Attractions++; snapshot.AttractivenessSum += System.Math.Max(0, traits.Attractiveness); }
                commercial |= traits.CommercialLeisureMask;
            }
            for (int type = 0; type < (int)LeisureType.Count; type++)
            {
                uint bit = 1u << type;
                if ((leisureTypes & bit) != 0) { snapshot.WeightedCategories += ParkQualityModel.FacilityCategoryWeight((LeisureType)type); snapshot.DistinctCategories++; }
                else if ((commercial & bit) != 0) { snapshot.WeightedCategories += SupportingCategoryWeight; snapshot.DistinctCategories++; }
            }
            if (attraction) { snapshot.WeightedCategories += 1f; snapshot.DistinctCategories++; }
            snapshot.VanillaCondition = conditionCount > 0 ? ParkMetric.Of((float)(condition / conditionCount)) : ParkMetric.Unavailable;
            return snapshot;
        }
        // Supporting commercial leisure adds breadth, but less than a genuine Park facility category.
        public const float SupportingCategoryWeight = 0.5f;
    }
}
