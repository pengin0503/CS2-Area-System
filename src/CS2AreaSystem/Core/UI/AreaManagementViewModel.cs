using System.Collections.Generic;
using Colossal.UI.Binding;
using CS2AreaSystem.Core.Modules;
namespace CS2AreaSystem.Core.UI
{
    public sealed class AreaManagementViewModel
    {
        public string StableId, Name, TypeKey, State, Experience, Summary, AnchorRole;
        public int? Level;
        public bool PendingAnchor;
        public IReadOnlyList<AreaPanelTab> Tabs = new AreaPanelTab[0];
        // RawValueBinding.Update() always sends a JSON event, so publishers compare before updating.
        public static bool Equivalent(AreaManagementViewModel a, AreaManagementViewModel b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.StableId != b.StableId || a.Name != b.Name || a.TypeKey != b.TypeKey || a.State != b.State ||
                a.Experience != b.Experience || a.Summary != b.Summary || a.AnchorRole != b.AnchorRole ||
                a.Level != b.Level || a.PendingAnchor != b.PendingAnchor) return false;
            var left = a.Tabs ?? new AreaPanelTab[0];
            var right = b.Tabs ?? new AreaPanelTab[0];
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (left[i]?.Id != right[i]?.Id || left[i]?.LabelKey != right[i]?.LabelKey) return false;
            return true;
        }
        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin("CS2AreaSystem.AreaManagementViewModel");
            writer.PropertyName("stableId"); writer.Write(StableId);
            writer.PropertyName("name"); writer.Write(Name);
            writer.PropertyName("typeKey"); writer.Write(TypeKey);
            writer.PropertyName("state"); writer.Write(State);
            writer.PropertyName("level"); if (Level.HasValue) writer.Write(Level.Value); else writer.WriteNull();
            writer.PropertyName("experience"); if (Experience != null) writer.Write(Experience); else writer.WriteNull();
            writer.PropertyName("summary"); writer.Write(Summary);
            writer.PropertyName("anchorRole"); if (AnchorRole != null) writer.Write(AnchorRole); else writer.WriteNull();
            writer.PropertyName("pendingAnchor"); writer.Write(PendingAnchor);
            writer.PropertyName("tabs"); writer.ArrayBegin((uint)Tabs.Count);
            foreach (var tab in Tabs)
            {
                writer.TypeBegin("CS2AreaSystem.AreaPanelTab");
                writer.PropertyName("id"); writer.Write(tab.Id);
                writer.PropertyName("labelKey"); writer.Write(tab.LabelKey);
                writer.TypeEnd();
            }
            writer.ArrayEnd(); writer.TypeEnd();
        }
    }
}
