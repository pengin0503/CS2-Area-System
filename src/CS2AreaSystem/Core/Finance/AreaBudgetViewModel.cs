using System;
using Colossal.UI.Binding;
namespace CS2AreaSystem.Core.Finance
{
    // What the vanilla Budget page shows for Areas (design §15): the Area amounts actually applied to city money over the
    // trailing in-game day. Area Management = policy upkeep + Park management + Industrial support + Park events (Service Upkeep group); Area Service Fees =
    // Park admission (Service Fees group). Queued or in-flight requests are never shown.
    public readonly struct AreaBudgetViewModel : IEquatable<AreaBudgetViewModel>
    {
        // Budget sub-item ids; the vanilla detail labels them EconomyPanel.BUDGET_SUB_ITEM[<id>] (UI/src/AreaBudgetDetails.tsx).
        public const string AreaManagementSource = "CS2AS.AreaManagement", AreaServiceFeesSource = "CS2AS.AreaServiceFees";
        public readonly long AreaManagement, AreaServiceFees;
        public AreaBudgetViewModel(long areaManagement, long areaServiceFees)
        {
            AreaManagement = Math.Max(0L, areaManagement);
            AreaServiceFees = Math.Max(0L, areaServiceFees);
        }
        public static AreaBudgetViewModel From(AreaFinanceRuntime runtime, uint frame) =>
            runtime == null ? default : new AreaBudgetViewModel(runtime.AreaManagement(frame), runtime.AreaServiceFees(frame));
        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin("CS2AreaSystem.Core.Finance.AreaBudgetViewModel");
            writer.PropertyName("areaManagement"); writer.Write(AreaManagement);
            writer.PropertyName("areaServiceFees"); writer.Write(AreaServiceFees);
            writer.TypeEnd();
        }
        public bool Equals(AreaBudgetViewModel other) => AreaManagement == other.AreaManagement && AreaServiceFees == other.AreaServiceFees;
        public override bool Equals(object obj) => obj is AreaBudgetViewModel other && Equals(other);
        public override int GetHashCode() => AreaManagement.GetHashCode() * 397 ^ AreaServiceFees.GetHashCode();
        public override string ToString() => $"AreaManagement={AreaManagement}, AreaServiceFees={AreaServiceFees}";
    }
}
