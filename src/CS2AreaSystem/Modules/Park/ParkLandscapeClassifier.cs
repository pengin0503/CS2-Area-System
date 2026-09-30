using System.Collections.Generic;
using Game.Prefabs;
namespace CS2AreaSystem.Modules.Park
{
    public enum ParkLandscapeCategory : byte { None, Vegetation, Amenity, Decoration, Lighting, Unknown }
    // Typed facts about one static object inside a Park (read from vanilla components/prefab data).
    public struct ParkObjectTraits
    {
        public bool IsStaticObject, IsBuilding, HasOwner, IsTempOrDeleted, IsNetObject, IsTransportStop, IsUtility;
        public bool IsTree, IsPlant, IsStreetLight, IsPlaceable;
        // Activities offered by the prefab's activity locations (SpawnLocationData.m_ActivityMask).
        public uint ActivityMask;
    }
    public static class ParkLandscapeClassifier
    {
        // Rest/leisure activities that mark benches, tables, fitness and similar amenities.
        public static readonly uint AmenityActivities = Bits(ActivityType.BenchSitting, ActivityType.GroundSitting, ActivityType.GroundLaying,
            ActivityType.Selfies, ActivityType.PushUps, ActivityType.PullUps, ActivityType.SitUps, ActivityType.JumpingJacks, ActivityType.JumpingLunges,
            ActivityType.Squats, ActivityType.Yoga, ActivityType.Reading, ActivityType.Fishing);
        private static uint Bits(params ActivityType[] types) { uint mask = 0; foreach (var type in types) mask |= new ActivityMask(type).m_Mask; return mask; }
        // Only player-placed standalone static objects are landscape. Buildings are Core members; owned sub-objects
        // belong to their building/net (already reflected by that facility); road furniture and stops are infrastructure.
        public static ParkLandscapeCategory Classify(in ParkObjectTraits t)
        {
            if (!t.IsStaticObject || t.IsBuilding || t.HasOwner || t.IsTempOrDeleted || t.IsNetObject || t.IsTransportStop || t.IsUtility)
                return ParkLandscapeCategory.None;
            if (t.IsTree || t.IsPlant) return ParkLandscapeCategory.Vegetation;
            if ((t.ActivityMask & AmenityActivities) != 0) return ParkLandscapeCategory.Amenity;
            if (t.IsStreetLight) return ParkLandscapeCategory.Lighting;
            // Remaining placeable props (statues, fountains, signs...) have no finer vanilla tag: Decoration, whose
            // value is still bounded by per-prefab diminishing returns. Anything else only adds density.
            if (t.IsPlaceable) return ParkLandscapeCategory.Decoration;
            return ParkLandscapeCategory.Unknown;
        }
    }
    public sealed class ParkLandscapeTally
    {
        private readonly Dictionary<ParkLandscapeCategory, int> counts = new Dictionary<ParkLandscapeCategory, int>();
        private readonly Dictionary<ParkLandscapeCategory, HashSet<int>> prefabs = new Dictionary<ParkLandscapeCategory, HashSet<int>>();
        public void Add(ParkLandscapeCategory category, int prefabKey)
        {
            if (category == ParkLandscapeCategory.None) return;
            counts[category] = Count(category) + 1;
            if (!prefabs.TryGetValue(category, out var set)) prefabs[category] = set = new HashSet<int>();
            set.Add(prefabKey);
        }
        public int Count(ParkLandscapeCategory category) => counts.TryGetValue(category, out var n) ? n : 0;
        private ParkLandscapeCategoryInput Input(ParkLandscapeCategory category) =>
            new ParkLandscapeCategoryInput { Count = Count(category), DistinctPrefabs = prefabs.TryGetValue(category, out var set) ? set.Count : 0 };
        public ParkLandscapeInputs ToInputs(float areaSquareMeters, float pathLength) => new ParkLandscapeInputs
        {
            AreaSquareMeters = areaSquareMeters, PathLength = pathLength,
            Vegetation = Input(ParkLandscapeCategory.Vegetation), Amenities = Input(ParkLandscapeCategory.Amenity),
            Decoration = Input(ParkLandscapeCategory.Decoration), Lighting = Input(ParkLandscapeCategory.Lighting),
            UnknownCount = Count(ParkLandscapeCategory.Unknown)
        };
    }
    // Chooses which Parks may run an expensive spatial scan this update: dirty Parks first, then Parks whose last
    // scan is older than the fallback refresh period, never more than the work budget.
    public sealed class ParkLandscapeScheduler
    {
        private readonly uint refreshFrames;
        private readonly Dictionary<ulong, uint?> lastScan = new Dictionary<ulong, uint?>();
        private readonly List<ulong> dirty = new List<ulong>();
        public ParkLandscapeScheduler(uint refreshFrames) { this.refreshFrames = refreshFrames; }
        public int TrackedCount => lastScan.Count;
        public bool IsTracked(ulong id) => lastScan.ContainsKey(id);
        public void Track(ulong id) { if (!lastScan.ContainsKey(id)) { lastScan[id] = null; MarkDirty(id); } }
        public void Forget(ulong id) { lastScan.Remove(id); dirty.Remove(id); }
        public void MarkDirty(ulong id) { if (lastScan.ContainsKey(id) && !dirty.Contains(id)) dirty.Add(id); }
        public void MarkAllDirty() { foreach (var id in lastScan.Keys) if (!dirty.Contains(id)) dirty.Add(id); }
        public void Clear() { lastScan.Clear(); dirty.Clear(); }
        public List<ulong> Take(uint frame, int budget)
        {
            var result = new List<ulong>();
            while (result.Count < budget && dirty.Count > 0) { result.Add(dirty[0]); dirty.RemoveAt(0); }
            if (result.Count < budget)
            {
                var stale = new List<KeyValuePair<ulong, uint>>();
                foreach (var pair in lastScan)
                    if (pair.Value.HasValue && !result.Contains(pair.Key) && frame >= pair.Value.Value && frame - pair.Value.Value > refreshFrames)
                        stale.Add(new KeyValuePair<ulong, uint>(pair.Key, pair.Value.Value));
                stale.Sort((a, b) => a.Value.CompareTo(b.Value));
                for (int i = 0; i < stale.Count && result.Count < budget; i++) result.Add(stale[i].Key);
            }
            foreach (var id in result) lastScan[id] = frame;
            return result;
        }
    }
}
namespace CS2AreaSystem.Modules.Park
{
    // Cursor over one Park's scan candidates so classification can be spread across frames.
    public sealed class ParkLandscapeScan<T>
    {
        private readonly List<T> items;
        private int cursor;
        public ParkLandscapeScan(List<T> items) { this.items = items ?? new List<T>(); }
        public bool Done => cursor >= items.Count;
        public int Remaining => items.Count - cursor;
        public void Process(int budget, System.Action<T> visit)
        {
            int end = System.Math.Min(items.Count, cursor + System.Math.Max(1, budget));
            for (; cursor < end; cursor++) visit(items[cursor]);
        }
    }
}
