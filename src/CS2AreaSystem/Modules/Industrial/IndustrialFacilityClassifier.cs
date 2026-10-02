using Game.Buildings;
using Game.Companies;
using Game.Economy;
using Game.Prefabs;
using Unity.Entities;
using CS2AreaSystem.Core.Semantics;
namespace CS2AreaSystem.Modules.Industrial
{
    // Industrial-local second stage after Core building membership (design §6). Pure: ECS facts arrive as traits.
    public static class IndustrialFacilityClassifier
    {
        public const IndustrialFacilityRole ProducerRoles = IndustrialFacilityRole.Extractor | IndustrialFacilityRole.Processor;
        public const IndustrialFacilityRole EligibleRoles = ProducerRoles | IndustrialFacilityRole.Storage | IndustrialFacilityRole.Cargo;
        public static IndustrialFacilityRole Classify(in IndustrialFacilityTraits traits,AreaAssetSemanticRegistry registry=null)
        {
            if (!traits.IsBuilding || traits.HasOfficeProperty || traits.HasOfficeCompany || traits.HasCommercialProperty) return IndustrialFacilityRole.None;
            var roles = IndustrialFacilityRole.None;
            bool extractor=traits.HasExtractorProperty||traits.HasExtractorFacility||traits.HasExtractorCompany;
            bool producer=traits.ManufacturesGoods||traits.HasProcessorCompany,storage=traits.HasStorageProperty||traits.HasStorageCompany;
            if (registry?.Resolve(traits.Prefab,AreaAssetSemantic.IndustrialExtractor,extractor)??extractor) roles |= IndustrialFacilityRole.Extractor;
            if (registry?.Resolve(traits.Prefab,AreaAssetSemantic.IndustrialProducer,producer)??producer) roles |= IndustrialFacilityRole.Processor;
            if (registry?.Resolve(traits.Prefab,AreaAssetSemantic.IndustrialStorage,storage)??storage) roles |= IndustrialFacilityRole.Storage;
            if (registry?.Resolve(traits.Prefab,AreaAssetSemantic.IndustrialCargo,traits.HasCargoStation)??traits.HasCargoStation) roles |= IndustrialFacilityRole.Cargo;
            // Office-only buildings never reach here with a role: office property/company is not an industrial capability.
            if ((roles & (IndustrialFacilityRole.Storage | IndustrialFacilityRole.Cargo)) != 0 && (roles & ProducerRoles) == 0 && traits.HasWorkplaces)
                roles |= IndustrialFacilityRole.SupportingWorkplace;
            return roles;
        }
        // Stable building evidence survives tenant vacancy. Company presence alone never qualifies.
        public static bool IsHeadquartersEligible(in IndustrialFacilityTraits traits,AreaAssetSemanticRegistry registry=null) => traits.IsBuilding &&
            (registry?.Resolve(traits.Prefab,AreaAssetSemantic.IndustrialHeadquarters,traits.HasOfficeProperty)??traits.HasOfficeProperty);
    }
    public static class IndustrialFacilityReader
    {
        // Manufactured goods excluding office output (EconomyUtils.IsOfficeResource) and raw extraction
        // (EconomyUtils.IsExtractorResource), mirroring BuildingProperties' Office/Extractor property split.
        public static bool ManufacturesGoods(Resource manufactured)
        {
            for (int bit = 0; bit < 64; bit++)
            {
                var resource = (Resource)(1UL << bit);
                if ((manufactured & resource) == 0) continue;
                if (!EconomyUtils.IsOfficeResource(resource) && !EconomyUtils.IsExtractorResource(resource)) return true;
            }
            return false;
        }
        // Main-thread read used only for an explicit HQ assignment (a user action), never in steady-state sampling.
        public static IndustrialFacilityTraits ReadAnchor(Entity entity, EntityManager entities)
        {
            var traits = new IndustrialFacilityTraits();
            if (!entities.Exists(entity) || !entities.HasComponent<Building>(entity)) return traits;
            traits.IsBuilding = true;
            traits.HasExtractorProperty = entities.HasComponent<ExtractorProperty>(entity);
            traits.HasStorageProperty = entities.HasComponent<StorageProperty>(entity);
            traits.HasOfficeProperty = entities.HasComponent<OfficeProperty>(entity);
            traits.HasCommercialProperty = entities.HasComponent<CommercialProperty>(entity);
            traits.HasCargoStation = entities.HasComponent<Game.Buildings.CargoTransportStation>(entity);
            traits.HasExtractorFacility = entities.HasComponent<Game.Buildings.ExtractorFacility>(entity);
            traits.HasWorkplaces = entities.HasComponent<WorkProvider>(entity);
            if (entities.HasComponent<PrefabRef>(entity))
            {
                var prefab = entities.GetComponentData<PrefabRef>(entity).m_Prefab;
                traits.Prefab=prefab;
                if (entities.Exists(prefab))
                {
                    if (entities.HasComponent<BuildingPropertyData>(prefab))
                    {
                        var property = entities.GetComponentData<BuildingPropertyData>(prefab);
                        traits.ManufacturesGoods = entities.HasComponent<IndustrialProperty>(entity) && ManufacturesGoods(property.m_AllowedManufactured);
                        traits.HasStorageProperty |= property.m_AllowedStored != Resource.NoResource && entities.HasComponent<IndustrialProperty>(entity);
                    }
                    traits.HasCargoStation |= entities.HasComponent<CargoTransportStationData>(prefab);
                    traits.HasExtractorFacility |= entities.HasComponent<ExtractorFacilityData>(prefab);
                }
            }
            if (entities.HasBuffer<Renter>(entity))
            {
                var renters = entities.GetBuffer<Renter>(entity, true);
                for (int i = 0; i < renters.Length; i++) AddTenant(renters[i].m_Renter, entities, ref traits);
            }
            return traits;
        }
        private static void AddTenant(Entity renter, EntityManager entities, ref IndustrialFacilityTraits traits)
        {
            if (!entities.Exists(renter) || !entities.HasComponent<CompanyData>(renter)) return;
            bool office = entities.HasComponent<OfficeCompany>(renter);
            bool commercial = entities.HasComponent<CommercialCompany>(renter) || entities.HasComponent<ServiceAvailable>(renter);
            traits.HasOfficeCompany |= office;
            if (office || commercial) return;
            bool extractor = entities.HasComponent<Game.Companies.ExtractorCompany>(renter);
            traits.HasExtractorCompany |= extractor;
            traits.HasProcessorCompany |= !extractor && entities.HasComponent<Game.Companies.ProcessingCompany>(renter) && entities.HasComponent<IndustrialCompany>(renter);
            traits.HasStorageCompany |= entities.HasComponent<Game.Companies.StorageCompany>(renter);
            traits.HasWorkplaces |= entities.HasComponent<WorkProvider>(renter);
        }
    }
}
