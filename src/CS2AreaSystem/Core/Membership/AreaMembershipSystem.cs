using System.Collections.Generic;
using Colossal.Collections;
using Colossal.Mathematics;
using CS2AreaSystem.Core.Areas;
using Game;
using Game.Areas;
using Game.Common;
using Game.Objects;
using Game.SceneFlow;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.Membership
{
    // Derived only: no serialized membership components or duplicate polygon store.
    public partial class AreaMembershipSystem : GameSystemBase, IAreaMembershipSpatialSource
    {
        private Game.Areas.SearchSystem areaSearch;
        private Game.Objects.SearchSystem objectSearch;
        private EntityQuery allMembers, changedMembers, changedAreas, managedAreas;
        private readonly HashSet<Entity> areaCandidates = new HashSet<Entity>();
        private NativeQuadTree<AreaSearchItem, QuadTreeBoundsXZ> batchAreaTree;
        private bool batchActive;
        private AreaCoreServicesSystem services;
        private AreaMembershipUpdater updater;
        private bool rebuildPending = true;
        public AreaMembershipIndex Index { get; } = new AreaMembershipIndex();
        public int FullRebuildCount => updater.FullRebuildCount;
        public int IncrementalMemberUpdateCount => updater.IncrementalMemberUpdateCount;
        public int IncrementalAreaUpdateCount => updater.IncrementalAreaUpdateCount;
        public void RequestRebuild() { rebuildPending = true; Index.Clear(); }
        public bool HasManagedAreas => !managedAreas.IsEmptyIgnoreFilter;
        public void RebuildNow()
        {
            BeginBatch();
            try { updater.Rebuild(); rebuildPending = false; }
            finally { EndBatch(); }
        }
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            areaSearch = World.GetOrCreateSystemManaged<Game.Areas.SearchSystem>();
            objectSearch = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            updater = new AreaMembershipUpdater(Index, this);
            // Design §9.4: v1 members are building-like entities. Trees, props and other static objects are excluded.
            allMembers = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Game.Objects.Object>(), ComponentType.ReadOnly<Static>(), ComponentType.ReadOnly<Transform>(), ComponentType.ReadOnly<Game.Buildings.Building>() },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Overridden>() }
            });
            changedMembers = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Game.Objects.Object>(), ComponentType.ReadOnly<Static>(), ComponentType.ReadOnly<Transform>(), ComponentType.ReadOnly<Game.Buildings.Building>() },
                Any = new[] { ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Updated>(), ComponentType.ReadOnly<Deleted>() },
                None = new[] { ComponentType.ReadOnly<Temp>() }
            });
            changedAreas = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<ManagedArea>(), ComponentType.ReadOnly<ManagedAreaStableId>() },
                Any = new[] { ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Updated>(), ComponentType.ReadOnly<Deleted>() },
                None = new[] { ComponentType.ReadOnly<Temp>() }
            });
            managedAreas = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<ManagedArea>(), ComponentType.ReadOnly<ManagedAreaStableId>() },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() }
            });
        }
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode) => RequestRebuild();
        [Preserve]
        protected override void OnUpdate()
        {
            if (GameManager.instance.isGameLoading || !World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Persistence.AreaLoadCoordinatorSystem>().Ready) return;
            if (rebuildPending) { RebuildNow(); return; }
            if (changedAreas.IsEmptyIgnoreFilter && changedMembers.IsEmptyIgnoreFilter) return;
            BeginBatch();
            try
            {
                using var areas = changedAreas.ToEntityArray(Allocator.Temp);
                foreach (var area in areas)
                {
                    // A quarantined Area never entered the index; its (zero or shared) ID must not touch another Area's entry.
                    if (services.IsQuarantined(area)) continue;
                    updater.UpdateArea(area, EntityManager.GetComponentData<ManagedAreaStableId>(area).Value, EntityManager.HasComponent<Deleted>(area));
                }
                using var members = changedMembers.ToEntityArray(Allocator.Temp);
                foreach (var member in members) updater.UpdateMember(member);
            }
            finally { EndBatch(); }
        }
        // All membership work runs synchronously on the main thread; fetch the area search tree once per batch.
        private void BeginBatch()
        {
            batchAreaTree = areaSearch.GetSearchTree(true, out var dependency);
            dependency.Complete();
            batchActive = true;
        }
        private void EndBatch() { batchActive = false; batchAreaTree = default; }
        public IEnumerable<Entity> GetAllMembers()
        {
            using var members = allMembers.ToEntityArray(Allocator.Temp);
            return members.ToArray();
        }
        // Building candidates inside the Area's bounds. The returned set is reused: it is valid until the next call.
        public IEnumerable<Entity> GetSpatialCandidates(Entity area)
        {
            spatialCandidates.Clear();
            if (!EntityManager.HasComponent<Geometry>(area)) return spatialCandidates;
            var iterator = new MembershipObjectIterator<BuildingMemberFilter> {
                Bounds = EntityManager.GetComponentData<Geometry>(area).m_Bounds.xz, Candidates = spatialCandidates,
                Filter = new BuildingMemberFilter { Buildings = GetComponentLookup<Game.Buildings.Building>(true), Deleted = GetComponentLookup<Deleted>(true) } };
            var tree = objectSearch.GetStaticSearchTree(true, out var dependency);
            dependency.Complete();
            tree.Iterate(ref iterator);
            SpatialObjectsVisited += iterator.Visited; SpatialCandidatesAccepted += iterator.Accepted;
            return spatialCandidates;
        }
        private readonly HashSet<Entity> spatialCandidates = new HashSet<Entity>();
        // Diagnostics counters (#51): static objects examined and building candidates kept by boundary searches.
        public long SpatialObjectsVisited { get; private set; }
        public long SpatialCandidatesAccepted { get; private set; }
        public IReadOnlyList<AreaMembership> FindContainingAreas(Entity member)
        {
            var results = new List<AreaMembership>();
            if (!IsCandidate(member)) return results;
            areaCandidates.Clear();
            var iterator = new AreaIterator { Position = EntityManager.GetComponentData<Transform>(member).m_Position.xz, Candidates = areaCandidates };
            if (batchActive) batchAreaTree.Iterate(ref iterator);
            else
            {
                var tree = areaSearch.GetSearchTree(true, out var dependency);
                dependency.Complete();
                tree.Iterate(ref iterator);
            }
            var containing = new List<ContainingArea>();
            foreach (var area in iterator.Candidates)
            {
                if (!EntityManager.Exists(area) || services.IsQuarantined(area) || EntityManager.HasComponent<Deleted>(area) || EntityManager.HasComponent<Temp>(area) ||
                    !EntityManager.HasComponent<ManagedArea>(area) || !EntityManager.HasComponent<ManagedAreaStableId>(area) ||
                    !EntityManager.HasBuffer<Node>(area) || !EntityManager.HasBuffer<Triangle>(area)) continue;
                var nodes = EntityManager.GetBuffer<Node>(area, true);
                var triangles = EntityManager.GetBuffer<Triangle>(area, true);
                foreach (var triangle in triangles)
                    if (NativeMembershipGeometry.Contains(AreaUtils.GetTriangle2(nodes, triangle), iterator.Position))
                    {
                        var typeId = EntityManager.GetComponentData<ManagedArea>(area).TypeId;
                        services.Registry.TryGet(typeId, out var descriptor);
                        containing.Add(new ContainingArea(area, EntityManager.GetComponentData<ManagedAreaStableId>(area).Value, typeId, descriptor?.Module));
                        break;
                    }
            }
            if (containing.Count == 0) return results;
            return AreaMembershipClassification.Collect(containing, member, (typeId, error) => {
                services.Failures.Report(Entity.Null, "Membership.Classifier:" + typeId, error, "Area classifier unavailable: " + typeId);
            });
        }
        private bool IsCandidate(Entity entity) => EntityManager.Exists(entity) && AreaMembershipCandidatePolicy.IsCandidate(
            EntityManager.HasComponent<Transform>(entity), EntityManager.HasComponent<Static>(entity), EntityManager.HasComponent<Game.Buildings.Building>(entity),
            EntityManager.HasComponent<Deleted>(entity), EntityManager.HasComponent<Temp>(entity), EntityManager.HasComponent<Overridden>(entity));
        private struct AreaIterator : INativeQuadTreeIterator<AreaSearchItem, QuadTreeBoundsXZ>
        {
            public float2 Position;
            public HashSet<Entity> Candidates;
            public bool Intersect(QuadTreeBoundsXZ bounds) => math.all(Position >= bounds.m_Bounds.xz.min) && math.all(Position <= bounds.m_Bounds.xz.max);
            public void Iterate(QuadTreeBoundsXZ bounds, AreaSearchItem item) { if (Intersect(bounds)) Candidates.Add(item.m_Area); }
        }
    }
}
