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
        public bool Ready => !GameManager.instance.isGameLoading && World.GetOrCreateSystemManaged<AreaLoadCoordinatorSystem>().Ready;
        // Incremented on every load so dependent systems can re-seed their runtime state.
        public int LoadGeneration { get; private set; }
        protected override void OnCreate()
        {
            base.OnCreate();
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
                if (managed.TypeId != (int)AreaTypeId.Park) continue;
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
        public static bool Contains(List<Triangle2> triangles, float2 point)
        {
            foreach (var triangle in triangles) if (NativeMembershipGeometry.Contains(triangle, point)) return true;
            return false;
        }
        public static float Distance(List<Triangle2> triangles, float2 point)
        {
            float best = float.MaxValue;
            foreach (var triangle in triangles) best = math.min(best, MathUtils.Distance(triangle, point, out _));
            return best;
        }
    }
}
