using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Modules;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Industrial
{
    // Read-only boundary between the Industrial panel and gameplay state. It has no setters and handles no actions:
    // HQ assignment, Edit and Delete stay in Core's guarded generic actions.
    public static class IndustrialGameplayFacade
    {
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
            }
            var inputs = IndustrialProgressionSystem.BuildInputs(model.Current, model.State);
            model.Promotion = IndustrialProgressionModel.EvaluatePromotion(model.Level, model.Experience, inputs);
            return model;
        }
    }
}
