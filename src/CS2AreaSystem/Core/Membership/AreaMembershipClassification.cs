using System;
using CS2AreaSystem.Core.Modules;
using Unity.Entities;
namespace CS2AreaSystem.Core.Membership
{
    public static class AreaMembershipClassification
    {
        public static bool Include(IAreaMemberClassifier classifier, Entity member, Entity area, Action<Exception> report)
        {
            try { return classifier == null || classifier.Classify(member, area) != AreaMemberRole.None; }
            catch (Exception error) { report(error); return false; }
        }
    }
}
