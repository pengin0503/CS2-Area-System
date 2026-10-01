using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Core.Persistence;
using Game;
using Game.Common;
using Game.SceneFlow;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Industrial
{
    public readonly struct IndustrialAreaRef
    {
        public Entity Area { get; }
        public ulong StableId { get; }
        public AreaLifecycleState State { get; }
        public IndustrialAreaRef(Entity area, ulong id, AreaLifecycleState state) { Area = area; StableId = id; State = state; }
    }
    public static class IndustrialAreaSelection
    {
        public static void Select(IEnumerable<(Entity Area, ManagedArea Managed, ulong StableId)> rows, List<IndustrialAreaRef> result)
        {
            result.Clear();
            foreach (var row in rows)
                if (row.Managed.TypeId == (int)AreaTypeId.Industrial) result.Add(new IndustrialAreaRef(row.Area, row.StableId, row.Managed.State));
        }
    }
    // Structural target for authoritative-state initialization (an ECS adapter in game, a fake in tests).
    public interface IIndustrialStateTarget
    {
        bool Exists(Entity area);
        bool HasState(Entity area);
        void AddState(Entity area, IndustrialAreaState state);
        bool HasProgress(Entity area);
        void AddProgress(Entity area, ManagedAreaProgress progress);
    }
    public static class IndustrialStateInitializer
    {
        // Adds IndustrialAreaState (current game day identities) and shared progress to Industrial Areas that lack
        // them. Existing components are never replaced, so suspend/reactivate, HQ reassignment and load keep history.
        // No Industrial budget/settings component exists in v1.
        public static int Ensure(IEnumerable<IndustrialAreaRef> areas, IIndustrialStateTarget target, uint currentDay)
        {
            int added = 0;
            foreach (var area in areas)
            {
                if (!target.Exists(area.Area)) continue;
                if (!target.HasState(area.Area)) { target.AddState(area.Area, IndustrialAreaState.CreateDefault(currentDay)); added++; }
                if (!target.HasProgress(area.Area))
                    target.AddProgress(area.Area, new ManagedAreaProgress { SchemaVersion = ManagedAreaProgress.CurrentSchemaVersion, Level = IndustrialProgressionLevels.MinLevel });
            }
            return added;
        }
    }
    public static class IndustrialProgressionLevels
    {
        public const int MinLevel = 1, MaxLevel = 5;
        public static int Normalize(int level) => level < MinLevel ? MinLevel : level > MaxLevel ? MaxLevel : level;
    }
    // Host of Industrial runtime-only data. ModificationEnd only: collects Industrial Areas, prunes deleted ones and
    // attaches missing authoritative state. It performs no economic observation.
    public partial class IndustrialRuntimeSystem : GameSystemBase
    {
        public IndustrialRuntimeStore Store { get; } = new IndustrialRuntimeStore();
        public IndustrialStructureTracker Structure { get; } = new IndustrialStructureTracker();
        private EntityQuery managedAreas, timeData, rentersUpdated, damagedMembers;
        private readonly List<IndustrialAreaRef> areas = new List<IndustrialAreaRef>();
        private bool failureLogged;
        public bool Ready => !GameManager.instance.isGameLoading && World.GetOrCreateSystemManaged<AreaLoadCoordinatorSystem>().Ready;
        // Incremented on every load so dependent systems drop cycles, baselines and windows.
        public int LoadGeneration { get; private set; }
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            managedAreas = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<ManagedArea>(), ComponentType.ReadOnly<ManagedAreaStableId>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() } });
            timeData = GetEntityQuery(ComponentType.ReadOnly<TimeData>());
            // Native change evidence for tenant/role structure (design §18.2): vanilla tenant events and member
            // destruction/abandonment. Ordinary economic changes carry no such evidence.
            rentersUpdated = GetEntityQuery(ComponentType.ReadOnly<Game.Common.Event>(), ComponentType.ReadOnly<Game.Buildings.RentersUpdated>());
            damagedMembers = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Game.Buildings.Building>(), ComponentType.ReadOnly<Updated>() },
                Any = new[] { ComponentType.ReadOnly<Destroyed>(), ComponentType.ReadOnly<Game.Buildings.Abandoned>() },
                None = new[] { ComponentType.ReadOnly<Temp>() } });
        }
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode) { Store.Clear(); Structure.Clear(); LoadGeneration++; }
        public IIndustrialCycleSource CycleSource => new IndustrialMembershipCycleSource(Membership, Structure);
        [Preserve]
        protected override void OnUpdate()
        {
            if (!Ready) return;
            try
            {
                CollectAreas(areas);
                PruneStore(areas);
                EnsureIndustrialState(areas);
                TrackStructureChanges();
                failureLogged = false;
            }
            catch (System.Exception error)
            {
                if (!failureLogged) { failureLogged = true; Mod.Log.Error(error, "Industrial runtime update failed; Area Core continues"); }
            }
        }
        public void CollectAreas(List<IndustrialAreaRef> result)
        {
            using var entities = managedAreas.ToEntityArray(Allocator.Temp);
            var rows = new List<(Entity, ManagedArea, ulong)>(entities.Length);
            foreach (var entity in entities)
                rows.Add((entity, EntityManager.GetComponentData<ManagedArea>(entity), EntityManager.GetComponentData<ManagedAreaStableId>(entity).Value));
            IndustrialAreaSelection.Select(rows, result);
        }
        public void PruneStore(List<IndustrialAreaRef> alive)
        {
            Store.Prune(alive);
            var ids = new HashSet<ulong>();
            foreach (var area in alive) ids.Add(area.StableId);
            foreach (var snapshot in new List<IndustrialRuntimeSnapshot>(Store.All)) if (!ids.Contains(snapshot.StableId)) Structure.Remove(snapshot.StableId);
        }
        private void TrackStructureChanges()
        {
            if (!rentersUpdated.IsEmptyIgnoreFilter)
            {
                using var events = rentersUpdated.ToComponentDataArray<Game.Buildings.RentersUpdated>(Allocator.Temp);
                foreach (var e in events) Structure.MemberChanged(e.m_Property, Membership);
            }
            if (!damagedMembers.IsEmptyIgnoreFilter)
            {
                using var buildings = damagedMembers.ToEntityArray(Allocator.Temp);
                foreach (var building in buildings) Structure.MemberChanged(building, Membership);
            }
        }
        // Structural change: call from the modification phase only.
        public void EnsureIndustrialState(List<IndustrialAreaRef> list)
        {
            if (!TryGetClock(out var clock)) return;
            IndustrialStateInitializer.Ensure(list, new EntityTarget(EntityManager), clock.Day(SimulationFrame));
        }
        public bool TryGetClock(out IndustrialGameClock clock)
        {
            clock = default;
            if (timeData.IsEmptyIgnoreFilter) return false;
            clock = IndustrialGameClock.From(timeData.GetSingleton<TimeData>());
            return clock.IsValid;
        }
        public AreaMembershipIndex Membership => World.GetOrCreateSystemManaged<AreaMembershipSystem>().Index;
        public uint SimulationFrame => World.GetOrCreateSystemManaged<Game.Simulation.SimulationSystem>().frameIndex;
        private sealed class EntityTarget : IIndustrialStateTarget
        {
            private readonly EntityManager entities;
            public EntityTarget(EntityManager entities) => this.entities = entities;
            public bool Exists(Entity area) => entities.Exists(area);
            public bool HasState(Entity area) => entities.HasComponent<IndustrialAreaState>(area);
            public void AddState(Entity area, IndustrialAreaState state) => entities.AddComponentData(area, state);
            public bool HasProgress(Entity area) => entities.HasComponent<ManagedAreaProgress>(area);
            public void AddProgress(Entity area, ManagedAreaProgress progress) => entities.AddComponentData(area, progress);
        }
    }
}
