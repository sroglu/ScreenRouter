using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// The presentation orchestrator: one active screen plus a stack of modal frames, with guards,
    /// runtime type registration, pooling, animation, and a background-blur surface. It is a plain
    /// class (not a MonoBehaviour); a <see cref="ScreenRouterHost"/> constructs it, publishes it on
    /// the static seam, and pumps <see cref="Tick"/> each frame. All ordering/stacking/guard/queue
    /// decisions defer to the engine-free helpers in the Core namespace.
    /// </summary>
    public sealed class ScreenRouter
    {
        // ------------------------------------------------------------------ static access seam
        private static ScreenRouter _instance;

        public static ScreenRouter Instance =>
            _instance ?? throw new InvalidOperationException(
                "No ScreenRouter is active. Add a ScreenRouterHost to the scene before routing.");

        public static bool HasInstance => _instance != null;

        public static bool TryGetInstance(out ScreenRouter router)
        {
            router = _instance;
            return router != null;
        }

        internal static void Publish(ScreenRouter router) => _instance = router;

        internal static void Withdraw(ScreenRouter router)
        {
            if (ReferenceEquals(_instance, router)) _instance = null;
        }

        // ------------------------------------------------------------------ collaborators & state
        private readonly ScreenRouterConfig _config;
        private readonly Transform _root;

        private readonly DefinitionRegistry<ScreenDefinition> _screens;
        private readonly DefinitionRegistry<FrameDefinition> _frames;
        private readonly GuardRegistry _guards = new GuardRegistry();

        private readonly FrameStackModel<Frame> _stack = new FrameStackModel<Frame>();
        private readonly FrameQueueModel<QueuedRequest> _queue;

        private readonly Dictionary<Type, ContentBase> _pool = new Dictionary<Type, ContentBase>();
        private readonly Dictionary<ContentBase, ContentDefinition> _defByInstance =
            new Dictionary<ContentBase, ContentDefinition>();
        private readonly Dictionary<ContentBase, float> _idleSince = new Dictionary<ContentBase, float>();

        private readonly IFrameTweenBackend _tween = new ManualFrameTweenBackend();
        private readonly BackgroundBlurOverlay _blur;

        // Mirrors the config's authored toggle; the router never infers blur presence from a null prefab.
        private readonly bool _useBlur;

        private readonly List<ContentBase> _scratch = new List<ContentBase>();
        private readonly List<Type> _typeScratch = new List<Type>();

        private Screen _activeScreen;
        private bool _resolvingGuards;

        private readonly struct QueuedRequest
        {
            public readonly Type Type;
            public readonly object Config;
            public QueuedRequest(Type type, object config) { Type = type; Config = config; }
        }

        // ------------------------------------------------------------------ construction
        public ScreenRouter(ScreenRouterConfig config, Transform root)
        {
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));

            _screens = new DefinitionRegistry<ScreenDefinition>(config.FindScreen);
            _frames = new DefinitionRegistry<FrameDefinition>(config.FindFrame);
            _queue = new FrameQueueModel<QueuedRequest>(config.MaxFrameQueue);

            // The blur surface is an authored optional: build it only when the designer enabled the
            // toggle. When enabled the prefab is trusted (validated at wire time on the config).
            _useBlur = config.UseBackgroundBlur;
            if (_useBlur)
            {
                _blur = new BackgroundBlurOverlay(
                    config.BackgroundBlurPrefab, root,
                    config.BlurBaseAlpha, config.BlurPerFrameAlpha, config.BlurMaxAlpha, config.BlurFadeDuration);
                _blur.BackgroundPressed += HandleBlurPressed;
            }
        }

        private static float Now => Time.unscaledTime;

        // ================================================================== screens
        public void SwitchScreen<T>() where T : Screen => SwitchScreen(typeof(T), null);

        public void SwitchScreen<T, TConfig>(TConfig config) where T : Screen =>
            SwitchScreen(typeof(T), config);

        public void SwitchScreen(Type screenType, object config = null)
        {
            if (screenType == null) throw new ArgumentNullException(nameof(screenType));
            if (!TryResolveGuarded(screenType, out var target)) return;

            // Close the outgoing screen atomically before the new one opens.
            CloseActiveScreenImmediate();

            var def = _screens.Resolve(target);
            var screen = (Screen)Acquire(target, def);
            if (config != null) screen.AssignConfig(config);

            screen.gameObject.SetActive(true);
            screen.DriveStartIfNeeded();
            _activeScreen = screen;

            screen.DriveOpening();
            screen.ApplySortingOrder(_config.BaseSortingOrder);

            var preset = def.TransitionAnimation;
            if (preset.Enabled && preset.Duration > 0f)
            {
                screen.Renderer.SetStateAnimating();
                _tween.Play(screen, preset, () => screen.DriveOpened());
            }
            else
            {
                screen.DriveOpened();
            }
        }

        public T GetActiveScreen<T>() where T : Screen => _activeScreen as T;

        public bool IsScreenOpen<T>() where T : Screen => _activeScreen is T;

        public void CloseActiveScreen() => CloseActiveScreenImmediate();

        private void CloseActiveScreenImmediate()
        {
            if (_activeScreen == null) return;
            var screen = _activeScreen;
            _activeScreen = null;
            _tween.Stop(screen);
            screen.DriveClosing();
            screen.DriveClosed();
            Release(screen);
        }

        // ================================================================== frames
        public bool HasActiveFrames => !_stack.IsEmpty;

        public T OpenFrame<T>() where T : Frame => OpenFrame(typeof(T), null) as T;

        public T OpenFrame<T, TConfig>(TConfig config) where T : Frame =>
            OpenFrame(typeof(T), config) as T;

        public Frame OpenFrame(Type frameType, object config = null)
        {
            if (frameType == null) throw new ArgumentNullException(nameof(frameType));
            if (!TryResolveGuarded(frameType, out var target)) return null;

            var def = _frames.Resolve(target);

            // Stacking gate: consult the current top's rules unless the incoming frame overrides.
            var top = _stack.Top;
            StackingRules? topRules = top != null ? GetFrameDefinition(top).ToStackingRules() : (StackingRules?)null;
            if (!StackingEvaluator.CanStack(topRules, target, def.AlwaysStackable))
                return null;

            // The frame currently on top slips into the background as the new one lands.
            if (top != null) top.DriveDemoted();

            var frame = (Frame)Acquire(target, def);
            if (config != null) frame.AssignConfig(config);

            frame.gameObject.SetActive(true);
            frame.DriveStartIfNeeded();

            _stack.PushTop(frame);
            frame.VisibilityState = FrameState.Opening;
            frame.DriveOpening();
            RecalculateSorting();
            RefreshBlur();

            var preset = def.OpeningAnimation;
            if (preset.Enabled && preset.Duration > 0f)
            {
                frame.Renderer.SetStateAnimating();
                _tween.Play(frame, preset, () =>
                {
                    frame.VisibilityState = FrameState.Opened;
                    frame.DriveOpened();
                });
            }
            else
            {
                frame.VisibilityState = FrameState.Opened;
                frame.DriveOpened();
            }
            return frame;
        }

        /// <summary>Closes the topmost frame. Returns false when the stack is empty.</summary>
        public bool CloseFrame()
        {
            var top = _stack.Top;
            return top != null && CloseFrame(top);
        }

        /// <summary>Closes a specific frame wherever it sits on the stack.</summary>
        public bool CloseFrame(Frame frame)
        {
            if (frame == null || !_stack.Contains(frame)) return false;

            bool wasTop = ReferenceEquals(_stack.Top, frame);
            _stack.Remove(frame);
            frame.VisibilityState = FrameState.Closing;
            frame.DriveClosing();

            void Finish()
            {
                frame.VisibilityState = FrameState.Closed;
                frame.DriveClosed();

                // A new frame is now on top; wake it before re-sorting and reclaiming the old one.
                if (wasTop && _stack.Top != null) _stack.Top.DrivePromoted();
                RecalculateSorting();
                Release(frame);
                RefreshBlur();
                PumpQueueIfIdle();
            }

            var preset = GetFrameDefinition(frame).ClosingAnimation;
            if (preset.Enabled && preset.Duration > 0f)
            {
                frame.Renderer.SetStateAnimating();
                _tween.Play(frame, preset, Finish);
            }
            else
            {
                Finish();
            }
            return true;
        }

        /// <summary>Instantly tears down every frame and empties the queue (no animations).</summary>
        public void CloseAllFrames()
        {
            _queue.Clear();
            while (!_stack.IsEmpty)
            {
                var frame = _stack.PopTop();
                _tween.Stop(frame);
                frame.VisibilityState = FrameState.Closing;
                frame.DriveClosing();
                frame.VisibilityState = FrameState.Closed;
                frame.DriveClosed();
                Release(frame);
            }
            RefreshBlur();
        }

        public bool QueueFrame<T>() where T : Frame => QueueFrame(typeof(T), null);

        public bool QueueFrame<T, TConfig>(TConfig config) where T : Frame =>
            QueueFrame(typeof(T), config);

        public bool QueueFrame(Type frameType, object config = null)
        {
            if (frameType == null) throw new ArgumentNullException(nameof(frameType));
            // Nothing on the stack? Honour the request now rather than making it wait.
            if (_stack.IsEmpty)
                return OpenFrame(frameType, config) != null;
            return _queue.Enqueue(frameType, new QueuedRequest(frameType, config));
        }

        public void ClearQueue() => _queue.Clear();

        public T GetActiveFrame<T>() where T : Frame =>
            _stack.FindTopDown(f => f is T) as T;

        public bool IsFrameOpen<T>() where T : Frame =>
            _stack.FindTopDown(f => f is T) != null;

        public bool CanOpenFrame<T>() where T : Frame
        {
            var res = _guards.Resolve(typeof(T));
            if (!res.Approved) return false;
            if (!_frames.TryResolve(res.Target, out var def)) return false;
            var top = _stack.Top;
            StackingRules? topRules = top != null ? GetFrameDefinition(top).ToStackingRules() : (StackingRules?)null;
            return StackingEvaluator.CanStack(topRules, res.Target, def.AlwaysStackable);
        }

        // ================================================================== guards
        public void RegisterGuard<T>(Func<GuardDecision> guard) => _guards.Add(typeof(T), guard);

        public bool UnregisterGuard<T>(Func<GuardDecision> guard) => _guards.Remove(typeof(T), guard);

        public void ClearGuards<T>() => _guards.RemoveAll(typeof(T));

        private bool TryResolveGuarded(Type requested, out Type target)
        {
            target = requested;

            // Re-entrant open from inside a guard delegate: skip a fresh guard pass so a redirect
            // target is not itself re-guarded (and cannot recurse).
            if (_resolvingGuards) return true;

            // Raise the flag only across the resolution call. A guard delegate that throws is the
            // caller's bug and is allowed to propagate (fail-fast) rather than being swallowed.
            _resolvingGuards = true;
            var res = _guards.Resolve(requested);
            _resolvingGuards = false;

            if (res.Kind == GuardResolutionKind.RedirectLoop)
            {
                Debug.LogError($"[ScreenRouter] Guard redirect loop detected at '{res.Target}'.");
                return false;
            }
            if (!res.Approved) return false;
            target = res.Target;
            return true;
        }

        // ================================================================== runtime type registration
        public void RegisterFrameType<T>(FrameDefinition definition) where T : Frame =>
            _frames.Register(typeof(T), definition);

        public bool UnregisterFrameType<T>() where T : Frame
        {
            DestroyLiveInstances(typeof(T));
            return _frames.Unregister(typeof(T));
        }

        public void RegisterScreenType<T>(ScreenDefinition definition) where T : Screen =>
            _screens.Register(typeof(T), definition);

        public bool UnregisterScreenType<T>() where T : Screen
        {
            DestroyLiveInstances(typeof(T));
            return _screens.Unregister(typeof(T));
        }

        // ================================================================== host-facing pump
        internal void Tick(float deltaTime)
        {
            _tween.Tick(deltaTime);
            if (_useBlur) _blur.Tick(deltaTime);
            ReclaimIdle();
        }

        /// <summary>Discards pooled instances that must not survive a scene change.</summary>
        public void HandleSceneChanged()
        {
            _typeScratch.Clear();
            foreach (var kvp in _pool)
            {
                var pooling = GetDefinition(kvp.Value).Pooling;
                if (PoolingPolicy.DropOnSceneChange(pooling)) _typeScratch.Add(kvp.Key);
            }
            foreach (var key in _typeScratch)
            {
                if (_pool.TryGetValue(key, out var content))
                {
                    _pool.Remove(key);
                    _idleSince.Remove(content);
                    _defByInstance.Remove(content);
                    if (content != null) UnityEngine.Object.Destroy(content.gameObject);
                }
            }
        }

        internal void Shutdown()
        {
            _tween.StopAll();
            if (_useBlur) _blur.Dispose();
            Withdraw(this);
        }

        // ================================================================== internals
        private ContentBase Acquire(Type type, ContentDefinition def)
        {
            // Reuse a parked instance when the pooling tier allows it.
            if (PoolingPolicy.CanRevive(def.Pooling) &&
                _pool.TryGetValue(type, out var cached) && cached != null)
            {
                _pool.Remove(type);
                _idleSince.Remove(cached);
                cached.gameObject.SetActive(true);
                cached.InvalidateConfig();   // stale typed config must not leak into the new cycle
                cached.RefreshClosedToken();
                _defByInstance[cached] = def;
                return cached;
            }

            var go = UnityEngine.Object.Instantiate(def.Prefab, _root);
            var content = go.GetComponent(type) as ContentBase;
            if (content == null)
            {
                UnityEngine.Object.Destroy(go);
                throw new InvalidOperationException(
                    $"Prefab for '{type}' does not carry a matching {nameof(ContentBase)} component.");
            }
            content.DriveAwake();          // idempotent; Unity's Awake already ran it on Instantiate
            content.RefreshClosedToken();
            _defByInstance[content] = def;
            return content;
        }

        private void Release(ContentBase content)
        {
            var pooling = GetDefinition(content).Pooling;
            if (PoolingPolicy.DestroyOnClose(pooling))
            {
                _defByInstance.Remove(content);
                if (content != null) UnityEngine.Object.Destroy(content.gameObject);
                return;
            }

            content.gameObject.SetActive(false);
            _pool[content.GetType()] = content;
            if (PoolingPolicy.ReclaimWhenIdle(pooling))
                _idleSince[content] = Now;
        }

        private void ReclaimIdle()
        {
            if (_idleSince.Count == 0) return;
            float timeout = _config.PoolIdleTimeoutSeconds;
            float now = Now;

            _scratch.Clear();
            foreach (var kvp in _idleSince)
                if (now - kvp.Value >= timeout) _scratch.Add(kvp.Key);

            for (int i = 0; i < _scratch.Count; i++)
            {
                var content = _scratch[i];
                _idleSince.Remove(content);
                if (content != null && ReferenceEquals(GetPooled(content.GetType()), content))
                    _pool.Remove(content.GetType());
                _defByInstance.Remove(content);
                if (content != null) UnityEngine.Object.Destroy(content.gameObject);
            }
        }

        private ContentBase GetPooled(Type type) =>
            _pool.TryGetValue(type, out var c) ? c : null;

        private void PumpQueueIfIdle()
        {
            if (!_stack.IsEmpty) return;
            if (_queue.TryDequeue(out _, out var request))
                OpenFrame(request.Type, request.Config);
        }

        private void RecalculateSorting()
        {
            int order = _config.BaseSortingOrder + _config.SortingIncrement;
            foreach (var frame in _stack.BottomToTop())
            {
                frame.ApplySortingOrder(order);
                order += _config.SortingIncrement;
            }
        }

        private void RefreshBlur()
        {
            if (!_useBlur) return;
            int count = _stack.Count;
            int topOrder = _config.BaseSortingOrder + _config.SortingIncrement * count;
            _blur.Refresh(count, topOrder - 1);
        }

        private void HandleBlurPressed()
        {
            var top = _stack.Top;
            if (top == null) return;
            if (GetFrameDefinition(top).CloseOnBackdropPress)
                top.OnCloseButtonClicked();
        }

        private void DestroyLiveInstances(Type type)
        {
            // Parked instance.
            if (_pool.TryGetValue(type, out var cached) && cached != null)
            {
                _pool.Remove(type);
                _idleSince.Remove(cached);
                _defByInstance.Remove(cached);
                UnityEngine.Object.Destroy(cached.gameObject);
            }

            // Any live copies on the stack.
            Frame onStack;
            while ((onStack = _stack.FindTopDown(f => f.GetType() == type)) != null)
            {
                _stack.Remove(onStack);
                _tween.Stop(onStack);
                _defByInstance.Remove(onStack);
                UnityEngine.Object.Destroy(onStack.gameObject);
            }

            // The active screen.
            if (_activeScreen != null && _activeScreen.GetType() == type)
            {
                _tween.Stop(_activeScreen);
                _defByInstance.Remove(_activeScreen);
                UnityEngine.Object.Destroy(_activeScreen.gameObject);
                _activeScreen = null;
            }

            RecalculateSorting();
            RefreshBlur();
        }

        // Every acquired instance is recorded in _defByInstance, so a live screen/frame always has
        // a definition. Direct indexing means a missing entry surfaces as the bug it is.
        private ContentDefinition GetDefinition(ContentBase content) => _defByInstance[content];

        private FrameDefinition GetFrameDefinition(Frame frame) => (FrameDefinition)_defByInstance[frame];
    }
}
