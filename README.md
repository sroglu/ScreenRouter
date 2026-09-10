# PFound.ScreenRouter

A UI presentation router for Unity: one active **screen** plus a stack of modal **frames**, with
guards, runtime type registration, four pooling tiers, animated transitions, and a background-blur
surface. The view *is* the content — one prefab carries both the `ContentBase` behaviour and its
`IContentRenderer`.

## Quick reference

Author one `ScreenRouterConfig` asset (*Create > PFound/ScreenRouter/Router Config*) and add screens
and frames as inline rows in its lists — the `[Serializable]` `ScreenDefinition` / `FrameDefinition`
records live inside that single asset, there are no per-screen/frame asset files. Add a
`ScreenRouterHost` (wired to the config + a content-root transform) to a scene, then route through
the static seam:

```csharp
ScreenRouter.Instance.SwitchScreen<MainMenuScreen>();
ScreenRouter.Instance.OpenFrame<SettingsFrame>();
ScreenRouter.Instance.RegisterGuard<SettingsFrame>(
    () => IsLoggedIn ? GuardDecision.Pass() : GuardDecision.Reroute<LoginFrame>());
ScreenRouter.Instance.CloseFrame();   // top of the stack
```

### Declarative NavigationFlow

For multi-step navigation with history, an engine-free flow layer sits **over** the router: an
immutable, action-keyed step graph plus a runner that adds the back-navigation the imperative router
does not track. Author the graph with a fluent builder (cycle-checked at build), then drive it:

```csharp
var flow = NavigationFlow.Define<MenuAction>("Main")
    .Screen<MainMenuScreen>().OnAction(MenuAction.Play, t => t.Screen<GameScreen>())
    .Screen<GameScreen>().OnAction(MenuAction.Settings, t => t.Frame<SettingsFrame>())
    .Build();

var runner = NavigationFlowHost.CreateRunner(flow);   // wired to ScreenRouter.Instance
runner.Start();                    // routes to MainMenuScreen
runner.Execute(MenuAction.Play);   // -> GameScreen
runner.Back();                     // history/back-navigation -> MainMenuScreen
```

Flows nest: an edge may enter a **sub-flow** with its own back-stack, so `Back()` unwinds within a
sub-flow before exiting to the parent. See [MODULE.md](MODULE.md#declarative-navigationflow).

## Dependencies

`UnityEngine.UI` (uGUI backend); no PFound modules, no external packages.

## Docs

Deep reference: [MODULE.md](MODULE.md) — assemblies, full API, host wiring, lifecycle, pooling tiers,
definitions/config, rendering, animation, background blur, extension points (guards / runtime types /
swappable backends), and testing.
