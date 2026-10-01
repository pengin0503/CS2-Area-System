using System;
namespace CS2AreaSystem.Modules.Park
{
    public struct ParkLandscapeCategoryInput
    {
        public int Count, DistinctPrefabs;
        public int[] PrefabCounts;
    }
    public struct ParkLandscapeInputs
    {
        public float AreaSquareMeters;
        public ParkLandscapeCategoryInput Vegetation, Amenities, Decoration, Lighting;
        public float PathLength;
        // Objects without reliable semantics: count toward density/clutter only.
        public int UnknownCount;
    }
    public struct ParkLandscapeResult
    {
        public float Score, Vegetation, Amenities, Decoration, Lighting, Paths, Diversity, ClutterPenalty;
        public bool Cluttered;
        public int CategoriesPresent, ObjectCount;
    }
    // Density + diversity + diminishing returns (design §7.3–7.5). All tuning constants live here.
    public static class ParkLandscapeModel
    {
        public const float VegetationWeight = 30f, AmenitiesWeight = 20f, DecorationWeight = 15f, LightingWeight = 10f, PathWeight = 15f, DiversityWeight = 10f;
        public const float MaxClutterPenalty = 15f;
        // Targets per size unit (a 1 ha park is one unit; units grow with area^0.75).
        public const float VegetationTarget = 40f, AmenitiesTarget = 8f, DecorationTarget = 6f, LightingTarget = 8f, PathTarget = 250f;
        // Copies of one prefab that count fully before further copies only add their square root.
        public const int FullValueCopiesPerPrefab = 5;
        // Objects per 100 m² above which a modest clutter penalty applies.
        public const float ClutterDensity = 5f, ClutterPenaltyPerDensity = 3f;
        public const float MinSizeScale = 0.2f, MaxSizeScale = 10f;
        public static float SizeScale(float areaSquareMeters)
        {
            double scale = Math.Pow(Math.Max(0f, areaSquareMeters) / 10_000.0, 0.75);
            return (float)Math.Min(MaxSizeScale, Math.Max(MinSizeScale, scale));
        }
        public static float EffectiveCount(in ParkLandscapeCategoryInput input)
        {
            int count = Math.Max(0, input.Count);
            if (count == 0) return 0f;
            // Aggregate counts cannot describe the distribution. Give them conservative single-prefab credit;
            // live scans always supply the exact frequencies, so unrelated variations never reset a prefab's cap.
            if (input.PrefabCounts == null) return CopiesValue(count);
            float effective = 0;
            foreach (int copies in input.PrefabCounts) effective += CopiesValue(Math.Max(0, copies));
            return effective;
        }
        private static float CopiesValue(int count) => Math.Min(count, FullValueCopiesPerPrefab) +
            (float)Math.Sqrt(Math.Max(0, count - FullValueCopiesPerPrefab));
        private static float Fill(float effective, float target) => target <= 0 ? 0 : 1f - (float)Math.Exp(-1.6 * effective / target);
        // The scanned landscape, as the objects in the Park stand.
        public static ParkLandscapeResult ComputeLandscape(in ParkLandscapeInputs inputs) => ComputeLandscape(inputs, 1f);
        // scenicCredit multiplies the effective count of the scenic categories (vegetation, decoration) before the same
        // diminishing-returns curve and category caps (Landscape Protection). 1, or any invalid value, is the scan itself.
        public static ParkLandscapeResult ComputeLandscape(in ParkLandscapeInputs inputs, float scenicCredit)
        {
            float credit = float.IsNaN(scenicCredit) || float.IsInfinity(scenicCredit) || scenicCredit <= 0 ? 1f : scenicCredit;
            float unit = SizeScale(inputs.AreaSquareMeters);
            var result = new ParkLandscapeResult
            {
                Vegetation = VegetationWeight * Fill(EffectiveCount(inputs.Vegetation) * credit, VegetationTarget * unit),
                Amenities = AmenitiesWeight * Fill(EffectiveCount(inputs.Amenities), AmenitiesTarget * unit),
                Decoration = DecorationWeight * Fill(EffectiveCount(inputs.Decoration) * credit, DecorationTarget * unit),
                Lighting = LightingWeight * Fill(EffectiveCount(inputs.Lighting), LightingTarget * unit),
                Paths = PathWeight * Fill(Math.Max(0f, inputs.PathLength), PathTarget * unit)
            };
            int present = (inputs.Vegetation.Count > 0 ? 1 : 0) + (inputs.Amenities.Count > 0 ? 1 : 0) + (inputs.Decoration.Count > 0 ? 1 : 0) +
                          (inputs.Lighting.Count > 0 ? 1 : 0) + (inputs.PathLength > 1f ? 1 : 0);
            result.CategoriesPresent = present;
            result.Diversity = DiversityWeight * Math.Max(0, present - 1) / 4f;
            result.ObjectCount = Math.Max(0, inputs.Vegetation.Count) + Math.Max(0, inputs.Amenities.Count) + Math.Max(0, inputs.Decoration.Count) +
                                 Math.Max(0, inputs.Lighting.Count) + Math.Max(0, inputs.UnknownCount);
            float density = inputs.AreaSquareMeters > 0 ? result.ObjectCount / (inputs.AreaSquareMeters / 100f) : 0f;
            if (density > ClutterDensity)
            {
                result.Cluttered = true;
                result.ClutterPenalty = Math.Min(MaxClutterPenalty, (density - ClutterDensity) * ClutterPenaltyPerDensity);
            }
            float total = result.Vegetation + result.Amenities + result.Decoration + result.Lighting + result.Paths + result.Diversity - result.ClutterPenalty;
            result.Score = Math.Min(100f, Math.Max(0f, total));
            return result;
        }
    }
}
