using Colossal.UI.Binding;
using Game.UI.InGame;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.UI
{
    // Managed-area content inside the vanilla selected-info panel (like the district panel), through the public
    // SelectedInfoUISystem.AddMiddleSection extension point. The UI module maps this type's full name to its
    // React component via selectedInfoSectionComponents.
    public partial class AreaInfoSection : InfoSectionBase
    {
        private AreaManagementUISystem management;
        private AreaManagementViewModel view, candidate;
        protected override string group => nameof(AreaInfoSection);
        private AreaManagementUISystem Management => management ??= World.GetExistingSystemManaged<AreaManagementUISystem>();
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            m_InfoUISystem.AddMiddleSection(this);
        }
        // Called by SelectedInfoUISystem every frame while something is selected: area state changes (anchor,
        // lifecycle, pending assignment, options) do not mark the entity Updated, so rebind when content changes.
        protected override void OnPreUpdate()
        {
            candidate = Management != null && Management.TryBuildView(selectedEntity, out var next) ? next : null;
            if (!AreaManagementViewModel.Equivalent(view, candidate)) m_InfoUISystem.SetDirty();
        }
        protected override void Reset() { view = null; }
        [Preserve]
        protected override void OnUpdate() { visible = candidate != null; }
        protected override void OnProcess() { view = candidate; }
        public override void OnWriteProperties(IJsonWriter writer) { if (view != null) view.WriteProperties(writer); }
    }
}
