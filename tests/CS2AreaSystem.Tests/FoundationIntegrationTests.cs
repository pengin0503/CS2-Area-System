using CS2AreaSystem.Modules.Park;
using CS2AreaSystem.Core.Areas;
using NUnit.Framework;
using Unity.Entities;
namespace CS2AreaSystem.Tests;
public class FoundationIntegrationTests
{
 [TestCase(false,false,false,false,"BothRequired")][TestCase(true,false,true,false,"EntranceRequired")][TestCase(false,true,false,true,"HeadquartersRequired")][TestCase(true,true,true,true,"Active")]
 [TestCase(false,true,true,false,"HeadquartersInvalid")][TestCase(true,false,true,true,"EntranceInvalid")][TestCase(false,false,true,true,"BothRequired")]
 public void MissingRequirementsHaveDistinctSummaries(bool hq,bool entrance,bool hqLinked,bool entranceLinked,string suffix) {
  Assert.That(ParkActivationSummary.Key(hq,hqLinked,entrance,entranceLinked),Is.EqualTo("CS2AS.Park.Summary."+suffix));
 }
 [Test] public void PanelShowsInvalidHeadquartersWithoutLosingProgressOrFinance() {
  var m=ParkPanelBuilderTests.Model(level:4,quality:false);m.Lifecycle=AreaLifecycleState.Suspended;m.Headquarters=ParkHeadquartersState.Invalid;
  m.Runtime!.EntranceCount=1;
  var document=ParkPanelBuilder.Build(m);var items=document.Tabs[0].Items;
  Assert.That(items.Any(i=>i.LabelKey=="CS2AS.Park.Warning.HeadquartersInvalid"),Is.True);
  Assert.That(items.Any(i=>i.LabelKey=="CS2AS.Park.Warning.NoEntrance"),Is.False,"a valid gate must not be blamed when only the HQ is invalid");
  Assert.That(m.Level,Is.EqualTo(4));Assert.That(m.State.LifetimeManagementExpense,Is.GreaterThanOrEqualTo(0));
 }
 // Phase 3 C2: before Quality runs, the missing gate comes from the live Entrance anchors, never from a path scan.
 [TestCase(ParkHeadquartersState.Missing,true)][TestCase(ParkHeadquartersState.Invalid,true)][TestCase(ParkHeadquartersState.Valid,false)]
 public void PanelReportsNoEntranceFromTheGateAnchorsBeforeQualityRuns(ParkHeadquartersState entrance,bool warned) {
  var m=ParkPanelBuilderTests.Model(quality:false);m.Lifecycle=AreaLifecycleState.Suspended;m.Headquarters=ParkHeadquartersState.Valid;
  m.Runtime!.EntranceCount=0;m.Entrance=entrance;
  var items=ParkPanelBuilder.Build(m).Tabs[0].Items;
  Assert.That(items.Any(i=>i.LabelKey=="CS2AS.Park.Warning.NoEntrance"),Is.EqualTo(warned));
 }
}
