using System;
namespace CS2AreaSystem.Core.Persistence
{
    public sealed class AreaLoadSequence
    {
        public bool Ready { get; private set; }
        public void Reset() => Ready = false;
        public bool TryRecover(bool nativeReady, Action observeIds, Action rebuildMembership, Action validateModules)
        {
            if (Ready || !nativeReady) return false;
            observeIds();
            rebuildMembership();
            validateModules();
            Ready = true;
            return true;
        }
    }
}
