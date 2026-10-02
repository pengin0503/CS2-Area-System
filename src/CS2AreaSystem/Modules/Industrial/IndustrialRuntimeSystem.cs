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
    // attaches missing authoritative state. It performs no economic observation. The Area list is rebuilt only through
    // an AreaChangeGate (#61): ManagedArea writes (state changes, added Areas) are seen by a change-version filter,
    // removals by the Created/Updated/Deleted tags, loads and quarantine by their counters, and a fallback reconciles
    // every AreaChangeGate.DefaultFallbackUpdates updates. Unchanged frames only check that evidence. The runtime is the
    // last system of ModificationEnd, after activation, so the GameSimulation systems read a list refreshed this frame.
    public partial class IndustrialRuntimeSystem : GameSystemBase
    {
        public IndustrialRuntimeStore Store { get; } = new IndustrialRuntimeStore();
        public IndustrialStructureTracker Structure { get; } = new IndustrialStructureTracker();
        private EntityQuery managedAreas, writtenAreas, changedAreas, timeData, rentersUpdated, damagedMembers;
        private List<IndustrialAreaRef> areas = new List<IndustrialAreaRef>(), building = new List<IndustrialAreaRef>();
        private readonly List<(Entity Area, ManagedArea Managed, ulong StableId)> rows = new List<(Entity Area, ManagedArea Managed, ulong StableId)>();
        private readonly AreaChangeGate gate = new AreaChangeGate();
        private IndustrialMembershipCycleSource cycleSource;
        // Industrial Areas of the current list may still lack authoritative state (the game clock was not available).
        private bool statePending;
        private AreaCoreServicesSystem services;
        public long AreaListRefreshes => gate.Refreshes;
        public long AreaListSkips => gate.Skips;
        public bool Ready => !GameManager.instance.isGameLoading && World.GetOrCreateSystemManaged<AreaLoadCoordinatorSystem>().Ready;
        // Incremented on every load so dependent systems drop cycles, baselines and windows.
        public int LoadGeneration { get; private set; }
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            services = World.GetOrCreateSystemManaged<AreaCoreServicesSystem>();
            Store.Initialize=snapshot=>{try{IndustrialHistorySystem.Seed(EntityManager,snapshot);}catch(System.Exception error){services.Failures.Report(snapshot.Area,"Industrial.History.Seed",error,"Saved trends unavailable; current observations continue");}};
            managedAreas = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<ManagedArea>(), ComponentType.ReadOnly<ManagedAreaStableId>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() } });
            writtenAreas = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<ManagedArea>(), ComponentType.ReadOnly<ManagedAreaStableId>() },
                None = new[] { ComponentType.ReadOnly<Temp>() } });
            writtenAreas.SetChangedVersionFilter(ComponentType.ReadOnly<ManagedArea>());
            // A removed entity can leave no changed chunk behind, so removals are taken from the tags.
            changedAreas = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<ManagedArea>() },
                Any = new[] { ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Updated>(), ComponentType.ReadOnly<Deleted>() },
                None = new[] { ComponentType.ReadOnly<Temp>() } });
            cycleSource = new IndustrialMembershipCycleSource(Membership, Structure);
            timeData = GetEntityQuery(ComponentType.ReadOnly<TimeData>());
            // Native change evidence for tenant/role structure (design §18.2): vanilla tenant events and member
            // destruction/abandonment. Ordinary economic changes carry no such evidence.
            rentersUpdated = GetEntityQuery(ComponentType.ReadOnly<Game.Common.Event>(), ComponentType.ReadOnly<Game.Buildings.RentersUpdated>());
            damagedMembers = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Game.Buildings.Building>(), ComponentType.ReadOnly<Updated>() },
                Any = new[] { ComponentType.ReadOnly<Destroyed>(), ComponentType.ReadOnly<Game.Buildings.Abandoned>() },
                None = new[] { ComponentType.ReadOnly<Temp>() } });
        }
        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            Store.Clear(); Structure.Clear(); cycleSource.Clear(); areas.Clear(); gate.Invalidate(); LoadGeneration++;
        }
        public IIndustrialCycleSource CycleSource => cycleSource;
        [Preserve]
        protected override void OnUpdate()
        {
            // Change versions advance while not Ready; the first Ready update rebuilds.
            if (!Ready) { gate.Invalidate(); return; }
            try
            {
                int generation = services.Identity.LoadGeneration, quarantine = services.Quarantine.Revision;
                if (gate.ShouldRefresh(!writtenAreas.IsEmpty || !changedAreas.IsEmptyIgnoreFilter, generation, quarantine))
                {
                    RefreshAreas();
                    PruneStore(areas);
                    statePending = true;
                    gate.Refreshed(generation, quarantine);
                }
                if (statePending) statePending = !EnsureIndustrialState(areas);
                TrackStructureChanges();
                services.Failures.Recovered(Entity.Null, "Industrial.Runtime");
            }
            catch (System.Exception error)
            {
                services.Failures.Report(Entity.Null, "Industrial.Runtime", error, "Industrial runtime update failed; Area Core continues");
            }
        }
        // The Industrial Areas as of this frame's ModificationEnd (a copy; the runtime keeps its own list).
        public void CollectAreas(List<IndustrialAreaRef> result)
        {
            result.Clear();
            result.AddRange(areas);
        }
        // Builds the new list aside and swaps it in, so a failed rebuild leaves the previous list and the gate open.
        private void RefreshAreas()
        {
            using var entities = managedAreas.ToEntityArray(Allocator.Temp);
            rows.Clear();
            foreach (var entity in entities)
                if (!services.IsQuarantined(entity)) rows.Add((entity, EntityManager.GetComponentData<ManagedArea>(entity), EntityManager.GetComponentData<ManagedAreaStableId>(entity).Value));
            IndustrialAreaSelection.Select(rows, building);
            rows.Clear();
            (areas, building) = (building, areas);
        }
        // Drops runtime snapshots, structure tokens and member lists of deleted Industrial Areas; Suspended Areas stay.
        public void PruneStore(List<IndustrialAreaRef> alive)
        {
            Store.Prune(alive);
            Structure.RetainOnly(alive);
            cycleSource.RetainOnly(alive);
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
        // False while the game clock is unavailable; the runtime retries every update until it succeeds.
        public bool EnsureIndustrialState(List<IndustrialAreaRef> list)
        {
            if (!TryGetClock(out var clock)) return false;
            IndustrialStateInitializer.Ensure(list, new EntityTarget(EntityManager), clock.Day(SimulationFrame));
            return true;
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
