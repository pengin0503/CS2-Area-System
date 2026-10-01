using System;
using Game;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Park
{
    public static class ParkFinanceSaveBoundary
    {
        public static void Flush(bool ready, int generation, int currentGeneration, Action completeAndRecord,
            Func<bool> hasPending, Action completeDependencies, Action schedule)
        {
            if (!ready || generation != currentGeneration) return;
            completeAndRecord();
            if (!hasPending()) return;
            // Schedule is invoked outside FinanceSystem.Update, where its cached Dependency does not refresh.
            // Complete current economy dependencies at this save-only boundary before reacquiring the lookups.
            completeDependencies();
            schedule();
            completeAndRecord();
        }
    }
    // Saves must serialize the Park counters after the corresponding Resources/PlayerMoney transfer.
    // Synchronization is confined to the save boundary; normal simulation keeps the scheduled transfer path.
    public partial class ParkFinanceSaveSystem : GameSystemBase
    {
        private ParkFinanceSystem finance;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            finance = World.GetOrCreateSystemManaged<ParkFinanceSystem>();
        }
        [Preserve]
        protected override void OnUpdate() => finance.FlushForSave();
    }
}
