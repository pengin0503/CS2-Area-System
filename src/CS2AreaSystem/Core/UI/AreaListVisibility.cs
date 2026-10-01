namespace CS2AreaSystem.Core.UI
{
    // The selection button is always available in a ready session. The saved preference controls
    // initial expansion, and changes apply once without undoing subsequent manual opening/closing.
    public sealed class AreaListVisibility
    {
        private bool? previousPreference;
        public bool Available { get; private set; }
        public bool Open { get; private set; }
        public void Update(bool ready, bool expandByDefault)
        {
            Available = ready;
            if (!ready) { Open = false; previousPreference = null; return; }
            if (previousPreference != expandByDefault)
            { Open = expandByDefault; previousPreference = expandByDefault; }
        }
        public void SetOpen(bool open) => Open = Available && open;
    }
}
