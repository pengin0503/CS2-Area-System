using System.Collections.Generic;
using Colossal.Mathematics;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Core.Persistence;
using Game;
using Game.Areas;
using Game.Common;
using Game.SceneFlow;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace CS2AreaSystem.Modules.Park
{
    public readonly struct ParkAreaRef
    {
        public Entity Area { get; }
        public ulong StableId { get; }
        public AreaLifecycleState State { get; }
        public ParkAreaRef(Entity area, ulong id, AreaLifecycleState state) { Area = area; StableId = id; State = state; }
    }
    // Host of Park runtime-only data shared by the Park systems: snapshots and visit sessions. Holds no
    // authoritative state; everything here is rebuilt after a load.
    public partial class ParkRuntimeSystem : GameSystemBase
    {
        public const uint FramesPerDay = ParkFinanceModel.FramesPerDay;
        public ParkRuntimeStore Store { get; } = new ParkRuntimeStore();
        private EntityQuery managedAreas;
        private AreaCoreServicesSystem services;
        public bool Ready => !GameManager.instance.isGameLoading && World.GetOrCreateSystemManaged<AreaLoadCoordinatorSystem>().Ready;
        // Incremented on every load so dependent systems can re-seed their runtime state.
        public int LoadGeneration { get; private set; }
        protected override void OnCreate()
        {
            base.OnCreate();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            managedAreas = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<ManagedArea>(), ComponentType.ReadOnly<ManagedAreaStableId>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
        }
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode) { Store.Clear(); LoadGeneration++; }
        protected override void OnUpdate() { }
        public void CollectParks(List<ParkAreaRef> parks)
        {
            parks.Clear();
            using var entities = managedAreas.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var managed = EntityManager.GetComponentData<ManagedArea>(entity);
                if (managed.TypeId != (int)AreaTypeId.Park || services.IsQuarantined(entity)) continue;
                parks.Add(new ParkAreaRef(entity, EntityManager.GetComponentData<ManagedAreaStableId>(entity).Value, managed.State));
            }
        }
        // Drops snapshots of Parks that no longer exist.
        public void PruneStore(List<ParkAreaRef> parks)
        {
            var alive = new HashSet<ulong>();
            foreach (var park in parks) alive.Add(park.StableId);
            var stale = new List<ulong>();
            foreach (var snapshot in Store.All) if (!alive.Contains(snapshot.StableId)) stale.Add(snapshot.StableId);
            foreach (var id in stale) Store.Remove(id);
        }
        // Adds Park authoritative state (and the shared progress/settings components) to Park areas only.
        // Existing state is never replaced, so suspend/reactivate and load keep progression. Structural change:
        // call from a modification-phase system.
        public void EnsureParkState(List<ParkAreaRef> parks)
        {
            foreach (var park in parks)
            {
                if (!EntityManager.Exists(park.Area)) continue;
                if (!EntityManager.HasComponent<ParkAreaState>(park.Area)) EntityManager.AddComponentData(park.Area, ParkAreaState.CreateDefault());
                if (!EntityManager.HasComponent<ManagedAreaProgress>(park.Area)) EntityManager.AddComponentData(park.Area, ParkStateDefaults.Progress());
                if (!EntityManager.HasComponent<ManagedAreaSettings>(park.Area)) EntityManager.AddComponentData(park.Area, ParkStateDefaults.Settings());
            }
        }
        public AreaMembershipIndex Membership => World.GetOrCreateSystemManaged<AreaMembershipSystem>().Index;
        public uint SimulationFrame => World.GetOrCreateSystemManaged<Game.Simulation.SimulationSystem>().frameIndex;
    }
    // Park polygon helpers over the native area triangles (same source as Core membership).
    public static class ParkGeometry
    {
        public static List<Triangle2> Triangles(EntityManager entities, Entity area)
        {
            var result = new List<Triangle2>();
            if (!entities.HasBuffer<Node>(area) || !entities.HasBuffer<Triangle>(area)) return result;
            var nodes = entities.GetBuffer<Node>(area, true);
            var triangles = entities.GetBuffer<Triangle>(area, true);
            for (int i = 0; i < triangles.Length; i++) result.Add(AreaUtils.GetTriangle2(nodes, triangles[i]));
            return result;
        }
    }
}
namespace CS2AreaSystem.Modules.Park
{
    // Park triangles with per-triangle bounds: containment/distance queries test only triangles whose bounds can
    // matter, instead of every triangle for every scanned object.
    public sealed class ParkPolygon
    {
        private const float Epsilon = 1e-3f;
        private readonly List<Triangle2> triangles;
        private readonly Bounds2[] bounds;
        private readonly Bounds2 overall;
        public int TrianglesTestedLastQuery { get; private set; }
        public ParkPolygon(List<Triangle2> triangles)
        {
            this.triangles = triangles ?? new List<Triangle2>();
            bounds = new Bounds2[this.triangles.Count];
            overall = new Bounds2(new float2(float.MaxValue), new float2(float.MinValue));
            for (int i = 0; i < bounds.Length; i++)
            {
                var t = this.triangles[i];
                bounds[i] = new Bounds2(math.min(t.a, math.min(t.b, t.c)) - Epsilon, math.max(t.a, math.max(t.b, t.c)) + Epsilon);
                overall = new Bounds2(math.min(overall.min, bounds[i].min), math.max(overall.max, bounds[i].max));
            }
        }
        private static bool Inside(Bounds2 box, float2 p) => math.all(p >= box.min) && math.all(p <= box.max);
        private static float BoxDistance(Bounds2 box, float2 p) => math.length(math.max(0f, math.max(box.min - p, p - box.max)));
        public bool Contains(float2 point)
        {
            TrianglesTestedLastQuery = 0;
            if (bounds.Length == 0 || !Inside(overall, point)) return false;
            for (int i = 0; i < bounds.Length; i++)
            {
                if (!Inside(bounds[i], point)) continue;
                TrianglesTestedLastQuery++;
                if (NativeMembershipGeometry.Contains(triangles[i], point)) return true;
            }
            return false;
        }
        public float Distance(float2 point)
        {
            float best = float.MaxValue;
            for (int i = 0; i < bounds.Length; i++)
            {
                // The distance to a triangle is never smaller than the distance to its (slightly enlarged) bounds.
                if (BoxDistance(bounds[i], point) - Epsilon >= best) continue;
                best = math.min(best, MathUtils.Distance(triangles[i], point, out _));
                if (best <= 0f) break;
            }
            return best;
        }
    }
}
