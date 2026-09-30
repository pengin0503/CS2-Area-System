using System;
namespace CS2AreaSystem.Core.Areas
{
    public sealed class ManagedAreaTypeResolver
    {
        public bool TryResolve(string prefabName, out AreaTypeId typeId)
        {
            typeId = default;
            if (StringComparer.Ordinal.Equals(prefabName, "CS2AS.ParkArea")) typeId = AreaTypeId.Park;
            else if (StringComparer.Ordinal.Equals(prefabName, "CS2AS.IndustrialArea")) typeId = AreaTypeId.Industrial;
            else if (StringComparer.Ordinal.Equals(prefabName, "CS2AS.TourismArea")) typeId = AreaTypeId.Tourism;
            else return false;
            return true;
        }
    }
    public readonly struct ManagedAreaIdentity
    {
        public ManagedArea Managed { get; }
        public ManagedAreaStableId StableId { get; }
        private ManagedAreaIdentity(ManagedArea managed, ManagedAreaStableId stable) { Managed = managed; StableId = stable; }
        public static ManagedAreaIdentity Ensure(int resolvedTypeId, ManagedArea? current, ManagedAreaStableId? stable, AreaStableIdAllocator ids) =>
            new ManagedAreaIdentity(current ?? new ManagedArea { SchemaVersion = ManagedArea.CurrentSchemaVersion, TypeId = resolvedTypeId, State = AreaLifecycleState.Created },
                stable ?? new ManagedAreaStableId { Value = ids.Allocate() });
    }
    public static class AreaToolActivationPolicy
    {
        public static bool CanCreate(bool prefabReady, bool nativeTool) => prefabReady && nativeTool;
    }
}
