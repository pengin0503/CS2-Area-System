using System.Collections.Generic;
using Colossal.Collections;
using Colossal.Mathematics;
using Game;
using Game.Areas;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Park
{
    // Park-local landscape analysis (design §7, §21.4). Core membership stays building-only: vegetation, props and
    // paths are read here, only inside Park bounds, only for Parks the scheduler selects within a strict budget.
    public partial class ParkLandscapeSystem : GameSystemBase
    {
        // At most this many Park scans every ScanFrameInterval rendered frames.
        public const int ScanBudget = 1, ScanFrameInterval = 8;
        // Fallback refresh (vegetation growth, undetected changes): every 3 in-game hours.
        public const uint RefreshFrames = ParkRuntimeSystem.FramesPerDay / 8;
        // Above this many changed objects in one frame, every Park is simply re-queued.
        public const int MaxTrackedChanges = 512;
        public const float TransitRadius = 120f;
        private const int PathSamples = 8;
        private ParkRuntimeSystem runtime;
        private Game.Objects.SearchSystem objectSearch;
        private Game.Net.SearchSystem netSearch;
        private EntityQuery changedParks, changedObjects, changedNets;
        private readonly ParkLandscapeScheduler scheduler = new ParkLandscapeScheduler(RefreshFrames);
        private readonly List<ParkAreaRef> parks = new List<ParkAreaRef>();
        private readonly Dictionary<ulong, Bounds2> bounds = new Dictionary<ulong, Bounds2>();
        private int frameCounter, loadGeneration = -1;
        private bool failureLogged, parksDirty = true;
        public int ScanCount { get; private set; }
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            runtime = World.GetOrCreateSystemManaged<ParkRuntimeSystem>();
            objectSearch = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            netSearch = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            changedParks = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Core.Areas.ManagedArea>() },
                Any = new[] { ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Updated>(), ComponentType.ReadOnly<Deleted>() },
                None = new[] { ComponentType.ReadOnly<Temp>() } });
            changedObjects = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Game.Objects.Object>(), ComponentType.ReadOnly<Static>(), ComponentType.ReadOnly<Transform>() },
                Any = new[] { ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Updated>(), ComponentType.ReadOnly<Deleted>() },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Game.Buildings.Building>() } });
            changedNets = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Game.Net.Edge>(), ComponentType.ReadOnly<Game.Net.Curve>() },
                Any = new[] { ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Updated>(), ComponentType.ReadOnly<Deleted>() },
                None = new[] { ComponentType.ReadOnly<Temp>() } });
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!runtime.Ready) return;
            if (loadGeneration != runtime.LoadGeneration) { loadGeneration = runtime.LoadGeneration; scheduler.Clear(); parksDirty = true; }
            if (!changedParks.IsEmptyIgnoreFilter) parksDirty = true;
            if (parksDirty || frameCounter % 256 == 0) SyncParks();
            if (scheduler.TrackedCount == 0) { frameCounter++; return; }
            MarkChanged(changedObjects, true);
            MarkChanged(changedNets, false);
            if (frameCounter++ % ScanFrameInterval != 0) return;
            foreach (var id in scheduler.Take(runtime.SimulationFrame, ScanBudget)) Scan(id);
        }
        private void SyncParks()
        {
            parksDirty = false;
            runtime.CollectParks(parks);
            runtime.PruneStore(parks);
            var alive = new HashSet<ulong>();
            foreach (var park in parks)
            {
                alive.Add(park.StableId);
                bool known = scheduler.IsTracked(park.StableId);
                var box = EntityManager.HasComponent<Geometry>(park.Area) ? EntityManager.GetComponentData<Geometry>(park.Area).m_Bounds.xz : default;
                // A new Park or a changed boundary needs a new scan.
                if (!known || !bounds.TryGetValue(park.StableId, out var old) || !old.Equals(box)) { scheduler.Track(park.StableId); scheduler.MarkDirty(park.StableId); }
                bounds[park.StableId] = box;
                runtime.Store.GetOrCreate(park.StableId, park.Area);
            }
            var gone = new List<ulong>();
            foreach (var id in bounds.Keys) if (!alive.Contains(id)) gone.Add(id);
            foreach (var id in gone) { bounds.Remove(id); scheduler.Forget(id); }
        }
        // Detected object/net changes re-queue only the Parks whose bounds contain them.
        private void MarkChanged(EntityQuery query, bool objects)
        {
            if (query.IsEmptyIgnoreFilter) return;
            using var changed = query.ToEntityArray(Allocator.Temp);
            if (changed.Length > MaxTrackedChanges) { scheduler.MarkAllDirty(); return; }
            foreach (var entity in changed)
            {
                float2 position;
                if (objects) position = EntityManager.GetComponentData<Transform>(entity).m_Position.xz;
                else position = MathUtils.Position(EntityManager.GetComponentData<Game.Net.Curve>(entity).m_Bezier, 0.5f).xz;
                foreach (var pair in bounds)
                    if (math.all(position >= pair.Value.min) && math.all(position <= pair.Value.max)) scheduler.MarkDirty(pair.Key);
            }
        }
        private void Scan(ulong id)
        {
            if (!runtime.Store.TryGet(id, out var snapshot) || !EntityManager.Exists(snapshot.Area)) return;
            try
            {
                ScanCount++;
                var area = snapshot.Area;
                var geometry = EntityManager.GetComponentData<Geometry>(area);
                var triangles = ParkGeometry.Triangles(EntityManager, area);
                snapshot.AreaSquareMeters = geometry.m_SurfaceArea;
                var tally = new ParkLandscapeTally();
                int transit = 0;
                var candidates = new List<Entity>();
                var search = new Bounds2(geometry.m_Bounds.xz.min - TransitRadius, geometry.m_Bounds.xz.max + TransitRadius);
                var objectTree = objectSearch.GetStaticSearchTree(true, out var objectDependency);
                objectDependency.Complete();
                var objectIterator = new BoundsIterator { Bounds = search, Items = candidates };
                objectTree.Iterate(ref objectIterator);
                foreach (var entity in candidates)
                {
                    if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<Transform>(entity)) continue;
                    var position = EntityManager.GetComponentData<Transform>(entity).m_Position.xz;
                    if (IsTransit(entity))
                    {
                        if (ParkGeometry.Distance(triangles, position) <= TransitRadius) transit++;
                        continue;
                    }
                    if (!ParkGeometry.Contains(triangles, position)) continue;
                    var traits = ReadObject(entity, out int prefabKey);
                    tally.Add(ParkLandscapeClassifier.Classify(traits), prefabKey);
                }
                float pathLength = MeasurePaths(geometry.m_Bounds.xz, triangles);
                snapshot.Landscape = new ParkLandscapeSnapshot {
                    Inputs = tally.ToInputs(geometry.m_SurfaceArea, pathLength), TransitStops = transit, ScannedFrame = runtime.SimulationFrame, Available = true };
                snapshot.Landscape.Result = ParkLandscapeModel.ComputeLandscape(snapshot.Landscape.Inputs);
                failureLogged = false;
            }
            catch (System.Exception error)
            {
                // Keep the last good snapshot; without one the metric stays unavailable (neutral in Quality).
                if (!failureLogged) { failureLogged = true; Mod.Log.Error(error, "Park landscape analysis failed; last result kept"); }
            }
        }
        private bool IsTransit(Entity entity) =>
            !EntityManager.HasComponent<Temp>(entity) && !EntityManager.HasComponent<Deleted>(entity) &&
            (EntityManager.HasComponent<Game.Routes.TransportStop>(entity) || EntityManager.HasComponent<Game.Buildings.PublicTransportStation>(entity));
        private ParkObjectTraits ReadObject(Entity entity, out int prefabKey)
        {
            var traits = new ParkObjectTraits {
                IsStaticObject = EntityManager.HasComponent<Game.Objects.Object>(entity) && EntityManager.HasComponent<Static>(entity),
                IsBuilding = EntityManager.HasComponent<Game.Buildings.Building>(entity),
                HasOwner = EntityManager.HasComponent<Owner>(entity),
                IsTempOrDeleted = EntityManager.HasComponent<Temp>(entity) || EntityManager.HasComponent<Deleted>(entity),
                IsNetObject = EntityManager.HasComponent<Game.Objects.NetObject>(entity),
                IsTransportStop = EntityManager.HasComponent<Game.Routes.TransportStop>(entity),
                IsUtility = EntityManager.HasComponent<Game.Objects.UtilityObject>(entity) || EntityManager.HasComponent<Game.Objects.Marker>(entity),
                IsTree = EntityManager.HasComponent<Game.Objects.Tree>(entity),
                IsPlant = EntityManager.HasComponent<Game.Objects.Plant>(entity),
                IsStreetLight = EntityManager.HasComponent<Game.Objects.StreetLight>(entity) };
            prefabKey = 0;
            if (!EntityManager.HasComponent<PrefabRef>(entity)) return traits;
            var prefab = EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
            if (!EntityManager.Exists(prefab)) return traits;
            prefabKey = prefab.Index;
            if (EntityManager.HasComponent<SpawnLocationData>(prefab)) traits.ActivityMask = EntityManager.GetComponentData<SpawnLocationData>(prefab).m_ActivityMask.m_Mask;
            traits.IsTree |= EntityManager.HasComponent<TreeData>(prefab);
            traits.IsPlant |= EntityManager.HasComponent<PlantData>(prefab);
            traits.IsStreetLight |= EntityManager.HasComponent<StreetLightData>(prefab);
            traits.IsPlaceable = EntityManager.HasComponent<PlaceableObjectData>(prefab);
            return traits;
        }
        // Pedestrian pathway length inside the Park (edge length weighted by sampled inside fraction).
        private float MeasurePaths(Bounds2 box, List<Triangle2> triangles)
        {
            var edges = new List<Entity>();
            var tree = netSearch.GetNetSearchTree(true, out var dependency);
            dependency.Complete();
            var iterator = new BoundsIterator { Bounds = box, Items = edges };
            tree.Iterate(ref iterator);
            float length = 0f;
            foreach (var edge in edges)
            {
                if (!EntityManager.Exists(edge) || !EntityManager.HasComponent<Game.Net.Edge>(edge) || !EntityManager.HasComponent<Game.Net.Curve>(edge) ||
                    EntityManager.HasComponent<Temp>(edge) || EntityManager.HasComponent<Deleted>(edge) || !EntityManager.HasComponent<PrefabRef>(edge)) continue;
                var prefab = EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab;
                if (!EntityManager.Exists(prefab) || !EntityManager.HasComponent<PathwayData>(prefab)) continue;
                var curve = EntityManager.GetComponentData<Game.Net.Curve>(edge);
                int inside = 0;
                for (int i = 0; i < PathSamples; i++)
                    if (ParkGeometry.Contains(triangles, MathUtils.Position(curve.m_Bezier, (i + 0.5f) / PathSamples).xz)) inside++;
                length += curve.m_Length * inside / PathSamples;
            }
            return length;
        }
        private struct BoundsIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            public Bounds2 Bounds;
            public List<Entity> Items;
            public bool Intersect(QuadTreeBoundsXZ bounds) => math.all(Bounds.min <= bounds.m_Bounds.xz.max) && math.all(Bounds.max >= bounds.m_Bounds.xz.min);
            public void Iterate(QuadTreeBoundsXZ bounds, Entity item) { if (Intersect(bounds)) Items.Add(item); }
        }
    }
}
