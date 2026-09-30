using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using Unity.Entities;
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
    }
    // Boundary between the module UI and Park gameplay: selected-Park reads and the only validated setters.
    // Entrance add/remove stays in Core's guarded anchor actions. No policies, events or XP/level setters exist.
    public static class ParkGameplayFacade
    {
        public const string SetBudget = "setBudget", SetFee = "setFee";
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
            if (runtime != null)
            {
                model.Frame = runtime.SimulationFrame;
                if (runtime.Store.TryGet(model.StableId, out var snapshot)) model.Runtime = snapshot;
            }
            model.Promotion = ParkProgressionModel.EvaluatePromotion(model.Level, model.Experience, ParkProgressionSystem.BuildInputs(model.Runtime?.Quality, model.State));
            return model;
        }
    }
}
