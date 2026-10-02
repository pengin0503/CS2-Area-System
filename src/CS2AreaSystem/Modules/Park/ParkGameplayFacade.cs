using System;
using System.Linq;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using Unity.Entities;
using Unity.Mathematics;
namespace CS2AreaSystem.Modules.Park
{
    // Read model for one selected Park, assembled only while the panel is built.
    public sealed class ParkPanelModel
    {
        public ulong StableId;
        public AreaLifecycleState Lifecycle;
        public int Level;
        public long Experience;
        public ParkAreaState State;
        public float Budget;
        public uint Frame;
        public ParkRuntimeSnapshot Runtime;
        public ParkPromotionResult Promotion;
        public bool HasState;
        public ParkHeadquartersState Headquarters;
        public ParkPolicyEffects Policies;
        public ParkWhatIfCandidates? Preview;
    }
    public enum ParkHeadquartersState : byte { Unknown, Missing, Valid, Invalid }
    // Boundary between the module UI and Park gameplay: selected-Park reads and the only validated setters.
    // Entrance add/remove stays in Core's guarded anchor actions. No policies, events or XP/level setters exist.
    // FocusDiagnosticTarget is read-only (camera focus on a landscape diagnostic target) and never reaches the setters.
    public static class ParkGameplayFacade
    {
        public const string SetBudget = "setBudget", SetFee = "setFee";
        public const string PreviewBudget = "previewBudget", PreviewFee = "previewFee";
        public const string FocusDiagnosticTarget = "focusDiagnosticTarget";
        public static bool TryApplyAction(string action, string value, int level, ref ManagedAreaSettings settings, ref ParkAreaState state)
        {
            switch (action)
            {
                case SetBudget:
                    if (!ParkFinanceModel.TryParseBudgetPercent(value, out var budget)) return false;
                    settings.BudgetMultiplier = budget;
                    return true;
                case SetFee:
                    if (!ParkFinanceModel.TryParseFee(level, value, out var fee)) return false;
                    state.AdmissionFee = fee;
                    return true;
                default: return false;
            }
        }
        public static bool HandleAction(Entity area, string action, string value, in AreaPanelContext context)
        {
            if (action == FocusDiagnosticTarget) return HandleFocus(area, value, context);
            if(action==PreviewBudget||action==PreviewFee) {
                var runtime=context.World?.GetExistingSystemManaged<ParkRuntimeSystem>();
                return runtime!=null&&runtime.Preview.TryApply(Read(area,context),action,value);
            }
            var entities = context.Entities;
            if (!entities.Exists(area) || !entities.HasComponent<ParkAreaState>(area) || !entities.HasComponent<ManagedAreaSettings>(area)) return false;
            var settings = entities.GetComponentData<ManagedAreaSettings>(area);
            var state = entities.GetComponentData<ParkAreaState>(area);
            int level = entities.HasComponent<ManagedAreaProgress>(area) ? entities.GetComponentData<ManagedAreaProgress>(area).Level : 1;
            if (!TryApplyAction(action, value, level, ref settings, ref state)) return false;
            entities.SetComponentData(area, settings);
            entities.SetComponentData(area, state);
            return true;
        }
        private static bool HandleFocus(Entity area, string key, in AreaPanelContext context)
        {
            if (context.Focus == null || context.World == null) return false;
            var entities = context.Entities;
            if (!entities.Exists(area) || !entities.HasComponent<ManagedAreaStableId>(area)) return false;
            var runtime = context.World.GetExistingSystemManaged<ParkRuntimeSystem>();
            if (runtime == null || !runtime.Store.TryGet(entities.GetComponentData<ManagedAreaStableId>(area).Value, out var snapshot)) return false;
            var services=context.World.GetExistingSystemManaged<AreaCoreServicesSystem>();
            ulong id=entities.GetComponentData<ManagedAreaStableId>(area).Value;
            var connectivity=snapshot.Connectivity;
            long entranceRevision=runtime.Entrances.TryGet(id,out var evidence)?evidence.Revision:-1;
            if(services!=null && connectivity!=null && connectivity.MembershipRevision==runtime.Membership.GetAreaRevision(id) &&
                connectivity.EntranceRevision==entranceRevision && connectivity.SemanticRevision==services.Semantics.Revision &&
                ParkConnectivityFocus.TryFocus(connectivity,key,e=>services.IsValidEntity(e)&&runtime.Membership.GetMembers(id).Contains(e)&&
                    ParkFacilityClassifier.IsVisitRelevant(ParkFacilityReader.ReadCore(e,entities,false),services.Semantics),context.Focus))return true;
            return TryFocus(snapshot.Landscape, key, e => StandalonePosition(entities, e), new ParkPolygon(ParkGeometry.Triangles(entities, area)), context.Focus);
        }
        // Landscape targets are standalone static objects (never Core building members): still alive, still standalone.
        private static (bool Valid, float X, float Z) StandalonePosition(EntityManager entities, Entity e)
        {
            if (!entities.Exists(e) || !entities.HasComponent<Game.Objects.Transform>(e) || !entities.HasComponent<Game.Objects.Static>(e) ||
                !entities.HasComponent<Game.Objects.Object>(e) || entities.HasComponent<Game.Common.Owner>(e) || entities.HasComponent<Game.Tools.Temp>(e) ||
                entities.HasComponent<Game.Common.Deleted>(e) || entities.HasComponent<Game.Buildings.Building>(e)) return (false, 0, 0);
            var position = entities.GetComponentData<Game.Objects.Transform>(e).m_Position;
            return (true, position.x, position.z);
        }
        // Resolves the key only against the current scan's targets, then requires the object to still stand inside the Park.
        public static bool TryFocus(ParkLandscapeSnapshot landscape, string key, Func<Entity, (bool Valid, float X, float Z)> standalonePosition, ParkPolygon polygon, Action<Entity> focus)
        {
            if (landscape == null || !landscape.Available || landscape.Diagnostics == null || focus == null || standalonePosition == null || polygon == null) return false;
            var targets = new System.Collections.Generic.List<Entity>();
            foreach (var diagnostic in landscape.Diagnostics) if (diagnostic != null && diagnostic.Target != Entity.Null) targets.Add(diagnostic.Target);
            if (!AreaDiagnosticTargetKey.TryResolve(key, targets, out var target)) return false;
            var position = standalonePosition(target);
            if (!position.Valid || !polygon.Contains(new float2(position.X, position.Z))) return false;
            focus(target);
            return true;
        }
        public static ParkPanelModel Read(Entity area, in AreaPanelContext context)
        {
            var entities = context.Entities;
            var model = new ParkPanelModel { Level = 1, Budget = ParkFinanceModel.DefaultBudget };
            if (!entities.Exists(area)) return model;
            if (entities.HasComponent<ManagedArea>(area)) model.Lifecycle = entities.GetComponentData<ManagedArea>(area).State;
            if (entities.HasComponent<ManagedAreaStableId>(area)) model.StableId = entities.GetComponentData<ManagedAreaStableId>(area).Value;
            if (entities.HasComponent<ManagedAreaProgress>(area))
            { var progress = entities.GetComponentData<ManagedAreaProgress>(area); model.Level = ParkProgressionModel.NormalizeLevel(progress.Level); model.Experience = progress.Experience; }
            if (entities.HasComponent<ManagedAreaSettings>(area)) model.Budget = ParkFinanceModel.ClampBudget(entities.GetComponentData<ManagedAreaSettings>(area).BudgetMultiplier);
            model.HasState = entities.HasComponent<ParkAreaState>(area);
            model.State = model.HasState ? entities.GetComponentData<ParkAreaState>(area) : ParkAreaState.CreateDefault();
            var runtime = context.World?.GetExistingSystemManaged<ParkRuntimeSystem>();
            var core=context.World?.GetExistingSystemManaged<AreaCoreServicesSystem>();
            if(core!=null)model.Headquarters=core.Anchors.HasValidAnchor(area,Core.Anchors.AreaAnchorRole.Headquarters)?ParkHeadquartersState.Valid:
                core.Anchors.HasLinks(area,Core.Anchors.AreaAnchorRole.Headquarters)?ParkHeadquartersState.Invalid:ParkHeadquartersState.Missing;
            if (runtime != null)
            {
                model.Frame = runtime.SimulationFrame;
                if (runtime.Store.TryGet(model.StableId, out var snapshot)) model.Runtime = snapshot;
                model.Preview=runtime.Preview.Read(model);
            }
            var policies=context.World?.GetExistingSystemManaged<Core.Policies.AreaPolicySystem>();
            if(policies!=null)model.Policies=new ParkPolicyEffectReader((a,id)=>policies.IsEffective(a,id),core?.Failures,"Park.Preview.PolicyEffects","Park preview policy inputs unavailable").Read(area);
            model.Promotion = ParkProgressionModel.EvaluatePromotion(model.Level, model.Experience, ParkProgressionSystem.BuildInputs(model.Runtime?.Quality, model.State));
            return model;
        }
    }
}
