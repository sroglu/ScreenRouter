using System;

namespace PFound.ScreenRouter.Flow
{
    /// <summary>
    /// The thin Unity seam over the engine-free flow core: the production <see cref="INavigationSink"/>
    /// that drives the live <see cref="ScreenRouter"/> singleton. This is the only flow type that
    /// touches <c>ScreenRouter.Instance</c>; the runner and graph never reference the router directly,
    /// which is what keeps the flow core mono-testable. <c>Instance</c> throws fail-fast if no host is
    /// active, so a flow run outside a live router surfaces immediately.
    /// </summary>
    public sealed class ScreenRouterNavigationSink : INavigationSink
    {
        public void SwitchScreen(Type screenType) => ScreenRouter.Instance.SwitchScreen(screenType);

        public void OpenFrame(Type frameType) => ScreenRouter.Instance.OpenFrame(frameType);
    }

    /// <summary>Convenience factory for wiring a flow runner to the live router in one call.</summary>
    public static class NavigationFlowHost
    {
        /// <summary>Create a runner for <paramref name="flow"/> backed by the live router singleton.</summary>
        public static NavigationFlowRunner<TAction> CreateRunner<TAction>(NavigationFlow<TAction> flow) =>
            new NavigationFlowRunner<TAction>(flow, new ScreenRouterNavigationSink());
    }
}
