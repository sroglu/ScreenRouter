using System;
using System.Collections.Generic;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// In-scene navigation router: one active full-screen <see cref="ContentBase"/> plus a stack of
    /// modal frames on top of it. Switching the screen closes any open frames first; opening a frame
    /// backgrounds the current top and foregrounds it again on close. Guards can cancel or redirect a
    /// navigation. Content realization is delegated to an <see cref="IContentRenderer"/> so the router
    /// itself is engine-independent. Synchronous transitions; animated/async transitions are a
    /// renderer-layer concern. Main-thread only.
    /// </summary>
    public sealed class ScreenRouter
    {
        private readonly IContentRenderer _renderer;
        private readonly List<ContentBase> _frames = new List<ContentBase>();
        private readonly Dictionary<Type, List<Func<NavigationGuardResult>>> _guards = new Dictionary<Type, List<Func<NavigationGuardResult>>>();
        private readonly Queue<Type> _frameQueue = new Queue<Type>();
        private ContentBase _screen;
        private bool _draining;

        public ScreenRouter(IContentRenderer renderer)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        }

        /// <summary>The active screen as <typeparamref name="T"/>, or null.</summary>
        public T GetActiveScreen<T>() where T : ContentBase => _screen as T;

        /// <summary>Number of stacked modal frames above the screen.</summary>
        public int FrameCount => _frames.Count;

        /// <summary>True if <typeparamref name="T"/> is the active screen or any open frame.</summary>
        public bool IsScreenOpen<T>()
        {
            if (_screen is T) return true;
            for (int i = 0; i < _frames.Count; i++) if (_frames[i] is T) return true;
            return false;
        }

        /// <summary>Registers a guard evaluated before navigating to <typeparamref name="T"/>.</summary>
        public void RegisterGuard<T>(Func<NavigationGuardResult> guard) where T : ContentBase
        {
            if (guard == null) throw new ArgumentNullException(nameof(guard));
            if (!_guards.TryGetValue(typeof(T), out var list))
            {
                list = new List<Func<NavigationGuardResult>>();
                _guards[typeof(T)] = list;
            }
            list.Add(guard);
        }

        public void SwitchScreen<T>() where T : ContentBase => SwitchScreenInternal(typeof(T), null);

        public void SwitchScreen<T, TConfig>(TConfig config) where T : ContentBase =>
            SwitchScreenInternal(typeof(T), c => (c as IConfigurable<TConfig>)?.Configure(config));

        public void OpenFrame<T>() where T : ContentBase => OpenFrameInternal(typeof(T), null);

        public void OpenFrame<T, TConfig>(TConfig config) where T : ContentBase =>
            OpenFrameInternal(typeof(T), c => (c as IConfigurable<TConfig>)?.Configure(config));

        /// <summary>Opens a frame after the current transition (queued FIFO).</summary>
        public void QueueFrame<T>() where T : ContentBase
        {
            _frameQueue.Enqueue(typeof(T));
            DrainQueue();
        }

        public void CloseFrame()
        {
            if (_frames.Count == 0) return;
            CloseTopFrame();
            Top()?.RaiseBecomeForeground();
            DrainQueue();
        }

        private void SwitchScreenInternal(Type type, Action<ContentBase> configure)
        {
            if (!Resolve(ref type, ref configure)) return;
            while (_frames.Count > 0) CloseTopFrame();
            if (_screen != null) Close(_screen);
            _screen = Open(type, configure);
        }

        private void OpenFrameInternal(Type type, Action<ContentBase> configure)
        {
            if (!Resolve(ref type, ref configure)) return;
            Top()?.RaiseBecomeBackground();
            _frames.Add(Open(type, configure));
        }

        // Runs guards, following redirects (config is dropped when redirected). Returns false if cancelled.
        private bool Resolve(ref Type type, ref Action<ContentBase> configure)
        {
            var visited = new HashSet<Type>();
            while (true)
            {
                if (!visited.Add(type))
                    throw new InvalidOperationException("Guard redirect loop while navigating to " + type.FullName + ".");
                var result = RunGuards(type);
                if (result.Outcome == NavigationGuardOutcome.Allow) return true;
                if (result.Outcome == NavigationGuardOutcome.Cancel) return false;
                type = result.RedirectTo; // Redirect
                configure = null;
            }
        }

        private NavigationGuardResult RunGuards(Type type)
        {
            if (_guards.TryGetValue(type, out var list))
                for (int i = 0; i < list.Count; i++)
                {
                    var result = list[i]();
                    if (result.Outcome != NavigationGuardOutcome.Allow) return result;
                }
            return NavigationGuardResult.Allow;
        }

        private ContentBase Open(Type type, Action<ContentBase> configure)
        {
            var content = _renderer.Create(type);
            configure?.Invoke(content);
            content.RaiseOpening();
            _renderer.Show(content);
            content.RaiseOpen();
            return content;
        }

        private void Close(ContentBase content)
        {
            content.RaiseClosing();
            _renderer.Hide(content);
            _renderer.Destroy(content);
            content.RaiseClose();
        }

        private void CloseTopFrame()
        {
            int last = _frames.Count - 1;
            var frame = _frames[last];
            _frames.RemoveAt(last);
            Close(frame);
        }

        private ContentBase Top() => _frames.Count > 0 ? _frames[_frames.Count - 1] : _screen;

        private void DrainQueue()
        {
            if (_draining) return;
            _draining = true;
            try
            {
                while (_frameQueue.Count > 0)
                    OpenFrameInternal(_frameQueue.Dequeue(), null);
            }
            finally
            {
                _draining = false;
            }
        }
    }
}
