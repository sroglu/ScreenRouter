using System;
using System.Threading;
using UnityEngine;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// Shared MonoBehaviour base for everything the router shows. It owns the renderer handle, a
    /// cancellation token that trips on close, and the sealed drivers the router calls to move the
    /// content through its lifecycle. Games override the protected On* hooks; they never call the
    /// Drive* methods, which are internal to the router.
    ///
    /// Hook order for a full open/close round-trip:
    ///   OnAwake (once) -> OnStart (once) -> OnOpening -> OnOpen
    ///   -> [OnDemoted / OnPromoted while other content stacks over it]
    ///   -> OnClosing -> OnClose -> OnDestroyed (only when actually torn down).
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class ContentBase : MonoBehaviour
    {
        private IContentRenderer _renderer;
        private CancellationTokenSource _closedCts;
        private bool _started;
        private bool _awoken;

        private object _config;
        private bool _hasConfig;

        /// <summary>The presentation surface discovered on this prefab at Awake.</summary>
        public IContentRenderer Renderer => _renderer;

        /// <summary>Trips the moment this content closes; refreshed on every (re)open. Valid only
        /// after Awake has run (the source is created deterministically there).</summary>
        public CancellationToken ClosedToken => _closedCts.Token;

        // ------------------------------------------------------------------ overridable hooks
        protected virtual void OnAwake() { }
        protected virtual void OnStart() { }
        protected virtual void OnOpening() { }
        protected virtual void OnOpen() { }
        protected virtual void OnClosing() { }
        protected virtual void OnClose() { }
        protected virtual void OnDemoted() { }
        protected virtual void OnPromoted() { }
        protected virtual void OnDestroyed() { }

        // ------------------------------------------------------------------ config plumbing
        /// <summary>True once a typed config has been supplied for the current open cycle.</summary>
        protected bool HasConfig => _hasConfig;

        /// <summary>The boxed config; generic subclasses cast this. Throws if unset.</summary>
        protected object BoxedConfig
        {
            get
            {
                if (!_hasConfig)
                    throw new InvalidOperationException(
                        $"{GetType().Name} has no config assigned for this open cycle.");
                return _config;
            }
        }

        internal void AssignConfig(object config)
        {
            _config = config;
            _hasConfig = true;
        }

        internal void InvalidateConfig()
        {
            _config = null;
            _hasConfig = false;
        }

        // ------------------------------------------------------------------ Unity messages
        private void Awake() => DriveAwake();

        private void OnDestroy()
        {
            OnDestroyed();
            _closedCts.Cancel();
            _closedCts.Dispose();
        }

        // ------------------------------------------------------------------ router-facing drivers
        internal void DriveAwake()
        {
            if (_awoken) return;
            _awoken = true;

            // GetComponent is a Unity boundary that legitimately returns null when the prefab was
            // authored without a renderer; report that precisely rather than NRE-ing later.
            _renderer = GetComponent<IContentRenderer>();
            if (_renderer == null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} needs a component implementing {nameof(IContentRenderer)} on the same prefab.");
            }
            _renderer.Initialize(gameObject);
            _closedCts = new CancellationTokenSource();
            OnAwake();
        }

        internal void DriveStartIfNeeded()
        {
            if (_started) return;
            _started = true;
            OnStart();
        }

        /// <summary>Rearms the closed token for a fresh open cycle (used on pooled reuse too).</summary>
        internal void RefreshClosedToken()
        {
            _closedCts.Dispose();
            _closedCts = new CancellationTokenSource();
        }

        internal void DriveOpening() => OnOpening();

        internal void DriveOpened()
        {
            _renderer.SetStateOpened();
            OnOpen();
        }

        internal void DriveClosing() => OnClosing();

        internal void DriveClosed()
        {
            _renderer.SetStateClosed();
            OnClose();
            _closedCts.Cancel();
        }

        internal void DriveDemoted() => OnDemoted();

        internal void DrivePromoted() => OnPromoted();

        internal void ApplySortingOrder(int order) => _renderer.SortingOrder = order;
    }
}
