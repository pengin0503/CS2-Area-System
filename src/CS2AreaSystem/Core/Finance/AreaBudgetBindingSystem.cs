using System;
using Colossal.UI.Binding;
using Game;
using Game.UI;
using Unity.Entities;
namespace CS2AreaSystem.Core.Finance
{
    // Publishes cs2AreaSystem.areaBudget for the Budget page rows (docs/verification/area-budget-ui-contracts.md).
    // It only reads AreaFinanceSystem's settled accounts.
    public partial class AreaBudgetBindingSystem : UISystemBase
    {
        private AreaFinanceSystem finance;
        private RawValueBinding binding;
        private AreaBudgetViewModel published;
        private CS2AreaSystem.Core.Diagnostics.AreaFailureLog failures;
        public override GameMode gameMode => GameMode.Game;
        protected override void OnCreate()
        {
            base.OnCreate();
            finance = World.GetOrCreateSystemManaged<AreaFinanceSystem>();
            failures = World.GetOrCreateSystemManaged<CS2AreaSystem.Core.Areas.AreaCoreServicesSystem>().Failures;
            AddBinding(binding = new RawValueBinding("cs2AreaSystem", "areaBudget", writer => published.Write(writer)));
        }
        protected override void OnUpdate()
        {
            try
            {
                var current = finance.Ready ? finance.Budget : default;
                if (!current.Equals(published)) { published = current; binding.Update(); }
                failures.Recovered(Entity.Null, "UI.Budget");
            }
            catch (Exception error)
            {
                failures.Report(Entity.Null, "UI.Budget", error, "Area budget rows could not be published; the vanilla budget is unaffected.");
            }
        }
    }
}
