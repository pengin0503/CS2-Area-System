using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Modules.Park;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class ParkWhatIfTests {
 private static ParkPanelModel Current()=>new(){StableId=7,Level=2,Lifecycle=AreaLifecycleState.Active,Budget=1,State=ParkAreaState.CreateDefault(),Runtime=new(){ManagementNeedPerDay=1000,Quality=new(){Result=new(){Quality=70}},Facilities=new()}};
 [Test] public void PreviewClampsCandidatesUsesPoliciesAndNeverMutatesCurrentState() {
  var model=Current();model.Policies=new(true,false,true);model.Runtime.RecentVisitors.Add(0,10);
  var result=ParkWhatIfPreview.Evaluate(model,9,999);
  Assert.That(result.ManagementCost,Is.EqualTo(1875));Assert.That(result.Coverage,Is.EqualTo(1.875));
  Assert.That(result.EstimatedNet,Is.EqualTo(600-1875));Assert.That(result.ValueForMoney,Is.EqualTo(ParkFinanceModel.ComputeValueForMoney(60,70,1.3)));
  Assert.That(model.Budget,Is.EqualTo(1));Assert.That(model.State.AdmissionFee,Is.Zero);Assert.That(model.Runtime.ManagementNeedPerDay,Is.EqualTo(1000));
 }
 [Test] public void LockedFeesAndUnavailableOrSuspendedDataAreExplicit() {
  var model=Current();model.Level=1;Assert.That(ParkWhatIfPreview.Evaluate(model,1,60).EffectiveFee,Is.Zero);
  model.Runtime=null;var missing=ParkWhatIfPreview.Evaluate(model,1,10);Assert.That(missing.ManagementAvailable,Is.False);Assert.That(missing.NetAvailable,Is.False);
  model=Current();model.Lifecycle=AreaLifecycleState.Suspended;Assert.That(ParkWhatIfPreview.Evaluate(model,1,10).NetAvailable,Is.False);
 }
 [Test] public void PreviewStateIsBoundedAndCannotLeakAcrossSelectedStableIdsOrReachSetters() {
  var state=new ParkWhatIfState();var model=Current();
  Assert.That(state.TryApply(model,"previewBudget","125"),Is.True);Assert.That(state.Read(model).Budget,Is.EqualTo(1.25));
  model.StableId=8;Assert.That(state.Read(model).Budget,Is.EqualTo(1));
  var settings=ParkStateDefaults.Settings();var park=model.State;
  Assert.That(ParkGameplayFacade.TryApplyAction("previewFee","20",2,ref settings,ref park),Is.False);Assert.That(park.AdmissionFee,Is.Zero);
  Assert.That(state.TryApply(model,"previewFee","NaN"),Is.False);
 }
 [Test] public void PanelUsesDistinctPreviewActionsAndEveryEstimateLabelIsMarked() {
  var items=ParkPanelBuilder.Build(Current()).Tabs.Single(t=>t.Id=="finances").Items;
  Assert.That(items.Single(i=>i.Id=="preview.budget").Action,Is.EqualTo("previewBudget"));
  Assert.That(items.Single(i=>i.Id=="preview.fee").Action,Is.EqualTo("previewFee"));
  Assert.That(items.Where(i=>i.Id.StartsWith("preview.")).All(i=>i.LabelKey.Contains("Preview")),Is.True);
 }
}
