using System.Collections.Generic;
using CS2AreaSystem.Core.Finance;
namespace CS2AreaSystem.Modules.Park
{
    // Park's view of the shared Area finance results: the Park management amounts that actually reached city money.
    public static class ParkAreaFinance
    {
        public static IEnumerable<AreaFinanceEntry> AppliedManagement(AreaFinanceBatch settled)
        {
            if (settled == null) yield break;
            foreach (var entry in settled.Expenses)
                if (entry.Kind == AreaFinanceKind.ParkManagement && entry.Amount > 0) yield return entry;
        }
    }
}
