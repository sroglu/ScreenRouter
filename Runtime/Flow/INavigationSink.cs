using System;

namespace PFound.ScreenRouter.Flow
{
    /// <summary>
    /// The engine-free seam the flow runner drives. A runner never touches Unity or the router
    /// singleton directly — it translates each declarative step into <see cref="SwitchScreen"/> /
    /// <see cref="OpenFrame"/> calls on this interface. Production wires the router-backed
    /// implementation (<c>ScreenRouterNavigationSink</c>); tests substitute a recording double, which
    /// is what lets the whole flow core compile and run under plain csc/mono.
    /// </summary>
    public interface INavigationSink
    {
        /// <summary>Make the screen of the given type the active full-screen destination.</summary>
        void SwitchScreen(Type screenType);

        /// <summary>Open the frame of the given type as a modal overlay.</summary>
        void OpenFrame(Type frameType);
    }
}
