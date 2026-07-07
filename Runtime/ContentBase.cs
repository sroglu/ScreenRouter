namespace PFound.ScreenRouter
{
    /// <summary>
    /// Base for navigable content (a full-screen Screen or a stacked modal Frame). Override the
    /// lifecycle hooks; the router raises them in order: Opening → (shown) → Open while live, then
    /// BecomeBackground/BecomeForeground as frames stack on top, and Closing → (hidden) → Close
    /// when it leaves. Engine-independent; the actual view is realized by an <see cref="IContentRenderer"/>.
    /// </summary>
    public abstract class ContentBase
    {
        /// <summary>True while this content is the foreground (top of stack / active screen).</summary>
        public bool IsForeground { get; private set; }

        protected virtual void OnOpening() { }
        protected virtual void OnOpen() { }
        protected virtual void OnClosing() { }
        protected virtual void OnClose() { }
        protected virtual void OnBecomeBackground() { }
        protected virtual void OnBecomeForeground() { }

        internal void RaiseOpening() => OnOpening();
        internal void RaiseOpen() { IsForeground = true; OnOpen(); }
        internal void RaiseClosing() => OnClosing();
        internal void RaiseClose() { IsForeground = false; OnClose(); }
        internal void RaiseBecomeBackground() { IsForeground = false; OnBecomeBackground(); }
        internal void RaiseBecomeForeground() { IsForeground = true; OnBecomeForeground(); }
    }
}
