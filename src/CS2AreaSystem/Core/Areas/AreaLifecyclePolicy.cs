namespace CS2AreaSystem.Core.Areas
{
    public static class AreaLifecyclePolicy
    {
        public static AreaLifecycleState Resolve(bool moduleKnown, bool activationRequirementsMet, bool wasDeleted)
        {
            if (wasDeleted) return AreaLifecycleState.Deleted;
            if (!moduleKnown) return AreaLifecycleState.ModuleUnavailable;
            return activationRequirementsMet ? AreaLifecycleState.Active : AreaLifecycleState.Suspended;
        }
    }
}
