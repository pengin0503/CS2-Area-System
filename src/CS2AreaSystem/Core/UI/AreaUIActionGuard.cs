using System.Globalization;
using Unity.Entities;
namespace CS2AreaSystem.Core.UI
{
    public static class AreaUIActionGuard
    {
        public static bool Matches(Entity published, Entity selected, ulong actualId, string expectedId, bool valid) =>
            valid && published != Entity.Null && published == selected && actualId != 0 &&
            ulong.TryParse(expectedId, NumberStyles.None, CultureInfo.InvariantCulture, out var expected) && actualId == expected;
    }
}
