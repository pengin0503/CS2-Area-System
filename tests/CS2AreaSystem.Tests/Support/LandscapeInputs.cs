using CS2AreaSystem.Modules.Park;
namespace CS2AreaSystem.Tests.Support;
internal static class LandscapeInputs {
 // Synthetic model fixtures explicitly distribute their copies; production scans use actual prefab frequencies.
 internal static ParkLandscapeCategoryInput Balanced(int count, int kinds) {
  kinds = Math.Min(Math.Max(0, count), Math.Max(1, kinds));
  var frequencies = new int[kinds];
  for (int i = 0; i < kinds; i++) frequencies[i] = count / kinds + (i < count % kinds ? 1 : 0);
  return new ParkLandscapeCategoryInput { Count = count, DistinctPrefabs = kinds, PrefabCounts = frequencies };
 }
}
