namespace CS2AreaSystem.Core.Policies
{
    // Derived every time from the saved desired state, current Level, lifecycle and effect availability; never saved.
    public readonly struct AreaPolicyRuntimeState
    {
        public bool Unlocked { get; }
        public bool DesiredEnabled { get; }
        public bool EffectAvailable { get; }
        public bool Suspended { get; }
        public bool Effective { get; }
        public string ReasonKey { get; }
        public AreaPolicyRuntimeState(bool unlocked, bool desiredEnabled, bool effectAvailable, bool suspended, bool effective, string reasonKey)
        {
            Unlocked = unlocked;
            DesiredEnabled = desiredEnabled;
            EffectAvailable = effectAvailable;
            Suspended = suspended;
            Effective = effective;
            ReasonKey = reasonKey;
        }
    }
}
