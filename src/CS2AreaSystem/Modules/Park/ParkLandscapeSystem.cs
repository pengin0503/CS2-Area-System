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
        // One Park scan at a time, a new one starting at most every ScanFrameInterval rendered frames.
        public const int ScanFrameInterval = 8;
        // Fallback refresh (vegetation growth, undetected changes): every 3 in-game hours.
        public const uint RefreshFrames = ParkRuntimeSystem.FramesPerDay / 8;
        // Above this many changed objects in one frame, every Park is simply re-queued.
        public const int MaxTrackedChanges = 512;
        public const float TransitRadius = 120f;
        // Candidate objects classified per frame; a large Park is scanned over several frames.
        public const int ObjectsPerFrame = 4096;
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
        private sealed class ActiveScan
        {
            public ParkRuntimeSnapshot Snapshot;
            public Geometry Geometry;
            public List<Triangle2> Triangles;
            public ParkLandscapeScan<Entity> Objects;
            public ParkLandscapeTally Tally = new ParkLandscapeTally();
            public List<(Entity Root, int TransportType)> Transit = new List<(Entity Root, int TransportType)>();
        }
        private ActiveScan current;
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
                // Owned sub-objects (building/net furniture) are never landscape, so their updates are ignored.
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Game.Buildings.Building>(), ComponentType.ReadOnly<Owner>() } });
            changedNets = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Game.Net.Edge>(), ComponentType.ReadOnly<Game.Net.Curve>() },
                Any = new[] { ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Updated>(), ComponentType.ReadOnly<Deleted>() },
                None = new[] { ComponentType.ReadOnly<Temp>() } });
        }
        [Preserve]
        protected override void OnUpdate()
        {
            if (!runtime.Ready) return;
            if (loadGeneration != runtime.LoadGeneration) { loadGeneration = runtime.LoadGeneration; scheduler.Clear(); parksDirty = true; current = null; propCategories.Clear(); }
            if (!changedParks.IsEmptyIgnoreFilter) parksDirty = true;
            if (parksDirty || frameCounter % 256 == 0) SyncParks();
            if (scheduler.TrackedCount == 0) { frameCounter++; return; }
            MarkChanged(changedObjects, true);
            MarkChanged(changedNets, false);
            frameCounter++;
            if (current != null) { Continue(); return; }
            if (frameCounter % ScanFrameInterval != 0) return;
            var next = scheduler.Take(runtime.SimulationFrame, 1);
            if (next.Count > 0) Begin(next[0]);
        }
        private void SyncParks()
        {
            parksDirty = false;
            runtime.CollectParks(parks);
            runtime.PruneStore(parks);
            runtime.EnsureParkState(parks);
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
        private void Begin(ulong id)
        {
            if (!runtime.Store.TryGet(id, out var snapshot) || !EntityManager.Exists(snapshot.Area) || !EntityManager.HasComponent<Geometry>(snapshot.Area)) return;
            try
            {
                var geometry = EntityManager.GetComponentData<Geometry>(snapshot.Area);
                var candidates = new List<Entity>();
                var search = new Bounds2(geometry.m_Bounds.xz.min - TransitRadius, geometry.m_Bounds.xz.max + TransitRadius);
                var tree = objectSearch.GetStaticSearchTree(true, out var dependency);
                dependency.Complete();
                var iterator = new BoundsIterator { Bounds = search, Items = candidates };
                tree.Iterate(ref iterator);
                current = new ActiveScan { Snapshot = snapshot, Geometry = geometry, Triangles = ParkGeometry.Triangles(EntityManager, snapshot.Area),
                    Objects = new ParkLandscapeScan<Entity>(candidates) };
                Continue();
            }
            catch (System.Exception error) { Fail(error); }
        }
        private void Continue()
        {
            var scan = current;
            try
            {
                // The Park may be deleted or edited while a sliced scan runs: drop the partial result.
                if (!EntityManager.Exists(scan.Snapshot.Area) || !EntityManager.HasComponent<Geometry>(scan.Snapshot.Area)) { current = null; return; }
                scan.Objects.Process(ObjectsPerFrame, entity => Classify(scan, entity));
                if (!scan.Objects.Done) return;
                current = null;
                ScanCount++;
                float pathLength = MeasurePaths(scan.Geometry.m_Bounds.xz, scan.Triangles);
                var snapshot = scan.Snapshot;
                snapshot.AreaSquareMeters = scan.Geometry.m_SurfaceArea;
                var landscape = new ParkLandscapeSnapshot {
                    Inputs = scan.Tally.ToInputs(scan.Geometry.m_SurfaceArea, pathLength), ScannedFrame = runtime.SimulationFrame, Available = true };
                var transit = ParkTransitAccess.Summarize(scan.Transit);
                landscape.TransitPoints = transit.Points; landscape.TransitTypes = transit.Types;
                landscape.Result = ParkLandscapeModel.ComputeLandscape(landscape.Inputs);
                snapshot.Landscape = landscape;
                failureLogged = false;
            }
            catch (System.Exception error) { Fail(error); }
        }
        // Keep the last good snapshot; without one the metric stays unavailable (neutral in Quality).
        private void Fail(System.Exception error)
        {
            current = null;
            if (!failureLogged) { failureLogged = true; Mod.Log.Error(error, "Park landscape analysis failed; last result kept"); }
        }
        private void Classify(ActiveScan scan, Entity entity)
        {
            if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<Transform>(entity)) return;
            var position = EntityManager.GetComponentData<Transform>(entity).m_Position.xz;
            if (IsTransit(entity))
            {
                if (ParkGeometry.Distance(scan.Triangles, position) <= TransitRadius) scan.Transit.Add((OwnerRoot(entity), TransportType(entity)));
                return;
            }
            if (!ParkGeometry.Contains(scan.Triangles, position)) return;
            var traits = ReadObject(entity, out int prefabKey);
            scan.Tally.Add(ParkLandscapeClassifier.Classify(traits), prefabKey);
        }
        private bool IsTransit(Entity entity) =>
            !EntityManager.HasComponent<Temp>(entity) && !EntityManager.HasComponent<Deleted>(entity) &&
            (EntityManager.HasComponent<Game.Routes.TransportStop>(entity) || EntityManager.HasComponent<Game.Buildings.PublicTransportStation>(entity));
        private Entity OwnerRoot(Entity entity)
        {
            for (int depth = 0; depth < 8 && EntityManager.HasComponent<Owner>(entity); depth++)
            {
                var owner = EntityManager.GetComponentData<Owner>(entity).m_Owner;
                if (owner == Entity.Null || !EntityManager.Exists(owner)) break;
                entity = owner;
            }
            return entity;
        }
        // Transport mode of a stop prefab (+1 so 0 stays "unknown", e.g. a station building).
        private int TransportType(Entity entity)
        {
            if (!EntityManager.HasComponent<PrefabRef>(entity)) return 0;
            var prefab = EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
            return EntityManager.Exists(prefab) && EntityManager.HasComponent<TransportStopData>(prefab)
                ? (int)EntityManager.GetComponentData<TransportStopData>(prefab).m_TransportType + 1 : 0;
        }
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
            if (traits.IsPlaceable) traits.PropCategory = PropCategory(prefab);
            return traits;
        }
        // Menu category per prefab entity, resolved once through the managed prefab (UIObject.m_Group).
        private readonly Dictionary<Entity, ParkPropCategory> propCategories = new Dictionary<Entity, ParkPropCategory>();
        private PrefabSystem prefabSystem;
        private ParkPropCategory PropCategory(Entity prefab)
        {
            if (propCategories.TryGetValue(prefab, out var cached)) return cached;
            var category = ParkPropCategory.None;
            prefabSystem ??= World.GetOrCreateSystemManaged<PrefabSystem>();
            if (prefabSystem.TryGetPrefab<PrefabBase>(prefab, out var managed) && managed.TryGet<UIObject>(out var ui) && ui.m_Group != null)
            {
                string icon = ui.m_Group.TryGet<UIObject>(out var groupUi) ? groupUi.m_Icon : null;
                category = ParkPropCategories.FromGroup(ui.m_Group.name, icon);
            }
            propCategories[prefab] = category;
            return category;
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
