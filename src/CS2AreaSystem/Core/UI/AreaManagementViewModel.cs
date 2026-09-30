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
