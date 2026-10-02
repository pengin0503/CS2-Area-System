using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Membership;
using CS2AreaSystem.Core.Modules;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    // Read-only boundary between the Industrial panel and gameplay state. It has no setters. Its only action is the
    // read-only diagnostic camera focus (Wave A3); HQ assignment, Edit and Delete stay in Core's guarded generic actions.
    public static class IndustrialGameplayFacade
    {
        public const string FocusDiagnosticTarget = "focusDiagnosticTarget";
        public const string AssignCompanyHere = "assignCompanyHere";
        public const string ClearCompanyAssignment = "clearCompanyAssignment";
        // Core already checked the stable ID and live selection. Never modifies simulation state.
        public static bool HandleAction(Entity area, string action, string value, in AreaPanelContext context)
        {
            if (action != FocusDiagnosticTarget && action != AssignCompanyHere && action != ClearCompanyAssignment) return false;
            if (context.World == null) return false;
            var entities = context.Entities;
            if (!entities.Exists(area) || entities.HasComponent<Game.Common.Deleted>(area) || !entities.HasComponent<ManagedAreaStableId>(area) ||
                !entities.HasComponent<ManagedArea>(area) || entities.GetComponentData<ManagedArea>(area).TypeId != (int)AreaTypeId.Industrial) return false;
            var runtime = context.World.GetExistingSystemManaged<IndustrialRuntimeSystem>();
            ulong areaId = entities.GetComponentData<ManagedAreaStableId>(area).Value;
            if (runtime == null || !runtime.Store.TryGet(areaId, out var snapshot)) return false;
            if (action == FocusDiagnosticTarget)
                return TryFocus(snapshot.Current, value, areaId, e => entities.Exists(e) && !entities.HasComponent<Game.Common.Deleted>(e), runtime.Membership, context.Focus);
            var services = context.World.GetExistingSystemManaged<AreaCoreServicesSystem>();
            if (!runtime.Ready || !runtime.AssignmentsReady || services == null || services.IsQuarantined(area)) return false;
            // Refresh invalid references first: a changed revision rejects the rendered snapshot key.
            runtime.RefreshAssignments();
            bool applied = IndustrialCompanyAssignmentActions.TryApply(action, value, areaId, snapshot.OwnershipCandidates, runtime.Assignments, company => {
                var eligibility = IndustrialCompanyOwnership.ReadLive(company, entities, services.Semantics, out var building);
                return new IndustrialCompanyLive(company, building, eligibility);
            }, runtime.Membership, new EntityAssignmentStore(runtime, entities));
            if (applied) runtime.AssignmentChanged();
            return applied;
        }
        public static bool TryFocus(IndustrialObservationResult current, string key, ulong areaId, Func<Entity, bool> isLive, AreaMembershipIndex membership, Action<Entity> focus)
        {
            if (focus == null || !TryResolveFocusTarget(current, key, areaId, isLive, membership, out var target)) return false;
            focus(target);
            return true;
        }
        // The key resolves only against targets of the current snapshot's bottlenecks; the entity must still exist and
        // still be primary-owned by this Industrial Area in Core membership. No world scan beyond these lookups.
        public static bool TryResolveFocusTarget(IndustrialObservationResult current, string key, ulong areaId, Func<Entity, bool> isLive, AreaMembershipIndex membership, out Entity target)
        {
            target = Entity.Null;
            if (current?.AllBottlenecks == null || isLive == null || membership == null) return false;
            if (!AreaDiagnosticTargetKey.TryResolve(key, Targets(current.AllBottlenecks), out var resolved) || !isLive(resolved)) return false;
            foreach (var relation in membership.GetAreas(resolved))
                if (relation.AreaStableId == areaId && relation.TypeId == (int)AreaTypeId.Industrial && relation.IsPrimaryForType) { target = resolved; return true; }
            return false;
        }
        private static IEnumerable<Entity> Targets(List<IndustrialBottleneck> bottlenecks)
        {
            foreach (var b in bottlenecks) if (b != null && b.Target != Entity.Null) yield return b.Target;
        }
        private sealed class EntityAssignmentStore : IIndustrialCompanyAssignmentStore
        {
            private readonly EntityManager entities;
            private readonly List<IndustrialAreaRef> areas = new List<IndustrialAreaRef>();
            public EntityAssignmentStore(IndustrialRuntimeSystem runtime, EntityManager entities) { this.entities = entities; runtime.CollectAreas(areas); }
            private Entity Area(ulong id)
            {
                foreach (var a in areas) if (a.StableId == id && entities.Exists(a.Area) && !entities.HasComponent<Game.Common.Deleted>(a.Area) &&
                    entities.HasComponent<ManagedAreaStableId>(a.Area) && entities.GetComponentData<ManagedAreaStableId>(a.Area).Value == id) return a.Area;
                return Entity.Null;
            }
            public bool CanWrite(ulong areaId) { var area = Area(areaId); return area != Entity.Null && entities.HasBuffer<IndustrialCompanyAssignment>(area); }
            public bool HasClaim(ulong areaId, Entity company, Entity building)
            {
                if (!CanWrite(areaId)) return false;
                var claims = entities.GetBuffer<IndustrialCompanyAssignment>(Area(areaId), true);
                for (int i = 0; i < claims.Length; i++) if (claims[i].Company == company && claims[i].Building == building) return true;
                return false;
            }
            public void Assign(ulong areaId, Entity company, Entity building)
            {
                foreach (var a in areas)
                {
                    if (!CanWrite(a.StableId)) continue;
                    var claims = entities.GetBuffer<IndustrialCompanyAssignment>(a.Area);
                    for (int i = claims.Length - 1; i >= 0; i--) if (claims[i].Company == company) claims.RemoveAt(i);
                }
                entities.GetBuffer<IndustrialCompanyAssignment>(Area(areaId)).Add(new IndustrialCompanyAssignment {
                    SchemaVersion = IndustrialCompanyAssignment.CurrentSchemaVersion, Company = company, Building = building });
            }
            public void Remove(ulong areaId, Entity company, Entity building)
            {
                var claims = entities.GetBuffer<IndustrialCompanyAssignment>(Area(areaId));
                for (int i = claims.Length - 1; i >= 0; i--)
                    if (claims[i].Company == company && claims[i].Building == building) claims.RemoveAt(i);
            }
        }
        public static IndustrialPanelModel Read(Entity area, in AreaPanelContext context)
        {
            var entities = context.Entities;
            var model = new IndustrialPanelModel();
            if (!entities.Exists(area)) return model;
            if (entities.HasComponent<ManagedArea>(area)) model.Lifecycle = entities.GetComponentData<ManagedArea>(area).State;
            if (entities.HasComponent<ManagedAreaStableId>(area)) model.StableId = entities.GetComponentData<ManagedAreaStableId>(area).Value;
            if (entities.HasComponent<ManagedAreaProgress>(area))
            {
                var progress = entities.GetComponentData<ManagedAreaProgress>(area);
                model.Level = IndustrialProgressionModel.NormalizeLevel(progress.Level); model.Experience = progress.Experience;
            }
            model.HasState = entities.HasComponent<IndustrialAreaState>(area);
            model.State = model.HasState ? entities.GetComponentData<IndustrialAreaState>(area) : IndustrialAreaState.CreateDefault(0);
            var runtime = context.World?.GetExistingSystemManaged<IndustrialRuntimeSystem>();
            if (runtime != null)
            {
                if (runtime.TryGetClock(out var clock)) model.NowHours = clock.GameHours(runtime.SimulationFrame);
                if (runtime.Store.TryGet(model.StableId, out var snapshot))
                {
                    model.Current = snapshot.Current; model.Paused = snapshot.Paused; model.Trends = snapshot.Trends;
                    model.HasXp = snapshot.HasXpResult; model.Xp = snapshot.LastXp; model.LastCompletedDay = snapshot.LastCompletedDay;
                }
                var services=context.World.GetExistingSystemManaged<AreaCoreServicesSystem>();
                var names=context.World.GetExistingSystemManaged<Game.UI.NameSystem>();
                var candidates = runtime.Store.GetOrCreate(model.StableId, area).OwnershipCandidates;
                long hour = runtime.TryGetClock(out var candidateClock) ? candidateClock.HourBucket(runtime.SimulationFrame) : long.MinValue;
                candidates.Begin(model.StableId, runtime.LoadGeneration, hour, runtime.Assignments.Revision);
                if (entities.HasBuffer<IndustrialCompanyAssignment>(area) && services != null)
                {
                    var claims = entities.GetBuffer<IndustrialCompanyAssignment>(area, true);
                    // Saved invalid/duplicate claims stay visible and safely removable, within the same 64-row bound.
                    for (int i = 0; i < claims.Length && i < IndustrialCompanyCandidateSet.Capacity; i++)
                    {
                        var claim = claims[i];
                        var eligibility = IndustrialCompanyOwnership.ReadLive(claim.Company, entities, services.Semantics, out var building);
                        var relations = runtime.Membership.GetAreas(building);
                        var owner = IndustrialCompanyOwnership.Resolve(claim.Company, building, relations, runtime.Assignments, eligibility);
                        bool valid = IndustrialCompanyOwnership.IsValidClaim(new IndustrialCompanyClaim(model.StableId, claim.Company, claim.Building), building, relations, eligibility) &&
                            owner.Manual && owner.AreaStableId == model.StableId;
                        candidates.Record(claim.Company, claim.Building, owner, entities.Exists(claim.Company) ? names?.GetRenderedLabelName(claim.Company) : null, true, !valid);
                    }
                }
                model.Companies = candidates.Rows;
                var areas=new List<IndustrialAreaRef>();runtime.CollectAreas(areas);
                var sources=new List<IndustrialComparisonSource>(areas.Count);
                foreach(var other in areas) {
                    if(!entities.Exists(other.Area)||entities.HasComponent<Game.Common.Deleted>(other.Area)||services==null||services.IsQuarantined(other.Area))continue;
                    runtime.Store.TryGet(other.StableId,out var otherSnapshot);
                    var managed=entities.GetComponentData<ManagedArea>(other.Area);
                    sources.Add(new IndustrialComparisonSource{StableId=other.StableId,TypeId=managed.TypeId,Lifecycle=managed.State,Snapshot=otherSnapshot,Name=names?.GetRenderedLabelName(other.Area)});
                }
                model.Comparison=IndustrialAreaComparison.Build(sources,runtime.TryGetClock(out var comparisonClock)?comparisonClock.GameHours(runtime.SimulationFrame):(double?)null);
            }
            var inputs = IndustrialProgressionSystem.BuildInputs(model.Current, model.State);
            model.Promotion = IndustrialProgressionModel.EvaluatePromotion(model.Level, model.Experience, inputs);
            return model;
        }
    }
}
