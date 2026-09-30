using System;
using System.Collections.Generic;
using System.Globalization;
using Colossal.UI.Binding;
using Unity.Entities;
namespace CS2AreaSystem.Core.UI
{
    public sealed class AreaListEntry
    {
        public ulong StableId;
        public string Name, TypeKey, State;
    }
    // Mod-owned selection route for completed managed Space areas, which the vanilla default tool does not
    // raycast (DefaultToolSystem only adds AreaTypeMask.Lots). Entries are sorted by stable ID.
    public static class AreaListViewModel
    {
        public static List<AreaListEntry> Sort(IEnumerable<AreaListEntry> entries)
        {
            var list = new List<AreaListEntry>(entries);
            list.Sort((a, b) => a.StableId.CompareTo(b.StableId));
            return list;
        }
        public static bool Equivalent(IReadOnlyList<AreaListEntry> a, IReadOnlyList<AreaListEntry> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].StableId != b[i].StableId || a[i].Name != b[i].Name || a[i].TypeKey != b[i].TypeKey || a[i].State != b[i].State) return false;
            return true;
        }
        public static void Write(IJsonWriter writer, IReadOnlyList<AreaListEntry> entries)
        {
            writer.ArrayBegin((uint)entries.Count);
            foreach (var entry in entries)
            {
                writer.TypeBegin("CS2AreaSystem.AreaListEntry");
                writer.PropertyName("stableId"); writer.Write(entry.StableId.ToString(CultureInfo.InvariantCulture));
                writer.PropertyName("name"); writer.Write(entry.Name);
                writer.PropertyName("typeKey"); writer.Write(entry.TypeKey);
                writer.PropertyName("state"); writer.Write(entry.State);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }
        // Resolves a UI request by persisted stable ID; an unknown, malformed or ambiguous ID selects nothing.
        public static bool TryFind(IEnumerable<KeyValuePair<Entity, ulong>> areas, string expectedId, out Entity area)
        {
            area = Entity.Null;
            if (!ulong.TryParse(expectedId, NumberStyles.None, CultureInfo.InvariantCulture, out var expected) || expected == 0) return false;
            foreach (var candidate in areas)
            {
                if (candidate.Value != expected) continue;
                if (area != Entity.Null) { area = Entity.Null; return false; }
                area = candidate.Key;
            }
            return area != Entity.Null;
        }
    }
}
