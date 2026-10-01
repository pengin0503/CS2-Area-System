using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace CS2AreaSystem.Modules.Park
{
    public readonly struct ParkLandscapeTile
    {
        public readonly int X, Z;
        // Tree query bounds: the tile, slightly widened so float rounding at a tile edge never hides an owned object.
        public readonly Bounds2 Query;
        public ParkLandscapeTile(int x, int z, Bounds2 query) { X = x; Z = z; Query = query; }
    }
    // Splits a scan's search bounds into fixed-size tiles (#57). Game.Objects.SearchSystem's static tree (NativeQuadTree in
    // 1.6.2f1) can only be iterated to completion, with no cursor, so a scan queries one tile at a time. An object belongs
    // to the tile holding the clamped minimum corner of its bounds, so one that spans tiles is collected exactly once.
    public sealed class ParkLandscapeTiling
    {
        public const float DefaultTileSize = 128f;
        private const float Margin = 1f;
        private readonly Bounds2 search;
        private readonly float size;
        private readonly int columns, rows;
        private int next;
        public int TileCount => columns * rows;
        public bool Done => next >= TileCount;
        public ParkLandscapeTiling(Bounds2 search, float tileSize)
        {
            this.search = search;
            size = tileSize > 0 ? tileSize : DefaultTileSize;
            columns = Count(search.max.x - search.min.x);
            rows = Count(search.max.y - search.min.y);
        }
        private int Count(float extent) => Math.Max(1, (int)Math.Ceiling(Math.Max(0f, extent) / size));
        public ParkLandscapeTile Next()
        {
            if (Done) throw new InvalidOperationException("No tile left");
            int x = next % columns, z = next / columns;
            next++;
            var min = search.min + new float2(x, z) * size;
            var max = math.min(min + size, search.max);
            return new ParkLandscapeTile(x, z, new Bounds2(min - Margin, max + Margin));
        }
        // True when the object overlaps the search bounds and its anchor lies in this tile.
        public bool Owns(in ParkLandscapeTile tile, Bounds2 item)
        {
            if (!math.all(item.min <= search.max) || !math.all(item.max >= search.min)) return false;
            var anchor = math.clamp(item.min, search.min, search.max) - search.min;
            int x = Math.Min(columns - 1, Math.Max(0, (int)Math.Floor(anchor.x / size)));
            int z = Math.Min(rows - 1, Math.Max(0, (int)Math.Floor(anchor.y / size)));
            return x == tile.X && z == tile.Z;
        }
    }
    // Drives one scan within a per-frame budget shared by enumeration and classification (#57): pending candidates are
    // classified first; when none are left, tiles are enumerated (each costing its visited count, at least 1) until the
    // budget or maxTilesPerFrame is used. A frame can exceed the budget by at most one tile's objects.
    public sealed class ParkLandscapeCollector<T>
    {
        private readonly ParkLandscapeTiling tiling;
        private readonly int budget, maxTilesPerFrame;
        private readonly List<T> pending = new List<T>();
        private int cursor;
        public long Enumerated { get; private set; }
        public long Classified { get; private set; }
        public int TilesEnumerated { get; private set; }
        public int LastFrameWork { get; private set; }
        public int MaxFrameWork { get; private set; }
        public bool Done => tiling.Done && cursor >= pending.Count;
        public ParkLandscapeCollector(ParkLandscapeTiling tiling, int budget, int maxTilesPerFrame)
        {
            this.tiling = tiling ?? throw new ArgumentNullException(nameof(tiling));
            this.budget = Math.Max(1, budget);
            this.maxTilesPerFrame = Math.Max(1, maxTilesPerFrame);
        }
        // enumerate(tile, into) appends the owned objects of the tile and returns how many objects it visited.
        public void Step(Func<ParkLandscapeTile, List<T>, int> enumerate, Action<T> classify)
        {
            int work = 0, tiles = 0;
            while (work < budget)
            {
                if (cursor < pending.Count)
                {
                    int end = Math.Min(pending.Count, cursor + (budget - work));
                    for (; cursor < end; cursor++) { classify(pending[cursor]); Classified++; work++; }
                    continue;
                }
                if (tiling.Done || tiles >= maxTilesPerFrame) break;
                pending.Clear(); cursor = 0;
                int visited = enumerate(tiling.Next(), pending);
                tiles++; TilesEnumerated++; Enumerated += visited;
                work += Math.Max(1, visited);
            }
            if (cursor >= pending.Count) { pending.Clear(); cursor = 0; }
            LastFrameWork = work;
            MaxFrameWork = Math.Max(MaxFrameWork, work);
        }
    }
}
