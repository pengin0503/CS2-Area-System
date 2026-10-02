using CS2AreaSystem.Core.Areas;
namespace CS2AreaSystem.Modules.Park
{
    public readonly struct ParkWhatIfResult
    {
        public readonly float ManagementCost,Coverage,Maintenance,ValueForMoney;
        public readonly double EstimatedNet;
        public readonly int EffectiveFee;
        public readonly bool ManagementAvailable,MaintenanceAvailable,ValueAvailable,NetAvailable;
        public ParkWhatIfResult(float cost,float coverage,float maintenance,float value,double net,int fee,bool management,bool maintenanceAvailable,bool valueAvailable,bool netAvailable)
        {ManagementCost=cost;Coverage=coverage;Maintenance=maintenance;ValueForMoney=value;EstimatedNet=net;EffectiveFee=fee;ManagementAvailable=management;MaintenanceAvailable=maintenanceAvailable;ValueAvailable=valueAvailable;NetAvailable=netAvailable;}
    }
    // Estimate holds visitor volume and current Quality constant; it does not predict demand or household solvency.
    public static class ParkWhatIfPreview
    {
        public static ParkWhatIfResult Evaluate(ParkPanelModel current,float candidateBudget,int candidateFee)
        {
            var runtime=current.Runtime;
            bool active=current.Lifecycle==AreaLifecycleState.Active;
            bool quality=active&&runtime?.Quality!=null&&unchecked(current.Frame-runtime.QualityFrame)<ParkFinanceModel.FramesPerDay/8;
            bool management=quality&&runtime.ManagementNeedPerDay>0;
            float coverage=ParkFinanceModel.ComputeManagementCoverage(candidateBudget,current.Policies.ManagementFundingMultiplier);
            float cost=management?ParkFinanceModel.ComputeManagementCostPerDay(runtime.ManagementNeedPerDay,candidateBudget,current.Policies.ManagementFundingMultiplier):0;
            int fee=ParkFinanceModel.EffectiveFee(current.Level,candidateFee);
            bool maintenance=quality&&runtime.Facilities!=null;
            float score=maintenance?ParkQualityModel.ComputeMaintenance(runtime.Facilities.VanillaCondition,coverage):0;
            float value=quality?ParkFinanceModel.ComputeValueForMoney(fee,runtime.Quality.Quality,current.Policies.FairFeeMultiplier):0;
            double net=management?runtime.RecentVisitors.Sum(current.Frame)*(double)fee-cost:0;
            return new ParkWhatIfResult(cost,coverage,score,value,net,fee,management,maintenance,quality,management);
        }
    }
    public readonly struct ParkWhatIfCandidates
    {
        public readonly float Budget;public readonly int Fee;
        public ParkWhatIfCandidates(float budget,int fee){Budget=budget;Fee=fee;}
    }
    // At most one selected StableId is retained; never authoritative or serialized.
    public sealed class ParkWhatIfState
    {
        private ulong id;private ParkWhatIfCandidates candidates;
        public ParkWhatIfCandidates Read(ParkPanelModel current)=>id==current.StableId&&id!=0?candidates:new ParkWhatIfCandidates(current.Budget,ParkFinanceModel.EffectiveFee(current.Level,current.State.AdmissionFee));
        public bool TryApply(ParkPanelModel current,string action,string value)
        {
            var next=Read(current);
            if(action==ParkGameplayFacade.PreviewBudget&&ParkFinanceModel.TryParseBudgetPercent(value,out var budget))next=new ParkWhatIfCandidates(budget,next.Fee);
            else if(action==ParkGameplayFacade.PreviewFee&&ParkFinanceModel.TryParseFee(current.Level,value,out var fee))next=new ParkWhatIfCandidates(next.Budget,fee);
            else return false;
            id=current.StableId;candidates=next;return id!=0;
        }
        public void Clear(){id=0;candidates=default;}
    }
}
