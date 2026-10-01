#nullable enable
using Game;
namespace CS2AreaSystem.Tests.Support;
// Replays Game.UpdateSystem's 1.6.2f1 ordering (UpdateSystem.Register/Refresh/AddSystemUpdate in the supplied Game.dll):
// UpdateAt/Before/After receive add index ++n, ++n - 1000000 and ++n + 1000000; unanchored systems sort by
// (phase, add index); anchored systems are inserted around their anchor, negative indices before it and the rest after.
// Vanilla SystemOrder.Initialize runs before any mod OnLoad, so its calls come first.
public static class UpdateSchedule {
    private sealed record Entry(SystemUpdatePhase Phase, int AddIndex, Type System);
    public static List<Type> Resolve(SystemUpdatePhase phase, params IEnumerable<UpdateSystemCall>[] callsInRegistrationOrder) {
        var systems = new List<Entry>();
        var anchored = new Dictionary<Type, List<Entry>>();
        int addIndex = 0;
        foreach (var calls in callsInRegistrationOrder)
        foreach (var call in calls) {
            if (call.System == null) continue;
            int index = call.Method switch {
                "UpdateAt" => ++addIndex,
                "UpdateBefore" => ++addIndex - 1000000,
                "UpdateAfter" => ++addIndex + 1000000,
                _ => throw new InvalidOperationException(call.Method),
            };
            var entry = new Entry(call.Phase, index, call.System);
            if (call.Anchor == null) systems.Add(entry);
            else {
                if (!anchored.TryGetValue(call.Anchor, out var list)) anchored.Add(call.Anchor, list = new List<Entry>());
                list.Add(entry);
            }
        }
        var order = new List<Type>();
        foreach (var entry in systems.Where(e => e.Phase == phase).OrderBy(e => e.AddIndex)) Add(entry, anchored, order, 0);
        return order;
    }
    private static void Add(Entry entry, Dictionary<Type, List<Entry>> anchored, List<Type> order, int depth) {
        if (++depth == 100) throw new InvalidOperationException("Too deep system order");
        if (!anchored.TryGetValue(entry.System, out var list)) { order.Add(entry.System); return; }
        var sorted = list.OrderBy(e => e.Phase).ThenBy(e => e.AddIndex).ToList();
        int i = 0;
        for (; i < sorted.Count; i++) {
            if (sorted[i].Phase != entry.Phase) continue;
            if (sorted[i].AddIndex >= 0) break;
            Add(sorted[i], anchored, order, depth);
        }
        order.Add(entry.System);
        for (; i < sorted.Count && sorted[i].Phase == entry.Phase; i++) Add(sorted[i], anchored, order, depth);
    }
}
