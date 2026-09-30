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
        public IReadOnlyList<AreaPanelTab> Tabs = new AreaPanelTab[0];
        // Comparing avoids rewriting the selected-info sections every frame.
        public static bool Equivalent(AreaManagementViewModel a, AreaManagementViewModel b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.StableId != b.StableId || a.TypeKey != b.TypeKey || a.State != b.State ||
                a.Experience != b.Experience || a.Summary != b.Summary || a.AnchorRole != b.AnchorRole || a.AnchorName != b.AnchorName ||
                a.Level != b.Level || a.PendingAnchor != b.PendingAnchor || a.ConfirmDelete != b.ConfirmDelete) return false;
            var left = a.Tabs ?? new AreaPanelTab[0];
            var right = b.Tabs ?? new AreaPanelTab[0];
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (left[i]?.Id != right[i]?.Id || left[i]?.LabelKey != right[i]?.LabelKey) return false;
            return true;
        }
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
            writer.PropertyName("tabs"); writer.ArrayBegin((uint)Tabs.Count);
            foreach (var tab in Tabs)
            {
                writer.TypeBegin("CS2AreaSystem.AreaPanelTab");
                writer.PropertyName("id"); writer.Write(tab.Id);
                writer.PropertyName("labelKey"); writer.Write(tab.LabelKey);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }
    }
}
