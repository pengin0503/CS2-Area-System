using System.Collections.Generic;
namespace CS2AreaSystem.Core.Prefabs
{
    public sealed class AreaTypeReadiness
    {
        private readonly Dictionary<int, bool> values = new Dictionary<int, bool>();
        public void Set(int typeId, bool ready) => values[typeId] = ready;
        public bool IsReady(int typeId) => values.TryGetValue(typeId, out var ready) && ready;
        public bool AllReady(IEnumerable<int> typeIds)
        {
            bool any = false;
            foreach (var typeId in typeIds)
            {
                any = true;
                if (!IsReady(typeId)) return false;
            }
            return any;
        }
        public void Clear() => values.Clear();
    }

    public static class AreaPrefabRegistrationPolicy
    {
        public static bool ShouldAttempt(bool hasPrefab, bool registeredWithPrefabSystem) =>
            !hasPrefab || !registeredWithPrefabSystem;
    }
}
