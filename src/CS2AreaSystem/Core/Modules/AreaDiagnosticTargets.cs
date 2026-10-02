using System.Collections.Generic;
using System.Globalization;
using Unity.Entities;
namespace CS2AreaSystem.Core.Modules
{
    // Opaque key of a diagnostic target entity sent to the UI (Wave A2). The key is never authorization on its own:
    // it resolves only to an entity that exactly equals one of the module's current candidates.
    public static class AreaDiagnosticTargetKey
    {
        public static string Format(Entity target) =>
            target.Index.ToString(CultureInfo.InvariantCulture) + "." + target.Version.ToString(CultureInfo.InvariantCulture);
        public static bool TryResolve(string key, IEnumerable<Entity> allowedTargets, out Entity target)
        {
            target = Entity.Null;
            if (!TryParse(key, out var parsed) || parsed == Entity.Null || allowedTargets == null) return false;
            foreach (var candidate in allowedTargets)
                if (candidate == parsed) { target = candidate; return true; }
            return false;
        }
        private static bool TryParse(string key, out Entity entity)
        {
            entity = Entity.Null;
            if (string.IsNullOrEmpty(key)) return false;
            int dot = key.IndexOf('.');
            if (dot <= 0 || dot != key.LastIndexOf('.') || dot == key.Length - 1) return false;
            if (!int.TryParse(key.Substring(0, dot), NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                !int.TryParse(key.Substring(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int version)) return false;
            entity = new Entity { Index = index, Version = version };
            return true;
        }
    }
}
