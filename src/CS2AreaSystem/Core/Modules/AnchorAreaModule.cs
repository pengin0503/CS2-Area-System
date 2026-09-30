using System.Collections.Generic;
using CS2AreaSystem.Core.Anchors;
using CS2AreaSystem.Core.Areas;
using CS2AreaSystem.Core.Prefabs;
using Unity.Entities;
namespace CS2AreaSystem.Core.Modules
{
    public abstract class AnchorAreaModule : IAreaModule, IAreaAnchorPolicy, IAreaMemberClassifier, IAreaPanelProvider
    {
        private readonly AreaAnchorService anchors;
        private readonly AreaTypeId kind;
        public AreaAnchorRole? RequiredAnchorRole { get; }
        public IReadOnlyList<AreaPanelTab> Tabs { get; } = new[] { new AreaPanelTab("overview", "CS2AS.Tab.Overview") };
        protected AnchorAreaModule(AreaTypeId kind, AreaAnchorService anchors, AreaAnchorRole? role)
        { this.kind = kind; this.anchors = anchors; RequiredAnchorRole = role; }
        public AreaTypeDescriptor DescribeType()
        {
            var metadata = ManagedAreaPrefabFactory.Describe(kind);
            return new AreaTypeDescriptor(metadata.TypeId, metadata.PrefabName, metadata.DisplayNameKey, metadata.IconUri, this);
        }
        public bool ValidateActivation(Entity area) => !RequiredAnchorRole.HasValue || anchors.HasValidAnchor(area, RequiredAnchorRole.Value);
        public IAreaMemberClassifier MemberClassifier => this;
        public IAreaPanelProvider PanelProvider => this;
        public AreaMemberRole Classify(Entity member, Entity area) => AreaMemberRole.Member;
        public bool AllowsOverlapWith(int otherTypeId) => true;
        public string BuildSummary(Entity area) => ValidateActivation(area) ? "CS2AS.Summary.Active" : "CS2AS.Summary.AnchorRequired";
        public bool HandleAction(Entity area, string action) => false;
        public void OnAreaCreated(Entity area) { }
        public void OnAreaActivated(Entity area) { }
        public void OnAreaSuspended(Entity area) { }
        public void OnAreaDeleted(Entity area) { }
    }
}
