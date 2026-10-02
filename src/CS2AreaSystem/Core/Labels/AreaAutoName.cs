using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Entities;
namespace CS2AreaSystem.Core.Labels
{
    // Names a newly placed Area like "<type> <n>" and decides which new Area becomes the selection.
    public static class AreaAutoName
    {
        public const string FallbackTypeName = "Area";
        // The smallest n >= 1 whose exact name no live Area renders; other names (other types, player names) never block.
        public static string Next(string typeName, IEnumerable<string> takenNames)
        {
            var type = string.IsNullOrWhiteSpace(typeName) ? FallbackTypeName : typeName.Trim();
            var taken = new HashSet<string>(StringComparer.Ordinal);
            if (takenNames != null)
                foreach (var name in takenNames) if (name != null) taken.Add(name);
            for (int n = 1; ; n++)
            {
                var candidate = type + " " + n.ToString(CultureInfo.InvariantCulture);
                if (!taken.Contains(candidate)) return candidate;
            }
        }
        // Only an Area the player just drew becomes the selection; Areas identified on load are never selected.
        public static Entity SelectionAfterPlacement(bool areaToolActive, IReadOnlyList<Entity> placed) =>
            areaToolActive && placed != null && placed.Count > 0 ? placed[placed.Count - 1] : Entity.Null;
    }
}
