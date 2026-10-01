using System;
using Game.City;
namespace CS2AreaSystem.Core.Finance
{
    public readonly struct AreaFinanceApplied
    {
        public readonly long Expense, Income;
        public AreaFinanceApplied(long expense, long income) { Expense = expense; Income = income; }
    }
    // Version-pinned to 1.6.2f1 (docs/verification/area-finance-contracts.md). Area amounts move the real city
    // PlayerMoney with its native clamp to ±kMaxMoney, exactly like BudgetApplySystem's one net Add per update.
    // There is no other lower bound: a negative balance never shortens an expense (design §15.1).
    // Callers pass the city's PlayerMoney by ref so this runs inside a scheduled job with a ComponentLookup.
    public static class AreaFinanceVanillaAdapter
    {
        public const long CityMoneyLimit = PlayerMoney.kMaxMoney;
        public static long StoredCityMoney(PlayerMoney money)
        {
            // The stored field is private. Reading a copy with the public unlimited flag cleared returns it through
            // the supported getter; the city's own component and setting are untouched.
            money.m_Unlimited = false;
            return money.money;
        }
        public static long ApplyExpense(ref PlayerMoney money, long amount) => Apply(ref money, amount, 0).Expense;
        public static long ApplyIncome(ref PlayerMoney money, long amount) => Apply(ref money, 0, amount).Income;
        // Applies one net change. Only when the native bound cuts the change short is one side reduced: at the upper
        // bound the income, at the lower bound the expense. Stored balance change == Income - Expense, always.
        public static AreaFinanceApplied Apply(ref PlayerMoney money, long expense, long income)
        {
            expense = Math.Max(0, expense); income = Math.Max(0, income);
            long stored = StoredCityMoney(money), net = income - expense;
            long target = Math.Min(CityMoneyLimit, Math.Max(-CityMoneyLimit, AreaFinanceModel.SaturatingAdd(stored, net)));
            long delta = target - stored;
            AddInIntSteps(ref money, delta);
            if (delta == net) return new AreaFinanceApplied(expense, income);
            return net > 0 ? new AreaFinanceApplied(expense, AreaFinanceModel.SaturatingAdd(delta, expense))
                           : new AreaFinanceApplied(income - delta, income);
        }
        // Expense reported to CityStatisticsSystem under the same key BudgetApplySystem uses for service upkeep.
        public static StatisticsEvent ExpenseStatistic(long applied) => new StatisticsEvent
        {
            m_Statistic = StatisticType.Expense,
            m_Parameter = (int)ExpenseSource.ServiceUpkeep,
            m_Change = Math.Max(0, applied),
        };
        // PlayerMoney.Add adds two ints before clamping, so one call with a large value can wrap. The target is already
        // inside the representable range; int-sized steps towards it never leave it.
        private static void AddInIntSteps(ref PlayerMoney money, long delta)
        {
            while (delta != 0)
            {
                int step = (int)Math.Max(-int.MaxValue, Math.Min(int.MaxValue, delta));
                money.Add(step);
                delta -= step;
            }
        }
    }
}
