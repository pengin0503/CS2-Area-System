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
  var m=ParkPanelBuilderTests.Model(level:4,quality:false);m.Lifecycle=AreaLifecycleState.Suspended;m.Headquarters=ParkHeadquartersState.Invalid;
  m.Runtime!.EntranceCount=1;
  m.Runtime.EntranceEvidence=new ParkEntranceEvidence(ParkEntranceEvidenceStatus.Complete,4,new[]{new ParkEntrancePoint(10,0,Entity.Null)});
  var document=ParkPanelBuilder.Build(m);var items=document.Tabs[0].Items;
  Assert.That(items.Any(i=>i.LabelKey=="CS2AS.Park.Warning.HeadquartersInvalid"),Is.True);
  Assert.That(items.Any(i=>i.LabelKey=="CS2AS.Park.Warning.NoEntrance"),Is.False,"valid entrance evidence must not be blamed when only the HQ is invalid");
  Assert.That(m.Level,Is.EqualTo(4));Assert.That(m.State.LifetimeManagementExpense,Is.GreaterThanOrEqualTo(0));
 }
 [Test] public void PanelShowsNoEntranceOnlyAfterCompleteEvidenceFindsNone() {
  var m=ParkPanelBuilderTests.Model(quality:false);m.Lifecycle=AreaLifecycleState.Suspended;m.Headquarters=ParkHeadquartersState.Valid;
  m.Runtime!.EntranceCount=0;m.Runtime.EntranceEvidence=new ParkEntranceEvidence(ParkEntranceEvidenceStatus.Complete,5);
  var items=ParkPanelBuilder.Build(m).Tabs[0].Items;
  Assert.That(items.Any(i=>i.LabelKey=="CS2AS.Park.Warning.NoEntrance"),Is.True);
 }
}
