using System;
using Colossal.UI.Binding;
using Game;
using Game.UI;
namespace CS2AreaSystem.Core.Finance
{
    // Publishes cs2AreaSystem.areaBudget for the Budget page rows (docs/verification/area-budget-ui-contracts.md).
    // It only reads AreaFinanceSystem's settled accounts.
    public partial class AreaBudgetBindingSystem : UISystemBase
    {
        private AreaFinanceSystem finance;
        private RawValueBinding binding;
        private AreaBudgetViewModel published;
        private bool failureLogged;
        public override GameMode gameMode => GameMode.Game;
        protected override void OnCreate()
        {
            base.OnCreate();
            finance = World.GetOrCreateSystemManaged<AreaFinanceSystem>();
            AddBinding(binding = new RawValueBinding("cs2AreaSystem", "areaBudget", writer => published.Write(writer)));
        }
        protected override void OnUpdate()
        {
            try
            {
                var current = finance.Ready ? finance.Budget : default;
                if (!current.Equals(published)) { published = current; binding.Update(); }
                failureLogged = false;
            }
            catch (Exception error)
            {
                if (!failureLogged) { failureLogged = true; Mod.Log.Error(error, "Area budget rows could not be published; the vanilla budget is unaffected."); }
            }
        }
    }
}
