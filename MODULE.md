# ScreenRouter

## Purpose

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

## Assemblies

| Assembly | Folder | Notes |
|----------|--------|-------|
| `PFound.ScreenRouter` | `Runtime/` | Runtime + engine-free core. `autoReferenced: false`; references `UnityEngine.UI`. |
| `PFound.ScreenRouter.Editor` | `Editor/` | `SerializableType` drawer + config editor. Editor-only, `autoReferenced: false`. |
| `PFound.ScreenRouter.Tests.EditMode` | `Tests/EditMode/` | NUnit EditMode suite; gated by `UNITY_INCLUDE_TESTS`. |

The `Runtime/Core` state logic references nothing from `UnityEngine`, so it also compiles under the
standalone csc/mono runner in `Tests/PureCore` (see [Testing](#testing)).

## Dependencies

- **PFound modules:** none.
- **Third-party:** `UnityEngine.UI` (uGUI backend). The UI Toolkit backend uses `UIElements`, part
  of the engine. No external packages, no scripting defines (the pure-test runner defines
  `SCREENROUTER_PURE_TESTS` for itself only).

## Key Types

**Router (`Runtime/Router`)**
- `ScreenRouter` — the presentation orchestrator; a plain class, not a MonoBehaviour.
- `ScreenRouterHost` — thin MonoBehaviour that constructs, publishes, and pumps the router.
- `BackgroundBlurOverlay` — manages the single dim surface behind the frame stack.
- `BlurClickForwarder` — MonoBehaviour on the blur prefab; forwards a pointer-down to the overlay.

**Content (`Runtime/Content`)**
- `ContentBase` — shared MonoBehaviour base; owns the renderer handle, the closed token, and the
  lifecycle drivers.
- `Screen` / `Screen<TConfig>` — full-screen destination; typed variant receives a config.
- `Frame` / `Frame<TConfig>` — modal overlay; adds the `VisibilityState` machine and
  `OnCloseButtonClicked`.

**Definitions (`Runtime/Definitions`)**
- `ContentDefinition` (abstract, `[Serializable]`) — content type + prefab + pooling tier; a plain
  class, not a ScriptableObject.
- `FrameDefinition`, `ScreenDefinition` (`[Serializable]`) — plain authoring records serialized
  **inline** in the config's screen/frame lists; each also has a `CreateRuntime(...)` factory.
- `ScreenRouterConfig` (ScriptableObject) — the **single** project-wide config asset; owns the inline
  definition lists. The only `[CreateAssetMenu]` type in the module.
- `SerializableType` — Unity-serializable `System.Type` wrapper with value equality (Editor drawer
  offers a dropdown of concrete `ContentBase` subclasses).

**Rendering (`Runtime/Rendering`)**
- `IContentRenderer` — the presentation-surface contract.
- `CanvasGroupContentRenderer` (uGUI), `UIDocumentContentRenderer` (UI Toolkit) — the two backends.

**Animation (`Runtime/Animation`)**
- `FrameAnimationPreset` — inspector-authored scale + fade transition.
- `IFrameTweenBackend` — the swappable tween contract.
- `ManualFrameTweenBackend` — bundled dependency-free backend, ticked by the host.

**Core (`Runtime/Core`, engine-free)**
- `FrameStackModel<T>`, `FrameQueueModel<T>` — stack and de-duplicated bounded queue.
- `StackingRules` / `StackingEvaluator` — stacking-policy evaluation.
- `GuardDecision`, `GuardVerdict`, `GuardRegistry`, `GuardResolution`, `GuardResolutionKind` —
  guard voting and reroute resolution.
- `PoolingPolicy`, `PoolingType`, `FrameState` — pooling and visibility enumerations + policy.
- `DefinitionRegistry<TDef>` — two-tier (runtime shadow over config) definition lookup.

## Public API

Reached through a static seam: `ScreenRouter.Instance`, `ScreenRouter.HasInstance`,
`ScreenRouter.TryGetInstance(out router)`. A `ScreenRouterHost` MonoBehaviour constructs and
publishes the router and pumps its per-frame tick. `Instance` throws if no host is active
(fail-fast — no silent null).

**Screens**
```csharp
void SwitchScreen<T>() where T : Screen;
void SwitchScreen<T, TConfig>(TConfig config) where T : Screen;
void SwitchScreen(Type screenType, object config = null);
T    GetActiveScreen<T>() where T : Screen;
bool IsScreenOpen<T>() where T : Screen;
void CloseActiveScreen();
```

**Frames**
```csharp
T    OpenFrame<T>() where T : Frame;
T    OpenFrame<T, TConfig>(TConfig config) where T : Frame;
Frame OpenFrame(Type frameType, object config = null);
bool CloseFrame();                 // top
bool CloseFrame(Frame frame);      // specific
void CloseAllFrames();             // instant, clears queue
bool QueueFrame<T>() where T : Frame;
bool QueueFrame<T, TConfig>(TConfig config) where T : Frame;
void ClearQueue();
T    GetActiveFrame<T>() where T : Frame;   // top-down search
bool HasActiveFrames { get; }
bool IsFrameOpen<T>() where T : Frame;
bool CanOpenFrame<T>() where T : Frame;
```
`QueueFrame` opens immediately when the stack is empty; otherwise it enqueues (de-duplicated by type,
capped by the config's max-queue) and opens when the stack next empties.

**Guards** — see [Extension points](#extension-points).
```csharp
void RegisterGuard<T>(Func<GuardDecision> guard);
bool UnregisterGuard<T>(Func<GuardDecision> guard);
void ClearGuards<T>();
```

**Runtime type registration** — see [Extension points](#extension-points).
```csharp
void RegisterFrameType<T>(FrameDefinition definition) where T : Frame;
bool UnregisterFrameType<T>() where T : Frame;
void RegisterScreenType<T>(ScreenDefinition definition) where T : Screen;
bool UnregisterScreenType<T>() where T : Screen;
```

## Setup / wiring

`ScreenRouterHost` is a thin shell: on `Awake` it builds the router, publishes it on the static seam,
and (via `OnEnable`) subscribes to `SceneManager.activeSceneChanged`. It wires two serialized fields —
`_config` (the `ScreenRouterConfig`) and `_contentRoot` (the transform every content instance is
parented under). **The host does not persist itself** — there is no `DontDestroyOnLoad`, and nothing
auto-creates the host. **Where the host lives, and whether it survives scene loads, is the consumer's
decision.**

Two supported placements:

- **Persistent host (recommended for multi-scene apps).** Create the host **once** (e.g. in a
  bootstrap scene) and make its **whole subtree — host + Canvas + `_contentRoot` — `DontDestroyOnLoad`
  as one unit.** This is required, not optional: the router instantiates all content under
  `_contentRoot`, so persisting the host GameObject alone (leaving the content root a scene object)
  destroys live content and leaves the router parenting under a dead transform. With a persistent
  host the pooling tiers pay off: on each scene change the router keeps `AppLifetime` content and
  drops the rest, so cross-scene shell UI (top bar, toast/loading layer) survives while per-scene UI
  is cleaned up automatically. Use one shared `ScreenRouterConfig` app-wide.

- **Scene-scoped host (single-scene apps, or teams that don't need cross-scene UI).** Place a host +
  Canvas + content root in each scene. Simpler, no `DontDestroyOnLoad`, but `AppLifetime` and all
  cross-scene pooling become inert (everything dies with its scene), and you must ensure only one host
  is alive at a time — the static `Instance` is process-wide and `Publish` is unconditional last-wins,
  so overlapping hosts (or routing during a scene-transition gap) can leave `Instance` pointing at a
  torn-down router. Single-scene routing itself works fully.

Rule of thumb: if any UI must outlive a scene load, use a persistent host and mark that UI
`AppLifetime`; give per-scene UI `SceneLifetime`/`Ephemeral` so it is dropped on each swap.

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
Each content also exposes a `ClosedToken` (a `CancellationToken`) that trips on close and is refreshed
on every (re)open. Games override the protected `On*` hooks; they never call the internal `Drive*`
drivers.

## Pooling tiers

| Tier             | On close            | Idle reaper | Scene change |
|------------------|---------------------|-------------|--------------|
| `Ephemeral`      | destroyed           | n/a         | n/a          |
| `Recyclable`     | parked for reuse    | reclaimed   | discarded    |
| `SceneLifetime`  | parked for reuse    | no          | discarded    |
| `AppLifetime`    | parked for reuse    | no          | survives     |

First open runs Awake/Start once; a reopen reactivates without re-Awake, refreshes the closed token,
and invalidates any stale typed config. The tier is read off `ContentDefinition.Pooling` and decided
by the engine-free `PoolingPolicy` (`DestroyOnClose` / `CanRevive` / `ReclaimWhenIdle` /
`DropOnSceneChange`). The reaper runs from the host tick against `ScreenRouterConfig.PoolIdleTimeoutSeconds`.

## Definitions & config

Authoring lives in **one asset**. `ScreenRouterConfig` is the only ScriptableObject (and the only
`[CreateAssetMenu]` type — *PFound/ScreenRouter/Router Config*); every screen and frame is described
by a plain `[Serializable]` definition serialized **inline** inside it. There are **no per-screen or
per-frame asset files** — you add rows to the config's `_screens` / `_frames` lists in the inspector.

- `ContentDefinition` (abstract, `[Serializable]`): the shared authoring record — content type,
  prefab, pooling tier. A plain class, not a ScriptableObject.
- `FrameDefinition` (`[Serializable]`): adds `BlockOverlays`, an overlay-exemptions list,
  `AlwaysStackable` (always wins), `CloseOnBackdropPress`, and opening/closing animation presets.
  `CreateRuntime(...)` builds an in-memory instance for runtime registration.
- `ScreenDefinition` (`[Serializable]`): adds an optional `TransitionAnimation` preset.
  `CreateRuntime(...)` factory.
- `ScreenRouterConfig` (ScriptableObject): holds the inline `List<ScreenDefinition>` /
  `List<FrameDefinition>`, base sorting order + increment, pool idle timeout, max frame-queue size,
  and background-blur settings. Type-keyed lookups (`FindScreen` / `FindFrame`) are cached and rebuilt
  on demand (`InvalidateCaches`). Optional features are gated by **authored toggles** (e.g.
  `UseBackgroundBlur`), never inferred from a null reference.
- `SerializableType`: a Unity-serializable `System.Type` wrapper (assembly-qualified name, value
  equality) used by the definitions to reference a `ContentBase` subclass across serialization.

**Authoring in the inspector.** A custom `ScreenRouterConfigEditor` greys out the blur-tuning fields
when `UseBackgroundBlur` is off (presence is authored, never inferred from a null prefab). Each
definition's content-type field is drawn by `SerializableTypeDrawer`, which offers a dropdown of the
concrete `ContentBase` subclasses discovered in the loaded assemblies rather than a hand-typed name.

## Rendering

`IContentRenderer` exposes `SortingOrder`, `Alpha`, `Interactable`, `BlocksRaycasts`, `Initialize`,
and the three resting/transition states `SetStateOpened` / `SetStateClosed` / `SetStateAnimating`. The
renderer is discovered per content via `GetComponent` on the same GameObject in `ContentBase.DriveAwake`
(a missing one throws — fail-fast). Two backends ship:

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
`BlurClickForwarder`, closes the top frame when that frame opts in with `CloseOnBackdropPress`. The
overlay is built only when `ScreenRouterConfig.UseBackgroundBlur` is on; when on, the prefab must carry
both an `IContentRenderer` and a `BlurClickForwarder` (both validated fail-fast at construction).

## Extension points

Three seams let a consumer redirect, extend, or re-back the router without touching its internals.

### Guards

`RegisterGuard<T>(() => GuardDecision.Pass() / Block() / Reroute<U>())`, `UnregisterGuard<T>(guard)`,
`ClearGuards<T>()`. A guard votes on every open request for its type:

- `GuardDecision.Pass()` — let the request through.
- `GuardDecision.Block()` — veto; nothing opens.
- `GuardDecision.Reroute<U>()` / `Reroute(Type)` — swap the request for another content type.

Multiple guards for one type are **AND-ed**: the first non-pass vote short-circuits the chain
(`GuardRegistry.Vote`). A reroute **restarts resolution** against the destination type; a set of
visited types is tracked so a **reroute cycle** (A → B → A) is reported as `RedirectLoop` and logged
rather than looping forever. A **re-entrancy** flag (`_resolvingGuards`) means a guard delegate that
itself triggers a nav does not re-run guard checks, so a redirect target cannot recurse. A guard that
throws is treated as the caller's bug and propagates (fail-fast).

### Runtime type registration

`RegisterFrameType<T>(FrameDefinition)`, `UnregisterFrameType<T>()`, `RegisterScreenType<T>(ScreenDefinition)`,
`UnregisterScreenType<T>()`. Runtime registrations **shadow** the central config: `DefinitionRegistry<TDef>`
resolves a type against its runtime tier first and falls back to the config lookup. Build definitions
with `FrameDefinition.CreateRuntime(...)` / `ScreenDefinition.CreateRuntime(...)` (no asset on disk).
Unregistering destroys any live instance (parked, on-stack, or the active screen) before dropping the
registration.

### Swappable backends

- `IFrameTweenBackend` — replace `ManualFrameTweenBackend` with a target-bound tween library; the
  router only knows the contract, and a self-driving library leaves `Tick` a no-op.
- `IContentRenderer` — author a content prefab against either shipped backend or a custom one; the
  router touches visuals only through this interface, so uGUI and UI Toolkit stay interchangeable.

## File Structure

```
ScreenRouter/
├── README.md                  Thin landing page
├── MODULE.md                  This file
├── Runtime/
│   ├── PFound.ScreenRouter.asmdef
│   ├── Core/                  Engine-free state logic
│   │   ├── DefinitionRegistry.cs   Runtime-shadow-over-config lookup
│   │   ├── Enums.cs                PoolingType / FrameState / GuardVerdict
│   │   ├── FrameQueueModel.cs      De-duplicated bounded queue
│   │   ├── FrameStackModel.cs      Bottom-to-top frame stack
│   │   ├── GuardDecision.cs        Pass / Block / Reroute
│   │   ├── GuardRegistry.cs        AND-ing + reroute-cycle resolution
│   │   ├── PoolingPolicy.cs        Tier -> behaviour predicates
│   │   └── StackingRules.cs        Stacking-policy evaluation
│   ├── Content/               ContentBase, Screen(<TConfig>), Frame(<TConfig>)
│   ├── Rendering/             IContentRenderer + uGUI / UI Toolkit backends
│   ├── Definitions/           ContentDefinition, Frame/Screen defs, config, SerializableType
│   ├── Animation/             FrameAnimationPreset, IFrameTweenBackend, ManualFrameTweenBackend
│   └── Router/                ScreenRouter, ScreenRouterHost, BackgroundBlurOverlay, BlurClickForwarder
├── Editor/
│   ├── PFound.ScreenRouter.Editor.asmdef
│   ├── ScreenRouterConfigEditor.cs
│   └── SerializableTypeDrawer.cs
└── Tests/
    ├── PureCore/PureCoreTests.cs        csc/mono runner
    └── EditMode/StateLogicTests.cs      NUnit
```

## Downstream Dependents

None within PFound. The module is standalone (no PFound module references it, and it references no
PFound module); consumers are game projects that add a `ScreenRouterHost` and derive their own
`Screen`/`Frame` types.

## Testing

The engine-free state logic has a standalone csc/mono runner and an equivalent Unity EditMode suite:

```
csc -define:SCREENROUTER_PURE_TESTS -out:/tmp/sr.exe \
    Runtime/Core/*.cs Tests/PureCore/PureCoreTests.cs && mono /tmp/sr.exe
```

The MonoBehaviour/lifecycle layer is verified in the Unity editor on integration.

## Limitations / Known Gaps

- **Single process-wide `Instance`.** `Publish` is unconditional last-wins; overlapping hosts or
  routing during a scene-transition gap can point `Instance` at a torn-down router (see Setup / wiring).
- **Scene-scoped hosts disable cross-scene pooling.** `AppLifetime` and cross-scene reuse are inert
  without a persistent host subtree.
- **No declarative flow DSL.** A Flow/Runner DSL (multi-step nav flows with a back-stack and sub-flows)
  is a planned future addition and is not part of this release.
</content>
</invoke>
