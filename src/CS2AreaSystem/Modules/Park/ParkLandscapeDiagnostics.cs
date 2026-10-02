using System;
using System.Collections.Generic;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    // One advisory landscape finding (Wave A5). Target is a representative standalone object, or Entity.Null when no
    // position is reliable (a missing category has no object to point at).
    public sealed class ParkLandscapeDiagnostic
    {
        public string Key { get; }
        public double Severity { get; }
        public Entity Target { get; }
        public ParkLandscapeDiagnostic(string key, double severity, Entity target) { Key = key; Severity = severity; Target = target; }
    }
    // Bounded spatial evidence gathered in the existing sliced scan: a fixed 4x4 grid over the Park's bounding box. Each
    // cell keeps only an object count, a category bit mask and its first object; nothing is kept per object.
    public sealed class ParkLandscapeDiagnosticCollector
    {
        public const int Side = 4;
        public static int Cells => Side * Side;
        private readonly float minX, minZ, width, depth;
        private readonly int[] counts = new int[Side * Side];
        private readonly int[] masks = new int[Side * Side];
        private readonly Entity[] firsts = new Entity[Side * Side];
        public int Objects { get; private set; }
        public ParkLandscapeDiagnosticCollector(float minX, float minZ, float maxX, float maxZ)
        {
            this.minX = minX; this.minZ = minZ;
            width = Math.Max(0f, maxX - minX); depth = Math.Max(0f, maxZ - minZ);
        }
        public void Add(ParkLandscapeCategory category, float x, float z, Entity entity)
        {
            if (category == ParkLandscapeCategory.None) return;
            int cell = Axis(z - minZ, depth) * Side + Axis(x - minX, width);
            if (counts[cell] == 0) firsts[cell] = entity;
            counts[cell]++;
            masks[cell] |= Bit(category);
            Objects++;
        }
        // Points outside the box (or NaN) clamp to the edge cells; a zero-size axis has one cell.
        private static int Axis(float offset, float extent)
        {
            if (!(extent > 0f) || !(offset > 0f)) return 0;
            return Math.Min(Side - 1, (int)(offset / extent * Side));
        }
        internal static int Bit(ParkLandscapeCategory category) => 1 << (int)category;
        public int EvidencedCells { get { int n = 0; foreach (int c in counts) if (c > 0) n++; return n; } }
        internal int Count(int cell) => counts[cell];
        internal bool Has(int cell, ParkLandscapeCategory category) => (masks[cell] & Bit(category)) != 0;
        internal Entity First(int cell) => firsts[cell];
    }
    // Pure evaluation of the scanned landscape breakdown plus the bounded grid. Advisory only: it never feeds Quality.
    public static class ParkLandscapeDiagnostics
    {
        public const int MaxDiagnostics = 4;
        // A category is short when its awarded score is below this share of its weight.
        public const float ShortageShare = 0.5f;
        // Spatial coverage needs this much evidence before an Amenity/Lighting gap is reported.
        public const int MinEvidencedCells = 8;
        public const double SparseCoverage = 0.25;
        public const string Cluttered = "Cluttered", SparseAmenities = "SparseAmenities", SparseLighting = "SparseLighting";
        public static readonly string[] Keys = { "LowVegetation", "LowAmenities", "LowDecoration", "LowLighting", "LowPaths", Cluttered, SparseAmenities, SparseLighting };
        public static IEnumerable<ParkLandscapeDiagnostic> Evaluate(ParkLandscapeResult result, ParkLandscapeDiagnosticCollector grid)
        {
            var found = new List<ParkLandscapeDiagnostic>();
            bool lowAmenities = Shortage(found, "LowAmenities", result.Amenities, ParkLandscapeModel.AmenitiesWeight);
            bool lowLighting = Shortage(found, "LowLighting", result.Lighting, ParkLandscapeModel.LightingWeight);
            Shortage(found, "LowVegetation", result.Vegetation, ParkLandscapeModel.VegetationWeight);
            Shortage(found, "LowDecoration", result.Decoration, ParkLandscapeModel.DecorationWeight);
            Shortage(found, "LowPaths", result.Paths, ParkLandscapeModel.PathWeight);
            if (result.Cluttered)
                found.Add(new ParkLandscapeDiagnostic(Cluttered, Severity(result.ClutterPenalty / ParkLandscapeModel.MaxClutterPenalty), grid == null ? Entity.Null : Densest(grid, null)));
            if (grid != null)
            {
                if (!lowAmenities) Sparse(found, SparseAmenities, grid, ParkLandscapeCategory.Amenity);
                if (!lowLighting) Sparse(found, SparseLighting, grid, ParkLandscapeCategory.Lighting);
            }
            found.Sort((a, b) => { int c = b.Severity.CompareTo(a.Severity); return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key); });
            if (found.Count > MaxDiagnostics) found.RemoveRange(MaxDiagnostics, found.Count - MaxDiagnostics);
            return found;
        }
        // Rounded so float noise in the breakdown never reorders equal findings.
        private static double Severity(double value) => Math.Round(Math.Min(1, Math.Max(0, double.IsNaN(value) ? 0 : value)), 4);
        private static bool Shortage(List<ParkLandscapeDiagnostic> into, string key, float score, float weight)
        {
            if (float.IsNaN(score) || score >= weight * ShortageShare) return false;
            into.Add(new ParkLandscapeDiagnostic(key, Severity((weight - Math.Max(0f, score)) / (double)weight), Entity.Null));
            return true;
        }
        private static void Sparse(List<ParkLandscapeDiagnostic> into, string key, ParkLandscapeDiagnosticCollector grid, ParkLandscapeCategory category)
        {
            int evidenced = grid.EvidencedCells;
            if (evidenced < MinEvidencedCells) return;
            int covered = 0;
            for (int cell = 0; cell < ParkLandscapeDiagnosticCollector.Cells; cell++) if (grid.Has(cell, category)) covered++;
            double coverage = (double)covered / evidenced;
            if (coverage >= SparseCoverage) return;
            into.Add(new ParkLandscapeDiagnostic(key, Severity(1 - coverage / SparseCoverage), Densest(grid, category)));
        }
        // First object of the cell with the most objects (lowest cell on a tie), optionally among cells lacking a category.
        private static Entity Densest(ParkLandscapeDiagnosticCollector grid, ParkLandscapeCategory? lacking)
        {
            int best = -1;
            for (int cell = 0; cell < ParkLandscapeDiagnosticCollector.Cells; cell++)
            {
                if (grid.Count(cell) == 0 || (lacking.HasValue && grid.Has(cell, lacking.Value))) continue;
                if (best < 0 || grid.Count(cell) > grid.Count(best)) best = cell;
            }
            return best < 0 ? Entity.Null : grid.First(best);
        }
    }
    // One Park's in-progress sliced scan: the category tally and the diagnostic grid are fed by the same classification
    // and discarded together. A scan whose boundary revision is no longer current produces nothing.
    public sealed class ParkLandscapeScanState
    {
        public ulong ParkId { get; }
        public ulong Revision { get; }
        public ParkLandscapeTally Tally { get; } = new ParkLandscapeTally();
        public ParkLandscapeDiagnosticCollector Diagnostics { get; }
        public ParkLandscapeScanState(ulong parkId, ulong revision, float minX, float minZ, float maxX, float maxZ)
        {
            ParkId = parkId; Revision = revision;
            Diagnostics = new ParkLandscapeDiagnosticCollector(minX, minZ, maxX, maxZ);
        }
        public void Add(ParkLandscapeCategory category, int prefabKey, float x, float z, Entity entity)
        {
            if (category == ParkLandscapeCategory.None) return;
            Tally.Add(category, prefabKey);
            Diagnostics.Add(category, x, z, entity);
        }
        public bool TryComplete(ParkLandscapeScheduler scheduler, float areaSquareMeters, float pathLength, uint frame, out ParkLandscapeSnapshot landscape)
        {
            landscape = null;
            if (scheduler == null || !scheduler.IsCurrent(ParkId, Revision)) return false;
            landscape = new ParkLandscapeSnapshot { Inputs = Tally.ToInputs(areaSquareMeters, pathLength), ScannedFrame = frame, Available = true };
            landscape.Result = ParkLandscapeModel.ComputeLandscape(landscape.Inputs);
            landscape.Diagnostics = new List<ParkLandscapeDiagnostic>(ParkLandscapeDiagnostics.Evaluate(landscape.Result, Diagnostics));
            return true;
        }
    }
}
