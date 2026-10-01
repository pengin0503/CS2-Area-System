using Colossal.UI.Binding;
using Game.UI.InGame;
using UnityEngine.Scripting;
namespace CS2AreaSystem.Core.UI
{
    // The selected managed Area's policies as their own selected-info middle section, mirroring the vanilla District
    // panel where policies are a separate section below the district section. The UI module maps this type's full name
    // to AreaPoliciesSection, which renders the vanilla Policy components.
    public partial class AreaPolicyInfoSection : InfoSectionBase
    {
        private AreaManagementUISystem management;
        private AreaPolicySectionView view, candidate;
        protected override string group => nameof(AreaPolicyInfoSection);
        private AreaManagementUISystem Management => management ??= World.GetExistingSystemManaged<AreaManagementUISystem>();
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            m_InfoUISystem.AddMiddleSection(this);
        }
        protected override void OnPreUpdate()
        {
            candidate = Management != null && Management.TryBuildPolicySection(selectedEntity, out var next) ? next : null;
            if (!AreaPolicySectionView.Equivalent(view, candidate)) m_InfoUISystem.SetDirty();
        }
        protected override void Reset() { view = null; }
        [Preserve]
        protected override void OnUpdate() { visible = candidate != null && candidate.Policies.Count > 0; }
        protected override void OnProcess() { view = candidate; }
        public override void OnWriteProperties(IJsonWriter writer) { if (view != null) view.WriteProperties(writer); }
    }
}
