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

## Dependencies

`UnityEngine.UI` (uGUI backend); no PFound modules, no external packages.

## Docs

Deep reference: [MODULE.md](MODULE.md) — assemblies, full API, host wiring, lifecycle, pooling tiers,
definitions/config, rendering, animation, background blur, extension points (guards / runtime types /
swappable backends), and testing.
</content>
