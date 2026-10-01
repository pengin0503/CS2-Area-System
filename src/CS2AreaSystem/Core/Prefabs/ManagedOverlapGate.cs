using System;
using System.Collections.Generic;
using Unity.Entities;
namespace CS2AreaSystem.Core.Prefabs
{
    // Pre-check before the managed overlap policy re-runs native area validation (#60). The policy only ever downgrades an
    // OverlapExisting error whose permanent entity (the native error's top-level owner of the other Area) is permitted to
    // overlap the preview. Native area validation only reports Areas whose search-tree bounds touch the preview's
    // triangles and skips Hidden or Slave ones, so when no such neighbour is permitted the re-validation cannot change the
    // native result and is skipped (no object/net/water dependency completion, no duplicate ValidateArea/Triangle).
    public static class ManagedOverlapGate
    {
        public static bool MayHavePermittedOverlap(Entity preview, IEnumerable<Entity> nearbyAreas, Func<Entity, bool> ignored,
            Func<Entity, Entity> topLevel, Func<Entity, Entity, bool> permits)
        {
            foreach (var area in nearbyAreas)
            {
                if (area == preview || ignored(area)) continue;
                var owner = topLevel(area);
                if (owner != preview && permits(preview, owner)) return true;
            }
            return false;
        }
    }
}
