# PFound.ScreenRouter

A UI presentation router for Unity: one active **screen** plus a stack of modal **frames**, with
guards, runtime type registration, four pooling tiers, animated transitions, and a background-blur
surface. The view *is* the content — a content prefab carries both a `ContentBase` MonoBehaviour and
an `IContentRenderer` component; there is no separate view instantiation.

## Model

- **Single active screen** — full-screen destinations. Switching closes the outgoing screen
  atomically before the incoming one opens.
- **Modal frame stack** — a linked list, bottom-to-top (first = bottom, last = top). Opening a frame
  sends the current top to the background; closing brings the new top to the foreground.
- **Pure state core** — frame-stack ordering, stacking-rule evaluation, guard resolution, pooling
  eligibility, and the frame queue are engine-free helpers (`Runtime/Core`) so they are unit-tested
  without Unity.

## Public API (`ScreenRouter`)

Reached through a static seam: `ScreenRouter.Instance`, `ScreenRouter.HasInstance`,
`ScreenRouter.TryGetInstance(out router)`. A `ScreenRouterHost` MonoBehaviour constructs and
publishes the router and pumps its per-frame tick.

**Screens:** `SwitchScreen<T>()`, `SwitchScreen<T,TConfig>(config)`, `SwitchScreen(Type, config)`,
`GetActiveScreen<T>()`, `IsScreenOpen<T>()`, `CloseActiveScreen()`.

**Frames:** `OpenFrame<T>()`, `OpenFrame<T,TConfig>(config)`, `OpenFrame(Type, config)`,
`CloseFrame()` (top), `CloseFrame(frame)`, `CloseAllFrames()` (instant, clears queue),
`QueueFrame<T>()` / `QueueFrame<T,TConfig>(config)` (opens when the stack empties; de-duplicated;
capped by the config's max-queue), `ClearQueue()`, `GetActiveFrame<T>()` (top-down search),
`HasActiveFrames`, `IsFrameOpen<T>()`, `CanOpenFrame<T>()`.

**Guards:** `RegisterGuard<T>(() => GuardDecision.Pass()/Block()/Reroute<U>())`,
`UnregisterGuard<T>(guard)`, `ClearGuards<T>()`. Multiple guards for a type are AND-ed; a reroute
restarts resolution against the destination and a reroute cycle is detected instead of looping. A
re-entrancy flag prevents a guard that itself triggers a nav from re-running guard checks.

**Runtime type registration:** `RegisterFrameType<T>(FrameDefinition)`, `UnregisterFrameType<T>()`,
`RegisterScreenType<T>(ScreenDefinition)`, `UnregisterScreenType<T>()`. Runtime registrations
**shadow** the central config; unregister destroys a live instance before dropping the registration.

## Content lifecycle

`ContentBase` fires hooks in this order across an open/close round trip:

```
OnAwake (once) -> OnStart (once) -> OnOpening -> OnOpen
 -> [OnDemoted / OnPromoted as other content stacks over / off it]
 -> OnClosing -> OnClose -> OnDestroyed (only when actually torn down)
```

`Screen` / `Frame` derive from `ContentBase`; `Frame` adds the five-state visibility machine
(`NotInitialized -> Opening -> Opened -> Closing -> Closed`, and back to `Opening` when a pooled
frame is reopened) and an `OnCloseButtonClicked` hook. Generic `Screen<TConfig>` / `Frame<TConfig>`
receive a typed config assigned **before** `OnOpening`; reading `Config` before assignment throws.
Each content also exposes a `ClosedToken` that trips on close.

## Pooling tiers

| Tier             | On close            | Idle reaper | Scene change |
|------------------|---------------------|-------------|--------------|
| `Ephemeral`      | destroyed           | n/a         | n/a          |
| `Recyclable`     | parked for reuse    | reclaimed   | discarded    |
| `SceneLifetime`  | parked for reuse    | no          | discarded    |
| `AppLifetime`    | parked for reuse    | no          | survives     |

First open runs Awake/Start once; a reopen reactivates without re-Awake, refreshes the closed token,
and invalidates any stale typed config.

## Definitions & config

- `ContentDefinition` (abstract): content type, prefab, pooling tier.
- `FrameDefinition`: adds `BlockOverlays`, an overlay-exemptions list, `AlwaysStackable`
  (always wins), `CloseOnBackdropPress`, and opening/closing animation presets. `CreateRuntime(...)`
  builds an in-memory instance for runtime registration.
- `ScreenDefinition`: adds an optional transition preset. `CreateRuntime(...)` factory.
- `ScreenRouterConfig` (ScriptableObject): screen/frame definition lists, base sorting order +
  increment, pool idle timeout, max frame-queue size, and background-blur settings. Type-keyed
  lookups are cached and rebuilt on demand. Optional features are gated by **authored toggles** (e.g.
  `Use Background Blur`), never inferred from a null reference; the config editor greys out the gated
  fields when a toggle is off.
- `SerializableType`: a Unity-serializable `System.Type` wrapper (assembly-qualified name, value
  equality) with an Editor drawer that offers a dropdown of concrete `ContentBase` subclasses.

## Rendering

`IContentRenderer` exposes `SortingOrder`, `Alpha`, `Interactable`, `BlocksRaycasts`, `Initialize`,
and the three resting/transition states `SetStateOpened/Closed/Animating`. The renderer is discovered
per content via `GetComponent` on the same GameObject in `ContentBase.Awake`. Two backends ship:

- `CanvasGroupContentRenderer` (uGUI): a local `Canvas` with `overrideSorting` + a `CanvasGroup`.
- `UIDocumentContentRenderer` (UI Toolkit): panel sort order + root-element opacity/picking.

In the animating state the content stays raycast-blocking but non-interactive, so nothing underneath
is clicked through a transition.

## Animation

`FrameAnimationPreset` describes an independent scale + fade transition (each with its own normalized
curve over a shared duration); a disabled or zero-length preset snaps instantly. Transitions run
through `IFrameTweenBackend`, so the tween engine is a swap. The bundled `ManualFrameTweenBackend` is
dependency-free and advanced by the host each frame; it is interrupt-safe (starting a transition on a
target cancels any in-flight one on it) and never calls back on a destroyed target. A production build
can implement `IFrameTweenBackend` over a target-bound tween library and leave `Tick` a no-op.

## Background blur

`BackgroundBlurOverlay` dims behind the frame stack. Target alpha deepens with depth on a
saturating curve that eases toward a ceiling (each stacked frame closes a fixed fraction of the
remaining gap), it sorts just below the top frame, eases toward its target with frame-rate
independent smoothing, and hides when the stack empties. A press on the surface, via
`BlurClickForwarder`, closes the top frame when that frame opts in with `CloseOnBackdropPress`.

## Testing

The engine-free state logic has a standalone csc/mono runner and an equivalent Unity EditMode suite:

```
csc -define:SCREENROUTER_PURE_TESTS -out:/tmp/sr.exe \
    Runtime/Core/*.cs Tests/PureCore/PureCoreTests.cs && mono /tmp/sr.exe
```

The MonoBehaviour/lifecycle layer is verified in the Unity editor on integration.

## Layout

- `Runtime/` — `Core/` (engine-free state logic), `Content/`, `Rendering/`, `Definitions/`,
  `Animation/`, `Router/`. Assembly `PFound.ScreenRouter`.
- `Editor/` — `SerializableType` drawer + config editor. Assembly `PFound.ScreenRouter.Editor`.
- `Tests/` — `PureCore/` (csc/mono runner) + `EditMode/` (NUnit).

## Deferred

A declarative Flow/Runner DSL (multi-step nav flows with a back-stack and sub-flows) is a planned
future addition and is not part of this release.
