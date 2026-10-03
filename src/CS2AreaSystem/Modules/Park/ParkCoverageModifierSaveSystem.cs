using Game;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Modules.Park
{
    // Runs before the native serializer: ModifiedServiceCoverage is ISerializable, so every value still written by the
    // coverage modifier is restored to its native baseline first (also when the writer is disabled or failed). The
    // writer reapplies only after the next fresh native pass.
    public partial class ParkCoverageModifierSaveSystem : GameSystemBase
    {
        private ParkCoverageModifierSystem writer;
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            writer = World.GetOrCreateSystemManaged<ParkCoverageModifierSystem>();
        }
        [Preserve]
        protected override void OnUpdate() => writer.RestoreTracked();
    }
}
