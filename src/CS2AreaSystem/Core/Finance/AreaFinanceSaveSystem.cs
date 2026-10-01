using System;
using Game;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.Finance
{
    public static class AreaFinanceSaveBoundary
    {
        public static void Flush(bool ready, int generation, int currentGeneration, Action completeAndRecord,
            Func<bool> hasPending, Action completeDependencies, Action schedule)
        {
            if (!ready || generation != currentGeneration) return;
            completeAndRecord();
            if (!hasPending()) return;
            // Schedule is invoked outside the system's Update, where its cached Dependency does not refresh.
            // Complete current economy dependencies at this save-only boundary before reacquiring the lookups.
            completeDependencies();
            schedule();
            completeAndRecord();
        }
    }
    // A save must never serialize PlayerMoney while Area amounts are still queued. Synchronization is confined to
    // the save boundary; normal simulation keeps the scheduled path.
    public partial class AreaFinanceSaveSystem : GameSystemBase
    {
        private AreaFinanceSystem finance;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            finance = World.GetOrCreateSystemManaged<AreaFinanceSystem>();
        }
        [Preserve]
        protected override void OnUpdate() => finance.FlushForSave();
    }
}
