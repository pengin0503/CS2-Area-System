using System.Collections.Generic;
using Colossal.UI.Binding;
using CS2AreaSystem.Core.Modules;
namespace CS2AreaSystem.Core.UI
{
    // Data of the managed-area section inside the vanilla selected-info panel. The panel's own title section
    // already shows (and renames) the area name and its actions section provides "locate"; this view carries
    // only managed-area content.
    public sealed class AreaManagementViewModel
    {
        public string StableId, TypeKey, State, Experience, Summary, AnchorRole, AnchorName;
        public int? Level;
        public bool PendingAnchor, ConfirmDelete = true;
        public AreaPanelDocument Panel = AreaPanelDocument.Overview();
        // Every valid anchor of the module's role (several Entrances, at most one Headquarters).
        public IReadOnlyList<AreaAnchorView> Anchors = new AreaAnchorView[0];
        public IReadOnlyList<AreaAnchorGroupView> AnchorGroups = new AreaAnchorGroupView[0];
        // Comparing avoids rewriting the selected-info sections every frame.
        public static bool Equivalent(AreaManagementViewModel a, AreaManagementViewModel b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.StableId != b.StableId || a.TypeKey != b.TypeKey || a.State != b.State ||
                a.Experience != b.Experience || a.Summary != b.Summary || a.AnchorRole != b.AnchorRole || a.AnchorName != b.AnchorName ||
                a.Level != b.Level || a.PendingAnchor != b.PendingAnchor || a.ConfirmDelete != b.ConfirmDelete) return false;
            var left = a.Anchors ?? new AreaAnchorView[0];
            var right = b.Anchors ?? new AreaAnchorView[0];
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (left[i]?.Key != right[i]?.Key || left[i]?.Name != right[i]?.Name || left[i]?.Valid != right[i]?.Valid) return false;
            var ag = a.AnchorGroups ?? new AreaAnchorGroupView[0]; var bg = b.AnchorGroups ?? new AreaAnchorGroupView[0];
            if (ag.Count != bg.Count) return false;
            for (int i = 0; i < ag.Count; i++) if (!AreaAnchorGroupView.Equivalent(ag[i], bg[i])) return false;
            return AreaPanelDocument.Equivalent(a.Panel, b.Panel);
        }
        // Module summary and panel change on the module's own cadence, so they are composed onto the cached Core view
        // (#70); the Core view itself is never changed.
        public AreaManagementViewModel WithModuleContent(string summary, AreaPanelDocument panel)
        {
            var view = (AreaManagementViewModel)MemberwiseClone();
            view.Summary = summary; view.Panel = panel; view.composedFrom = this;
            return view;
        }
        public bool IsComposedFrom(AreaManagementViewModel core, string summary, AreaPanelDocument panel) =>
            ReferenceEquals(composedFrom, core) && Summary == summary && AreaPanelDocument.Equivalent(Panel, panel);
        private AreaManagementViewModel composedFrom;
        public void WriteProperties(IJsonWriter writer)
        {
            writer.PropertyName("stableId"); writer.Write(StableId);
            writer.PropertyName("typeKey"); writer.Write(TypeKey);
            writer.PropertyName("state"); writer.Write(State);
            writer.PropertyName("level"); if (Level.HasValue) writer.Write(Level.Value); else writer.WriteNull();
            writer.PropertyName("experience"); if (Experience != null) writer.Write(Experience); else writer.WriteNull();
            writer.PropertyName("summary"); writer.Write(Summary);
            writer.PropertyName("anchorRole"); if (AnchorRole != null) writer.Write(AnchorRole); else writer.WriteNull();
            writer.PropertyName("anchorName"); if (AnchorName != null) writer.Write(AnchorName); else writer.WriteNull();
            writer.PropertyName("pendingAnchor"); writer.Write(PendingAnchor);
            writer.PropertyName("confirmDelete"); writer.Write(ConfirmDelete);
            writer.PropertyName("tabs"); (Panel ?? new AreaPanelDocument()).Write(writer);
            writer.PropertyName("moduleShowsProgress"); writer.Write(Panel != null && Panel.ShowsProgress);
            var groups = AnchorGroups ?? new AreaAnchorGroupView[0];
            writer.PropertyName("anchorGroups"); writer.ArrayBegin((uint)groups.Count);
            foreach (var group in groups) group.Write(writer);
            writer.ArrayEnd();
            var anchors = Anchors ?? new AreaAnchorView[0];
            writer.PropertyName("anchors"); writer.ArrayBegin((uint)anchors.Count);
            foreach (var anchor in anchors)
            {
                writer.TypeBegin("CS2AreaSystem.AreaAnchorView");
                writer.PropertyName("key"); writer.Write(anchor.Key);
                writer.PropertyName("name"); if (anchor.Name != null) writer.Write(anchor.Name); else writer.WriteNull();
                writer.PropertyName("valid"); writer.Write(anchor.Valid);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }
    }
    public sealed class AreaAnchorView
    {
        // Name is null when the anchored building no longer exists; Valid=false marks an ineligible or removed anchor.
        public string Key, Name;
        public bool Valid;
    }
    public sealed class AreaAnchorGroupView
    {
        public string Role, DerivedStatusKey;
        public bool AssignmentSupported;
        public IReadOnlyList<AreaAnchorView> Anchors = new AreaAnchorView[0];
        public int? DerivedCount;
        public static bool Equivalent(AreaAnchorGroupView a, AreaAnchorGroupView b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Role != b.Role || a.AssignmentSupported != b.AssignmentSupported || a.DerivedCount != b.DerivedCount || a.DerivedStatusKey != b.DerivedStatusKey || a.Anchors.Count != b.Anchors.Count) return false;
            for (int i=0;i<a.Anchors.Count;i++) if (a.Anchors[i].Key != b.Anchors[i].Key || a.Anchors[i].Name != b.Anchors[i].Name || a.Anchors[i].Valid != b.Anchors[i].Valid) return false;
            return true;
        }
        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin("CS2AreaSystem.AreaAnchorGroupView");
            writer.PropertyName("role"); writer.Write(Role);
            writer.PropertyName("assignmentSupported"); writer.Write(AssignmentSupported);
            writer.PropertyName("derivedCount"); if(DerivedCount.HasValue) writer.Write(DerivedCount.Value); else writer.WriteNull();
            writer.PropertyName("derivedStatusKey"); if(DerivedStatusKey != null) writer.Write(DerivedStatusKey); else writer.WriteNull();
            writer.PropertyName("anchors"); writer.ArrayBegin((uint)Anchors.Count);
            foreach(var anchor in Anchors) {
                writer.TypeBegin("CS2AreaSystem.AreaAnchorView");
                writer.PropertyName("key");writer.Write(anchor.Key);writer.PropertyName("name");if(anchor.Name != null)writer.Write(anchor.Name);else writer.WriteNull();
                writer.PropertyName("valid");writer.Write(anchor.Valid);writer.TypeEnd();
            }
            writer.ArrayEnd();writer.TypeEnd();
        }
    }
}
