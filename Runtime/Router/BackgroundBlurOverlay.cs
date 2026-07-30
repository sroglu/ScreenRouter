using System;
using UnityEngine;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// Manages the single dim/blur surface shown behind the frame stack. Its target alpha deepens
    /// with stack depth on a saturating curve that eases toward a ceiling, it sorts just under the
    /// top frame, and it eases toward the target each tick. A press on the surface is forwarded to
    /// <see cref="BackgroundPressed"/> so the router can honour the top frame's
    /// close-on-background-click preference. The overlay hides once the stack empties.
    ///
    /// The instance is created up front in the constructor (the router only builds an overlay when
    /// the config's blur toggle is on), so there is no lazy null-init. The blur prefab is the
    /// module's own asset and must carry both a renderer and a click forwarder; both are required
    /// (validated fail-fast at construction), not treated as optional.
    /// </summary>
    public sealed class BackgroundBlurOverlay
    {
        private readonly float _baseAlpha;
        private readonly float _perFrameAlpha;
        private readonly float _maxAlpha;
        private readonly float _fadeDuration;

        private readonly GameObject _instance;
        private readonly IContentRenderer _renderer;
        private readonly BlurClickForwarder _forwarder;

        private float _currentAlpha;
        private float _targetAlpha;
        private bool _visible;

        /// <summary>Raised when the blur surface is pressed while visible.</summary>
        public event Action BackgroundPressed;

        public BackgroundBlurOverlay(
            GameObject prefab, Transform root,
            float baseAlpha, float perFrameAlpha, float maxAlpha, float fadeDuration)
        {
            _baseAlpha = baseAlpha;
            _perFrameAlpha = perFrameAlpha;
            _maxAlpha = maxAlpha;
            _fadeDuration = Mathf.Max(0f, fadeDuration);

            _instance = UnityEngine.Object.Instantiate(prefab, root);
            _renderer = _instance.GetComponent<IContentRenderer>();
            if (_renderer == null)
            {
                throw new InvalidOperationException(
                    $"Background-blur prefab must carry a component implementing {nameof(IContentRenderer)}.");
            }
            _renderer.Initialize(_instance);
            _renderer.SetStateOpened();
            _renderer.Alpha = 0f;
            _currentAlpha = 0f;

            // The forwarder is required on the module's own blur prefab; report a missing one
            // precisely rather than silently dropping background-click support.
            _forwarder = _instance.GetComponent<BlurClickForwarder>();
            if (_forwarder == null)
            {
                throw new InvalidOperationException(
                    $"Background-blur prefab must carry a {nameof(BlurClickForwarder)} component.");
            }
            _forwarder.Pressed += HandlePressed;

            _instance.SetActive(false);
        }

        /// <summary>
        /// Recomputes visibility, target alpha, and sort order for the current stack. Call whenever
        /// the stack changes.
        /// </summary>
        public void Refresh(int frameCount, int sortingOrderJustBelowTop)
        {
            if (frameCount <= 0)
            {
                _visible = false;
                _targetAlpha = 0f;
                return;
            }

            _visible = true;
            _instance.SetActive(true);
            _renderer.SortingOrder = sortingOrderJustBelowTop;

            // Saturating dim: the first frame shows the base dim, then every additional frame in the
            // stack closes a fixed fraction (_perFrameAlpha) of whatever gap still remains up to the
            // ceiling. Because each step only ever moves a fraction of the remaining distance, the
            // value rises with depth yet approaches — never overshoots — the ceiling, so no explicit
            // clamp is required.
            float dim = _baseAlpha;
            for (int covered = 1; covered < frameCount; covered++)
                dim += (_maxAlpha - dim) * _perFrameAlpha;
            _targetAlpha = dim;
        }

        /// <summary>Eases the surface alpha toward its target; deactivates once fully faded out.</summary>
        public void Tick(float deltaTime)
        {
            // Frame-rate-independent exponential smoothing: each tick covers a proportion of the
            // remaining distance to the target set by how far deltaTime has advanced through the
            // fade window. A zero-length window snaps straight to the target.
            float blend = _fadeDuration <= 0f ? 1f : 1f - Mathf.Exp(-deltaTime / _fadeDuration);
            _currentAlpha += (_targetAlpha - _currentAlpha) * blend;
            _renderer.Alpha = _currentAlpha;

            if (!_visible && _currentAlpha <= 0.001f && _instance.activeSelf)
                _instance.SetActive(false);
        }

        public void Dispose()
        {
            _forwarder.Pressed -= HandlePressed;
            UnityEngine.Object.Destroy(_instance);
        }

        private void HandlePressed()
        {
            if (_visible) BackgroundPressed?.Invoke();
        }
    }
}
