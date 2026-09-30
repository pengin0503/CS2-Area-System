using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Modules;
using Unity.Entities;
namespace CS2AreaSystem.Core.Membership
{
    public readonly struct ContainingArea
    {
        public Entity Area { get; }
        public ulong StableId { get; }
        public int TypeId { get; }
        public IAreaModule Module { get; }
        public ContainingArea(Entity area, ulong stableId, int typeId, IAreaModule module)
        { Area = area; StableId = stableId; TypeId = typeId; Module = module; }
    }
    public static class AreaMembershipClassification
    {
        public static bool Include(IAreaMemberClassifier classifier, Entity member, Entity area, Action<Exception> report)
        {
            try { return classifier == null || classifier.Classify(member, area) != AreaMemberRole.None; }
            catch (Exception error) { report(error); return false; }
        }
        // Module failures are isolated per area/type: reading MemberClassifier is inside the same boundary as Classify.
        // An unknown module (null) keeps the geometric relation.
        public static bool IncludeModule(IAreaModule module, Entity member, Entity area, Action<Exception> report)
        {
            IAreaMemberClassifier classifier;
            try { classifier = module?.MemberClassifier; }
            catch (Exception error) { report(error); return false; }
            return Include(classifier, member, area, report);
        }
        public static List<AreaMembership> Collect(IEnumerable<ContainingArea> areas, Entity member, Action<int, Exception> report)
        {
            var results = new List<AreaMembership>();
            foreach (var area in areas)
            {
                var typeId = area.TypeId;
                if (IncludeModule(area.Module, member, area.Area, error => report(typeId, error)))
                    results.Add(new AreaMembership(area.StableId, typeId, false));
            }
            return results;
        }
    }
}
