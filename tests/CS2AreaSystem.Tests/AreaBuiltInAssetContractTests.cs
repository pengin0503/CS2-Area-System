using System.IO;
using System.Linq;
using CS2AreaSystem.Core.Semantics;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Phase 3 C2 Task 1: the three dedicated Area System assets are the only Headquarters/Entrance sources.
public class AreaBuiltInAssetContractTests
{
 private static string Doc() => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../docs/content/area-system-built-in-assets-contract.md"));
 [Test] public void TheContractNamesExactlyThreeRequiredAsPrefixedPrefabs() {
  var ids = AreaBuiltInAssetContract.Required.Select(a => a.PrefabName).ToArray();
  Assert.That(ids, Is.EqualTo(new[] { "AS_ParkHeadquarters", "AS_IndustrialHeadquarters", "AS_ParkGate" }));
  Assert.That(ids, Is.Unique);
  Assert.That(ids.All(id => id.StartsWith(AreaBuiltInAssetContract.OwnedPrefix, System.StringComparison.Ordinal)), Is.True);
  Assert.That(AreaBuiltInAssetContract.OwnedPrefix, Is.EqualTo("AS_"));
  Assert.That(AreaBuiltInAssetContract.PackageId, Is.EqualTo("AS_AreaSystemAssets"));
  Assert.That(AreaBuiltInAssetContract.ProviderId, Is.EqualTo("CS2AreaSystem.BuiltInContent"));
  Assert.That(AreaBuiltInAssetContract.PrefabType, Is.EqualTo("BuildingPrefab"));
 }
 [TestCase("AS_ParkHeadquarters", AreaAssetSemantic.ParkHeadquarters)]
 [TestCase("AS_IndustrialHeadquarters", AreaAssetSemantic.IndustrialHeadquarters)]
 [TestCase("AS_ParkGate", AreaAssetSemantic.ParkEntrance)]
 public void EachPrefabMapsToExactlyItsOwnSemantic(string id, AreaAssetSemantic semantic) {
  Assert.That(AreaBuiltInAssetContract.TryGetSemantic(id, out var mapped), Is.True);
  Assert.That(mapped, Is.EqualTo(semantic));
  Assert.That(AreaBuiltInAssetContract.Required.Count(a => a.Semantic == semantic), Is.EqualTo(1), "one asset per role");
 }
 // No localized/display-name, case-insensitive, substring, prefix or DLC-lookalike identity is accepted.
 [TestCase("as_ParkGate")] [TestCase("AS_PARKGATE")] [TestCase("AS_ParkGate ")] [TestCase(" AS_ParkGate")]
 [TestCase("AS_ParkGate01")] [TestCase("AS_ParkGate_Upgrade")] [TestCase("ParkGate")] [TestCase("ParkGate01_2x1")]
 [TestCase("Park Gate")] [TestCase("公園ゲート")] [TestCase("ParkAreaHQ01_4x3")] [TestCase("IndustrialAreaHQ01_5x4")]
 [TestCase("ParkHeadquarters")] [TestCase("IndustrialHeadquarters")] [TestCase("DLC_ParkGate")] [TestCase("")] [TestCase(null)]
 public void OnlyTheExactOrdinalIdentifierResolves(string candidate) =>
  Assert.That(AreaBuiltInAssetContract.TryGetSemantic(candidate, out var mapped) || mapped != AreaAssetSemantic.None, Is.False);
 // C2 retires the Foundation office / Park Maintenance / derived-path sources; the contract carries no fallback.
 [Test] public void TheContractCarriesNoFoundationFallback() {
  Assert.That(AreaBuiltInAssetContract.Required.Select(a => a.Semantic).Aggregate((a, b) => a | b),
   Is.EqualTo(AreaAssetSemantic.ParkHeadquarters | AreaAssetSemantic.IndustrialHeadquarters | AreaAssetSemantic.ParkEntrance));
  var source = IndustrialSupportSourceGuardTests.Source("Core/Semantics/AreaBuiltInAssetContract.cs");
  Assert.That(source, Does.Not.Match(@"OfficeProperty|ParkMaintenance|PathwayData|Contains\(|StartsWith\(\s*name|IgnoreCase|ToLower|ToUpper|Localiz"));
 }
 [Test] public void TheOwnershipRuleRequiresANonBuiltInPackagedAsPrefixedSource() {
  Assert.That(AreaBuiltInAssetContract.IsOwnedSource(true, false, "AS_ParkHeadquarters.cok"), Is.True);
  Assert.That(AreaBuiltInAssetContract.IsOwnedSource(true, false, "AS_AreaSystemAssets.cok"), Is.True);
  Assert.That(AreaBuiltInAssetContract.IsOwnedSource(false, false, "AS_ParkHeadquarters.cok"), Is.False, "unpackaged local prefab");
  Assert.That(AreaBuiltInAssetContract.IsOwnedSource(true, true, "AS_ParkHeadquarters.cok"), Is.False, "base game/DLC database");
  Assert.That(AreaBuiltInAssetContract.IsOwnedSource(true, false, "ParkSet.cok"), Is.False);
  Assert.That(AreaBuiltInAssetContract.IsOwnedSource(true, false, "as_ParkSet.cok"), Is.False);
  Assert.That(AreaBuiltInAssetContract.IsOwnedSource(true, false, null), Is.False);
 }
 [Test] public void TheContractDocumentPinsIdentifiersArchivesAndRetiredFallbacks() {
  var doc = Doc();
  foreach (var required in new[] { "`AS_AreaSystemAssets`", "`CS2AreaSystem.BuiltInContent`", "`AS_ParkHeadquarters`", "`AS_IndustrialHeadquarters`", "`AS_ParkGate`",
   "`CS2_AS_IndustrialHeadquarters.zip`", "`CS2_AS_ParkSet_HQ_Gate.zip`", "`CS2_AS_ParkHeadquarters.zip`", "superseded", "no fallback" })
   Assert.That(doc, Does.Contain(required), required);
 }
}
