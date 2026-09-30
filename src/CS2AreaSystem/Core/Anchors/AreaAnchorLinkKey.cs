using System.Collections.Generic;
using System.Globalization;
using Unity.Entities;
namespace CS2AreaSystem.Core.Anchors
{
    // Opaque UI key for one persisted anchor link. It is resolved only against the selected area's current links,
    // so a stale or fabricated key can never address another entity.
    public static class AreaAnchorLinkKey
    {
        public static string Format(Entity anchor) =>
            anchor.Index.ToString(CultureInfo.InvariantCulture) + "." + anchor.Version.ToString(CultureInfo.InvariantCulture);
        public static bool TryResolve(IReadOnlyList<AreaAnchorLink> links, AreaAnchorRole role, string key, out Entity anchor)
        {
            anchor = Entity.Null;
            if (string.IsNullOrEmpty(key) || links == null) return false;
            foreach (var link in links)
                if (link.Role == role && Format(link.Anchor) == key) { anchor = link.Anchor; return true; }
            return false;
        }
    }
}
