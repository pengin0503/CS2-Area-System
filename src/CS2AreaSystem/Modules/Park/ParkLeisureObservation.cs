using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    public struct ParkLeisureProviderState
    {
        public Entity Entity;
        public bool HasPrefab, HasLeisureData, Inactive, ServiceUnavailable, OutOfStock;
    }
    // Public-component equivalent of vanilla LeisureJob's provider resolution and SpendLeisure availability gate.
    // This observes a usable leisure opportunity, not the private vanilla event queue.
    public static class ParkLeisureObservation
    {
        public static bool TryResolveProvider(Entity target, Entity building, Entity targetProperty,
            in ParkLeisureProviderState tenant, in ParkLeisureProviderState current, out Entity provider)
        {
            provider = Entity.Null;
            if (target == Entity.Null || building == Entity.Null) return false;
            var selected = targetProperty == building && tenant.HasPrefab ? tenant : current;
            if (selected.Entity == Entity.Null || !selected.HasLeisureData || selected.Inactive || selected.ServiceUnavailable || selected.OutOfStock) return false;
            provider = selected.Entity;
            return true;
        }
    }
}
