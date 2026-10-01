using System;
namespace CS2AreaSystem.Core.Prefabs
{
    public enum AreaToolbarExposure { Pending, Native, Unavailable }
    public readonly struct AreaToolbarDecision
    {
        public bool RegisterPrefabs { get; }
        public bool AttachToolbar { get; }
        public bool BecameUnavailable { get; }
        public AreaToolbarDecision(bool registerPrefabs, bool attachToolbar, bool becameUnavailable)
        {
            RegisterPrefabs = registerPrefabs; AttachToolbar = attachToolbar; BecameUnavailable = becameUnavailable;
        }
    }
    // Decides once per session whether Area prefabs carry the native toolbar relationship (UIObject.m_Group). The game only
    // updates the world after its prefabs are loaded (GameManager.state >= WorldReady), so the vanilla toolbar host is normally
    // present on the first attempt; a bounded number of attempts covers transient failures. When the graph cannot be built the
    // prefabs are registered without m_Group: saved managed areas keep resolving their prefab, only drawing new ones is lost.
    // The decision is not reverted: an Area prefab's m_Group is consumed once by UIObject.LateInitialize when it is added.
    public sealed class AreaToolbarExposureState
    {
        public const int MaxFailedAttempts = 8;
        public AreaToolbarExposure Exposure { get; private set; } = AreaToolbarExposure.Pending;
        public int FailedAttempts { get; private set; }
        public AreaToolbarDecision Update(Func<bool> tryEnsureGraph)
        {
            if (tryEnsureGraph == null) throw new ArgumentNullException(nameof(tryEnsureGraph));
            if (Exposure == AreaToolbarExposure.Unavailable) return new AreaToolbarDecision(true, false, false);
            if (tryEnsureGraph())
            {
                Exposure = AreaToolbarExposure.Native;
                return new AreaToolbarDecision(true, true, false);
            }
            // Prefabs already registered with the graph keep it; registration of the rest waits for the graph to recover.
            if (Exposure == AreaToolbarExposure.Native) return new AreaToolbarDecision(false, false, false);
            if (++FailedAttempts < MaxFailedAttempts) return new AreaToolbarDecision(false, false, false);
            Exposure = AreaToolbarExposure.Unavailable;
            return new AreaToolbarDecision(true, false, true);
        }
    }
}
