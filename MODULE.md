# ScreenRouter

> **Module group — UI & Presentation.** Sibling modules in this group: `UISystem`, `TweenPresetLibrary`, `GuidedOnboardingFlow`, `MVC`. Grouped by purpose — see the catalog `Assets/PFound/README.md` and each module's **Dependencies** for exact edges.

## Purpose

A UI presentation router for Unity: one active **screen** plus a stack of modal **frames**, with
guards, runtime type registration, four pooling tiers, animated transitions, and a background-blur
surface. The view *is* the content — a content prefab carries both a `ContentBase` MonoBehaviour and
an `IContentRenderer` component; there is no separate view instantiation.

## Scope boundary — the app navigator (vs UISystem, MVC)

ScreenRouter is THE app-wide navigation / presentation system: it owns *which* screen and modal
frames are on-stack and their transition lifecycle (guards, pooling, animation, blur). It composes
with, but does not overlap:
- **`PFound.UISystem`** — the visual widgets/theme rendered *inside* a screen's content prefab.
  ScreenRouter decides which screen shows; UISystem is what it looks like. Used together.
- **`PFound.MVC`** — a per-screen view↔model logic pattern. It is NOT the app navigator: its
  `ViewManager` page-stack is a separate, non-interoperable mechanism, has no production consumer, and
  is superseded by Toolbox's `mvp` + `viewmanager` packages (slated for retirement — see the MVC
  module note). For navigation, use ScreenRouter.

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
- `ScreenRouterNavigationSink`, `NavigationFlowHost` — the Unity seam that binds the engine-free
  flow layer to the live `ScreenRouter.Instance` (see [Declarative NavigationFlow](#declarative-navigationflow)).

**Flow (`Runtime/Flow`, engine-free)**
- `NavigationFlow` / `NavigationFlow<TAction>` — static builder entry point + the immutable step graph.
- `NavigationFlowBuilder<TAction>` (+ nested `StepChain`) — fluent, cycle-checked graph builder.
- `FlowStep<TAction>`, `FlowTransition<TAction>` — graph nodes + action-keyed edges.
- `NavigationFlowRunner<TAction>` — drives a flow; scope stack of per-scope back-stacks.
- `INavigationSink` — the engine-free seam the runner drives (router-backed in production).
- `IFlowTarget` — lambda selector for naming an edge's destination step.

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
  host the pooling tiers pay off: on each scene change the router keeps `KeepAlways` content and
  drops the rest, so cross-scene shell UI (top bar, toast/loading layer) survives while per-scene UI
  is cleaned up automatically. Use one shared `ScreenRouterConfig` app-wide.

- **Scene-scoped host (single-scene apps, or teams that don't need cross-scene UI).** Place a host +
  Canvas + content root in each scene. Simpler, no `DontDestroyOnLoad`, but `KeepAlways` and all
  cross-scene pooling become inert (everything dies with its scene), and you must ensure only one host
  is alive at a time — the static `Instance` is process-wide and `Publish` is unconditional last-wins,
  so overlapping hosts (or routing during a scene-transition gap) can leave `Instance` pointing at a
  torn-down router. Single-scene routing itself works fully.

Rule of thumb: if any UI must outlive a scene load, use a persistent host and mark that UI
`KeepAlways`; give per-scene UI `KeepForScene`/`DestroyOnClose` so it is dropped on each swap.

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

| Tier             | On close            | Idle reaper | Scene change | Meaning                                                              |
|------------------|---------------------|-------------|--------------|----------------------------------------------------------------------|
| `DestroyOnClose` | destroyed           | n/a         | n/a          | The instance is destroyed as soon as it finishes closing.            |
| `KeepAndReuse`   | parked for reuse    | reclaimed   | discarded    | Kept for reuse; destroyed only if it stays idle past the grace time. |
| `KeepForScene`   | parked for reuse    | no          | discarded    | Kept and reused while the scene is loaded; destroyed on scene change.|
| `KeepAlways`     | parked for reuse    | no          | survives     | Kept and reused for the whole run of the app; never auto-destroyed.  |

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

## Declarative NavigationFlow

The router's public API (`SwitchScreen` / `OpenFrame`) is imperative and stateless — it has no notion
of history, so there is no built-in *back*. `Runtime/Flow` adds a declarative layer **over** the
router (it never modifies the router core): an immutable, action-keyed step graph plus a runner that
translates actions into router calls and tracks the history the router does not.

The layer is **engine-free**. The runner drives an `INavigationSink` (`SwitchScreen(Type)` /
`OpenFrame(Type)`), never the router directly, so the whole graph + runner compiles and runs under
plain csc/mono. The only Unity-touching type is the thin seam `ScreenRouterNavigationSink`, which
forwards to `ScreenRouter.Instance`; `NavigationFlowHost.CreateRunner(flow)` wires a runner to it in
one call.

**The graph.** `NavigationFlow.Define<TAction>(name)` (an enum `TAction` gives compile-time-safe
action keys; `Define(name)` uses `string` keys) returns a fluent `NavigationFlowBuilder`. Nodes are
screen/frame content types added with `Screen<T>()` / `Frame<T>()`; edges are action keys mapped with
`OnAction`:

- `OnAction(action, t => t.Screen<U>() / t.Frame<U>())` — an edge to a sibling step (forward
  references are allowed; the target node is auto-created at build).
- `OnAction(action, subBuilder / subChain / builtFlow)` — an edge that **enters a sub-flow**.

`Build()` resolves the whole builder graph into shared, immutable `NavigationFlow` instances and
validates it: a **circular sub-flow reference** (a flow reachable from itself through sub-flow
entries) throws `InvalidOperationException`. Diamonds (one sub-flow reached by two parents) are shared,
not rejected; intra-flow action cycles (`A --go--> B --go--> A`) are legal navigation, not build
cycles.

**The runner.** `NavigationFlowRunner<TAction>` holds a **stack of scopes**, one per active flow
level, each with its own back-stack:

- `Start()` — enters the root flow at its entry step and routes to it. Throws if started twice.
- `Execute(action)` — from the current step, either navigates to the edge's target (pushing the
  current step onto this level's back-stack) or enters a sub-flow (a new scope). Returns `false` when
  the current step has no edge for the action.
- `Back()` — the recovered capability. Unwinds this level's back-stack first; when it is empty and the
  runner is inside a sub-flow, exits to the parent level and re-routes to its current step. Returns
  `false` at the root entry with nothing left to unwind.
- `Stop()` — clears every level (can be started again).
- `Depth` — how deep in sub-flows the runner is (`0` = root). Also exposes `IsRunning`, `ActiveFlow`,
  `CurrentStep`.

Misuse fails fast: a null flow or sink throws in the constructor; `Execute` / `Back` before `Start`
throw. "Action not found on the current step" and "already at the root entry" are legitimate
control-flow results (`false`), not errors.

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
│   ├── Flow/                  Engine-free NavigationFlow layer
│   │   ├── INavigationSink.cs         Runner -> router seam (engine-free)
│   │   ├── FlowStep.cs                Graph nodes + action-keyed transitions
│   │   ├── NavigationFlow.cs          Immutable step graph + static builder entry
│   │   ├── NavigationFlowBuilder.cs   Fluent, cycle-checked builder + StepChain
│   │   ├── FlowScope.cs               Per-level current step + back-stack
│   │   └── NavigationFlowRunner.cs    Start/Execute/Back/Stop/Depth over the scope stack
│   └── Router/                ScreenRouter, ScreenRouterHost, BackgroundBlurOverlay,
│                              BlurClickForwarder, ScreenRouterNavigationSink (Unity flow seam)
├── Editor/
│   ├── PFound.ScreenRouter.Editor.asmdef
│   ├── ScreenRouterConfigEditor.cs
│   └── SerializableTypeDrawer.cs
└── Tests/
    ├── PureCore/PureCoreTests.cs        csc/mono runner (state core)
    ├── PureCore/FlowTests.cs            csc/mono runner (NavigationFlow)
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

The engine-free NavigationFlow layer has its own standalone runner (graph builder, runner back-stack,
sub-flows, and cycle detection). The Unity flow seam (`ScreenRouterNavigationSink`) lives under
`Runtime/Router` and is deliberately excluded from the glob, so the compile stays engine-free:

```
csc -define:SCREENROUTER_FLOW_TESTS -out:/tmp/sr_flow.exe \
    Runtime/Flow/*.cs Tests/PureCore/FlowTests.cs && mono /tmp/sr_flow.exe
```

The MonoBehaviour/lifecycle layer is verified in the Unity editor on integration.

## Limitations / Known Gaps

- **Single process-wide `Instance`.** `Publish` is unconditional last-wins; overlapping hosts or
  routing during a scene-transition gap can point `Instance` at a torn-down router (see Setup / wiring).
- **Scene-scoped hosts disable cross-scene pooling.** `KeepAlways` and cross-scene reuse are inert
  without a persistent host subtree.
- **NavigationFlow content types are not statically constrained to `Screen`/`Frame`.** To keep the
  flow core engine-free (mono-testable), the builder's `Screen<T>()` / `Frame<T>()` use a `where T :
  class` constraint rather than `where T : Screen` / `where T : Frame`; screen-vs-frame is chosen by
  which method you call, and the router validates the concrete type at open time (fail-fast).
