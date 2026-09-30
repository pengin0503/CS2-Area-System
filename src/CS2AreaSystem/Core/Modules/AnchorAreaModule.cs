using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Prefabs;
using Unity.Entities;
namespace CS2AreaSystem.Core.Modules
{
    public abstract class AnchorAreaModule : IAreaModule, IAreaAnchorPolicy, IAreaMemberClassifier, IAreaPanelProvider
    {
        protected AreaAnchorService Anchors { get; }
        private readonly AreaTypeId kind;
        public AreaAnchorRole? RequiredAnchorRole { get; }
        protected AnchorAreaModule(AreaTypeId kind, AreaAnchorService anchors, AreaAnchorRole? role)
        { this.kind = kind; Anchors = anchors; RequiredAnchorRole = role; }
        public AreaTypeDescriptor DescribeType()
        {
            var metadata = ManagedAreaPrefabFactory.Describe(kind);
            return new AreaTypeDescriptor(metadata.TypeId, metadata.PrefabName, metadata.DisplayNameKey, metadata.IconUri, this);
        }
        // Generic Core checks (valid building, role, membership) already ran; modules may be stricter.
        public virtual bool AcceptsAnchor(Entity area, Entity anchor, AreaAnchorRole role, EntityManager entities) => true;
        public bool ValidateActivation(Entity area) => !RequiredAnchorRole.HasValue || Anchors.HasValidAnchor(area, RequiredAnchorRole.Value);
        public IAreaMemberClassifier MemberClassifier => this;
        public IAreaPanelProvider PanelProvider => this;
        public AreaMemberRole Classify(Entity member, Entity area) => AreaMemberRole.Member;
        public bool AllowsOverlapWith(int otherTypeId) => true;
        public virtual string BuildSummary(Entity area, in AreaPanelContext context) => ValidateActivation(area) ? "CS2AS.Summary.Active" : "CS2AS.Summary.AnchorRequired";
        // Modules without structured content keep a single empty Overview tab; the section then shows the summary.
        public virtual AreaPanelDocument BuildPanel(Entity area, in AreaPanelContext context) => AreaPanelDocument.Overview();
        public virtual bool HandleAction(Entity area, string action, string value, in AreaPanelContext context) => false;
        public virtual void OnAreaCreated(Entity area) { }
        public virtual void OnAreaActivated(Entity area) { }
        public virtual void OnAreaSuspended(Entity area) { }
        public virtual void OnAreaDeleted(Entity area) { }
    }
}
