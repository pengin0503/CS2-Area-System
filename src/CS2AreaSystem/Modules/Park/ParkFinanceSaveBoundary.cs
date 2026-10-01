using Game;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Park
{
    // Saves must serialize the Park counters after the corresponding Resources/PlayerMoney transfer. This runs before
    // AreaFinanceSaveSystem, which then charges the queued management and lets Park record it, before the serializer.
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
