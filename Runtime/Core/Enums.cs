// Engine-free enumerations shared by the router state logic and the Unity layer.
// These live in the runtime assembly but reference nothing from UnityEngine so the
// state logic can be exercised by the standalone csc/mono test runner.

namespace PFound.ScreenRouter
{
    /// <summary>
    /// Lifetime policy applied to a piece of content once it is closed.
    /// </summary>
    public enum PoolingType
    {
        /// <summary>The instance is destroyed as soon as it finishes closing.</summary>
        DestroyOnClose = 0,

        /// <summary>The instance is kept after it closes so it can be reused. It is
        /// destroyed only if it stays unused longer than the router's idle grace period.</summary>
        KeepAndReuse = 1,

        /// <summary>The instance is kept and reused for as long as the current scene is
        /// loaded. It is destroyed when the scene changes.</summary>
        KeepForScene = 2,

        /// <summary>The instance is kept and reused for the whole run of the app. It is
        /// never destroyed automatically.</summary>
        KeepAlways = 3,
    }

    /// <summary>
    /// The five discrete visibility phases a modal frame moves through. Reopening a pooled
    /// frame walks it from <see cref="Closed"/> straight back to <see cref="Opening"/>.
    /// </summary>
    public enum FrameState
    {
        NotInitialized = 0,
        Opening = 1,
        Opened = 2,
        Closing = 3,
        Closed = 4,
    }

    /// <summary>How a guard votes on an incoming open request.</summary>
    public enum GuardVerdict
    {
        Allow = 0,
        Reject = 1,
        Redirect = 2,
    }
}
