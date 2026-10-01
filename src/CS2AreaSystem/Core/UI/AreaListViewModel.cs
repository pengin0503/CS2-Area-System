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
        // Set for a quarantined Area only (AreaUIKey): its stable ID is zero or shared and cannot address it.
        public string Key;
        public string Name, TypeKey, State;
        public string UIKey => Key ?? StableId.ToString(CultureInfo.InvariantCulture);
    }
    public readonly struct AreaUICandidate
    {
        public Entity Area { get; }
        public ulong StableId { get; }
        public bool Quarantined { get; }
        public AreaUICandidate(Entity area, ulong stableId, bool quarantined) { Area = area; StableId = stableId; Quarantined = quarantined; }
    }
    // The key the UI sends back for an Area: its stable ID, or for a quarantined Area its entity ("q{index}v{version}"),
    // valid for the current session only and accepted solely by selection, Edit and Delete.
    public static class AreaUIKey
    {
        public static string Format(Entity area, ulong stableId, bool quarantined) =>
            quarantined ? "q" + area.Index.ToString(CultureInfo.InvariantCulture) + "v" + area.Version.ToString(CultureInfo.InvariantCulture)
                        : stableId.ToString(CultureInfo.InvariantCulture);
    }
    // Mod-owned selection route for completed managed Space areas, which the vanilla default tool does not
    // raycast (DefaultToolSystem only adds AreaTypeMask.Lots). Entries are sorted by stable ID.
    public static class AreaListViewModel
    {
        public static List<AreaListEntry> Sort(IEnumerable<AreaListEntry> entries)
        {
            var list = new List<AreaListEntry>(entries);
            list.Sort((a, b) => a.StableId != b.StableId ? a.StableId.CompareTo(b.StableId) : string.CompareOrdinal(a.Key, b.Key));
            return list;
        }
        public static bool Equivalent(IReadOnlyList<AreaListEntry> a, IReadOnlyList<AreaListEntry> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].StableId != b[i].StableId || a[i].Key != b[i].Key || a[i].Name != b[i].Name || a[i].TypeKey != b[i].TypeKey || a[i].State != b[i].State) return false;
            return true;
        }
        public static void Write(IJsonWriter writer, IReadOnlyList<AreaListEntry> entries)
        {
            writer.ArrayBegin((uint)entries.Count);
            foreach (var entry in entries)
            {
                writer.TypeBegin("CS2AreaSystem.AreaListEntry");
                writer.PropertyName("stableId"); writer.Write(entry.UIKey);
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
        // Healthy Areas resolve by unique stable ID exactly as above; quarantined ones only by their entity key.
        public static bool TryFind(IEnumerable<AreaUICandidate> areas, string expectedKey, out Entity area)
        {
            area = Entity.Null;
            var healthy = new List<KeyValuePair<Entity, ulong>>();
            foreach (var candidate in areas)
            {
                if (!candidate.Quarantined) { healthy.Add(new KeyValuePair<Entity, ulong>(candidate.Area, candidate.StableId)); continue; }
                if (area == Entity.Null && expectedKey == AreaUIKey.Format(candidate.Area, candidate.StableId, true)) area = candidate.Area;
            }
            if (area != Entity.Null) return true;
            return TryFind(healthy, expectedKey, out area);
        }
    }
}
