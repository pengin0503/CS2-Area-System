using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Core.Areas;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class FoundationIntegrationTests
{
 [TestCase(false,false,"BothRequired")][TestCase(true,false,"EntranceRequired")][TestCase(false,true,"HeadquartersRequired")][TestCase(true,true,"Active")]
 public void MissingRequirementsHaveDistinctSummaries(bool hq,bool entrance,string suffix) {
  Assert.That(ParkActivationSummary.Key(hq,false,entrance,false),Is.EqualTo("CS2AS.Park.Summary."+suffix));
 }
 [Test] public void DerivedPositionsFeedSpreadAndIgnoreUnknownEvidence() {
  var e=new ParkEntranceEvidence(ParkEntranceEvidenceStatus.Complete,2,new[]{new ParkEntrancePoint(100,0,Entity.Null)});
  var points=ParkEntranceAccess.Positions(new[]{(0f,0f)},e);
  Assert.That(points.Count,Is.EqualTo(2));Assert.That(ParkQualityModel.ComputeEntranceSpread(points,10000),Is.GreaterThan(0));
  Assert.That(ParkEntranceAccess.Positions(Array.Empty<(float,float)>(),new ParkEntranceEvidence(ParkEntranceEvidenceStatus.Unknown,3)).Count,Is.Zero);
 }
 [Test] public void PanelShowsMissingMaintenanceHeadquartersWithoutLosingProgressOrFinance() {
  var m=ParkPanelBuilderTests.Model(level:4);m.Lifecycle=AreaLifecycleState.Suspended;m.Headquarters=ParkHeadquartersState.Invalid;
  var document=ParkPanelBuilder.Build(m);var items=document.Tabs[0].Items;
  Assert.That(items.Any(i=>i.LabelKey=="CS2AS.Park.Warning.HeadquartersInvalid"),Is.True);
  Assert.That(m.Level,Is.EqualTo(4));Assert.That(m.State.LifetimeManagementExpense,Is.GreaterThanOrEqualTo(0));
 }
}
