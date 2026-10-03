using Game;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Industrial
{
    // Saves must serialize company money and the support counters after the settled credit. Runs after
    // AreaFinanceSaveSystem (which applied every queued request and reported its completion) and before the serializer;
    // synchronization is confined to this save boundary.
    public partial class IndustrialSupportSaveSystem : GameSystemBase
    {
        private IndustrialSupportSystem support;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            support = World.GetOrCreateSystemManaged<IndustrialSupportSystem>();
        }
        [Preserve]
        protected override void OnUpdate() => support.FlushForSave();
    }
}
