namespace CS2AreaSystem.Core.Membership
{
    // Design §9.4: v1 membership covers building-like entities only; trees/props would grow the index without consumers.
    public static class AreaMembershipCandidatePolicy
    {
        public static bool IsCandidate(bool hasTransform, bool isStatic, bool isBuilding, bool deleted, bool temp, bool overridden) =>
            hasTransform && isStatic && isBuilding && !deleted && !temp && !overridden;
    }
}
