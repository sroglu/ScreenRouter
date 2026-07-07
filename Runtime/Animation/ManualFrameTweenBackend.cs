using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// Dependency-free tween backend: it lerps scale and alpha over a preset's duration, advanced
    /// by the host through <see cref="Tick"/>. Lifetime is target-bound — <see cref="Play"/> drops
    /// any prior transition on the same target before starting (the "stop-first" re-entrancy rule),
    /// and a destroyed target is dropped silently without ever firing its callback. Completion
    /// callbacks run after iteration so a callback may safely start a new transition.
    /// </summary>
    public sealed class ManualFrameTweenBackend : IFrameTweenBackend
    {
        private sealed class Running
        {
            public ContentBase Target;
            public FrameAnimationPreset Preset;
            public float Elapsed;
            public Action OnComplete;
        }

        private readonly Dictionary<ContentBase, Running> _running = new Dictionary<ContentBase, Running>();
        private readonly List<ContentBase> _completed = new List<ContentBase>();

        public bool IsPlaying(ContentBase target) => target != null && _running.ContainsKey(target);

        public void Play(ContentBase target, FrameAnimationPreset preset, Action onComplete)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            // Stop-first: cancel any in-flight transition on this target (no callback).
            _running.Remove(target);

            if (!preset.Enabled || preset.Duration <= 0f)
            {
                ApplyPose(target, preset, 1f);
                onComplete?.Invoke();
                return;
            }

            target.Renderer.SetStateAnimating();
            ApplyPose(target, preset, 0f);

            _running[target] = new Running
            {
                Target = target,
                Preset = preset,
                Elapsed = 0f,
                OnComplete = onComplete,
            };
        }

        public void Stop(ContentBase target)
        {
            if (target != null) _running.Remove(target);
        }

        public void StopAll() => _running.Clear();

        public void Tick(float deltaTime)
        {
            if (_running.Count == 0) return;

            _completed.Clear();
            foreach (var kvp in _running)
            {
                var run = kvp.Value;

                // A destroyed Unity object compares equal to null: drop it without a callback.
                if (run.Target == null)
                {
                    _completed.Add(kvp.Key);
                    continue;
                }

                run.Elapsed += deltaTime;
                float t = Mathf.Clamp01(run.Elapsed / run.Preset.Duration);
                ApplyPose(run.Target, run.Preset, t);

                if (t >= 1f) _completed.Add(kvp.Key);
            }

            // Resolve completions after iterating so a callback may restart a transition safely.
            for (int i = 0; i < _completed.Count; i++)
            {
                var key = _completed[i];
                if (_running.TryGetValue(key, out var run))
                {
                    _running.Remove(key);
                    if (key != null) run.OnComplete?.Invoke();
                }
            }
        }

        private static void ApplyPose(ContentBase target, FrameAnimationPreset preset, float t)
        {
            target.transform.localScale = preset.EvaluateScale(t);
            target.Renderer.Alpha = preset.EvaluateAlpha(t);
        }
    }
}
