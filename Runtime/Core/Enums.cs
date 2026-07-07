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
        /// <summary>Tear the instance down the moment it finishes closing.</summary>
        Ephemeral = 0,

        /// <summary>Keep the instance parked after close, but reclaim it once it has been
        /// idle for longer than the router's configured grace period.</summary>
        Recyclable = 1,

        /// <summary>Keep the instance parked for the life of the current scene; discard it
        /// when the scene is swapped out.</summary>
        SceneLifetime = 2,

        /// <summary>Keep the instance parked indefinitely; never reclaimed automatically.</summary>
        AppLifetime = 3,
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
