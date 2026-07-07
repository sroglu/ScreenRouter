# PFound.ScreenRouter

A UI presentation router for Unity: one active **screen** plus a stack of modal **frames**, with
guards, runtime type registration, four pooling tiers, animated transitions, and a background-blur
surface. The view *is* the content — one prefab carries both the `ContentBase` behaviour and its
`IContentRenderer`.

## Quick reference

Add a `ScreenRouterHost` (wired to a `ScreenRouterConfig` + a content-root transform) to a scene,
then route through the static seam:

```csharp
ScreenRouter.Instance.SwitchScreen<MainMenuScreen>();
ScreenRouter.Instance.OpenFrame<SettingsFrame>();
ScreenRouter.Instance.RegisterGuard<SettingsFrame>(
    () => IsLoggedIn ? GuardDecision.Pass() : GuardDecision.Reroute<LoginFrame>());
ScreenRouter.Instance.CloseFrame();   // top of the stack
```

## Dependencies

`UnityEngine.UI` (uGUI backend); no PFound modules, no external packages.

## Docs

Deep reference: [MODULE.md](MODULE.md) — assemblies, full API, host wiring, lifecycle, pooling tiers,
definitions/config, rendering, animation, background blur, extension points (guards / runtime types /
swappable backends), and testing.
</content>
