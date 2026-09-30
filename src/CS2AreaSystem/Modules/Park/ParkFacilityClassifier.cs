using Game.Agents;
using Game.Buildings;
using Game.Prefabs;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    // Typed facts about one member building, read from vanilla components/prefab data. Pure scoring code
    // consumes this struct so it stays testable without an ECS world.
    public struct ParkFacilityTraits
    {
        public bool IsBuilding, HasRoadAccess, HasPark, HasLeisureProvider, HasAttractivenessProvider;
        public LeisureType LeisureType;
        public int LeisureEfficiency, Attractiveness;
        public short ParkMaintenance, ParkMaintenancePool;
    }
    public static class ParkFacilityClassifier
    {
        // Entrance baseline (design §5.3): a member building with road/path access that is recognizably a Park,
        // leisure provider or attraction. Display names are never consulted.
        public static bool IsPlausibleEntrance(in ParkFacilityTraits traits) =>
            traits.IsBuilding && traits.HasRoadAccess &&
            (traits.HasPark || traits.HasLeisureProvider || traits.HasAttractivenessProvider || traits.Attractiveness > 0);
    }
    public static class ParkFacilityReader
    {
        public static ParkFacilityTraits Read(Entity entity, EntityManager entities)
        {
            var traits = new ParkFacilityTraits();
            if (!entities.Exists(entity) || !entities.HasComponent<Building>(entity)) return traits;
            traits.IsBuilding = true;
            traits.HasRoadAccess = entities.GetComponentData<Building>(entity).m_RoadEdge != Entity.Null;
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
                traits.LeisureType = leisure.m_LeisureType;
                traits.LeisureEfficiency = leisure.m_Efficiency;
            }
            if (entities.HasComponent<ParkData>(prefab)) traits.ParkMaintenancePool = entities.GetComponentData<ParkData>(prefab).m_MaintenancePool;
            if (!traits.HasAttractivenessProvider && entities.HasComponent<AttractionData>(prefab))
                traits.Attractiveness = entities.GetComponentData<AttractionData>(prefab).m_Attractiveness;
            return traits;
        }
    }
}
