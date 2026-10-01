using System;
using Game.City;
using Game.Economy;
using Unity.Entities;
namespace CS2AreaSystem.Core.Finance
{
    // A household-to-city payment (Park admission): the city is credited first and the household is debited by exactly
    // the credited amount, so the two can never diverge. A zero credit means no debit.
    public static class AreaFinanceHouseholdTransfer
    {
        // Never more than the fee, never more than the household has, never more than the city can store.
        public static long Plan(long requested, long householdMoney, long storedCityMoney) =>
            requested <= 0 || householdMoney <= 0 ? 0 :
            Math.Min(Math.Min(requested, householdMoney), Math.Max(0, AreaFinanceVanillaAdapter.CityMoneyLimit - storedCityMoney));
        // Credits the city and returns the amount the household must be debited.
        public static long Transfer(ref PlayerMoney cityMoney, long householdMoney, long requested) =>
            AreaFinanceVanillaAdapter.ApplyIncome(ref cityMoney, Plan(requested, householdMoney, AreaFinanceVanillaAdapter.StoredCityMoney(cityMoney)));
        public static long Transfer(ref PlayerMoney cityMoney, DynamicBuffer<Resources> householdResources, long requested)
        {
            long applied = Transfer(ref cityMoney, EconomyUtils.GetResources(Resource.Money, householdResources), requested);
            // applied <= the household's int balance, so the cast is exact.
            if (applied > 0) EconomyUtils.AddResources(Resource.Money, (int)-applied, householdResources);
            return applied;
        }
    }
}
