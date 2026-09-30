using System.Globalization;
using Unity.Entities;
namespace CS2AreaSystem.Core.UI
{
    public static class AreaUIActionGuard
    {
        // SelectedInfoUISystem.selectedEntity is refreshed from ToolSystem.selected during its own update, while
        // SetSelection changes ToolSystem.selected immediately. A mutating action must match both: the cached
        // selection the panel was built from and the live selection. SelectedInfo's selection filter only remaps
        // icons, residents and pets, so for a managed area both values are the same entity once refreshed.
        public static bool Matches(Entity published, Entity cachedSelection, Entity liveSelection, ulong actualId, string expectedId, bool valid) =>
            valid && published != Entity.Null && published == cachedSelection && published == liveSelection &&
            IdMatches(actualId, expectedId);
        // Cancelling an anchor assignment reselects the pending area only when it still exists (Entity.Exists checks
        // the version) and still carries the same stable ID; otherwise the stale assignment is simply dropped.
        public static bool CanRestorePending(bool pendingValid, ulong currentId, ulong pendingId, string expectedId) =>
            pendingValid && currentId == pendingId && IdMatches(pendingId, expectedId);
        private static bool IdMatches(ulong actualId, string expectedId) =>
            actualId != 0 && ulong.TryParse(expectedId, NumberStyles.None, CultureInfo.InvariantCulture, out var expected) && actualId == expected;
    }
}
