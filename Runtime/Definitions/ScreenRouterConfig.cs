using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// The project-wide router configuration asset. It holds the authored screen/frame
    /// definitions, sorting and pooling knobs, the max frame-queue size, and the background-blur
    /// settings. Type-keyed lookups are cached lazily and rebuilt on demand.
    /// </summary>
    [CreateAssetMenu(menuName = "PFound/ScreenRouter/Router Config", fileName = "ScreenRouterConfig")]
    public sealed class ScreenRouterConfig : ScriptableObject
    {
        [Header("Definitions")]
        [SerializeField] private List<ScreenDefinition> _screens = new List<ScreenDefinition>();
        [SerializeField] private List<FrameDefinition> _frames = new List<FrameDefinition>();

        [Header("Sorting")]
        [SerializeField] private int _baseSortingOrder = 100;
        [SerializeField] private int _sortingIncrement = 10;

        [Header("Pooling")]
        [Min(0f)]
        [SerializeField]
        [Tooltip("Seconds a Recyclable-pooled instance may sit idle before it is reclaimed.")]
        private float _poolIdleTimeoutSeconds = 30f;

        [Header("Frame queue")]
        [Min(0)]
        [SerializeField]
        [Tooltip("Max frames that may wait in the queue (0 = unbounded).")]
        private int _maxFrameQueue = 8;

        [Header("Background blur")]
        [SerializeField]
        [Tooltip("Authored toggle for the dim/blur surface. When off, no blur is built and the " +
                 "prefab slot below is greyed out by the config editor. Presence is authored here, " +
                 "never inferred from the prefab being null.")]
        private bool _useBackgroundBlur = true;

        [SerializeField] private GameObject _backgroundBlurPrefab;
        [Range(0f, 1f)] [SerializeField] private float _blurBaseAlpha = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float _blurPerFrameAlpha = 0.1f;
        [Range(0f, 1f)] [SerializeField] private float _blurMaxAlpha = 0.85f;
        [Min(0f)] [SerializeField] private float _blurFadeDuration = 0.15f;

        // ---- knobs ----
        public int BaseSortingOrder => _baseSortingOrder;
        public int SortingIncrement => _sortingIncrement;
        public float PoolIdleTimeoutSeconds => _poolIdleTimeoutSeconds;
        public int MaxFrameQueue => _maxFrameQueue;

        /// <summary>Authored flag deciding whether the router builds a background-blur surface.</summary>
        public bool UseBackgroundBlur => _useBackgroundBlur;

        /// <summary>The blur prefab. Trusted (wire-time validated) whenever <see cref="UseBackgroundBlur"/> is on.</summary>
        public GameObject BackgroundBlurPrefab => _backgroundBlurPrefab;
        public float BlurBaseAlpha => _blurBaseAlpha;
        public float BlurPerFrameAlpha => _blurPerFrameAlpha;
        public float BlurMaxAlpha => _blurMaxAlpha;
        public float BlurFadeDuration => _blurFadeDuration;

        // ---- lazily-built caches, gated by an explicit built-flag (not by null-as-uninitialized) ----
        [NonSerialized] private readonly Dictionary<Type, ScreenDefinition> _screenCache =
            new Dictionary<Type, ScreenDefinition>();
        [NonSerialized] private readonly Dictionary<Type, FrameDefinition> _frameCache =
            new Dictionary<Type, FrameDefinition>();
        [NonSerialized] private bool _cachesBuilt;

        /// <summary>Config-tier screen lookup; null when the type is not authored here.</summary>
        public ScreenDefinition FindScreen(Type type)
        {
            EnsureCaches();
            return type != null && _screenCache.TryGetValue(type, out var def) ? def : null;
        }

        /// <summary>Config-tier frame lookup; null when the type is not authored here.</summary>
        public FrameDefinition FindFrame(Type type)
        {
            EnsureCaches();
            return type != null && _frameCache.TryGetValue(type, out var def) ? def : null;
        }

        /// <summary>Forces the caches to rebuild on next lookup (e.g. after editing definitions).</summary>
        public void InvalidateCaches() => _cachesBuilt = false;

        private void EnsureCaches()
        {
            if (_cachesBuilt) return;

            _screenCache.Clear();
            foreach (var def in _screens)
            {
                // A designer may leave an empty list slot; skip such data holes.
                if (def == null) continue;
                var t = def.ContentType;
                if (t != null) _screenCache[t] = def;
            }

            _frameCache.Clear();
            foreach (var def in _frames)
            {
                if (def == null) continue;
                var t = def.ContentType;
                if (t != null) _frameCache[t] = def;
            }

            _cachesBuilt = true;
        }

#if UNITY_EDITOR
        // Wire-time validation only (editor). Runtime trusts the authored toggle + wiring.
        private void OnValidate()
        {
            if (_useBackgroundBlur)
                Debug.Assert(_backgroundBlurPrefab != null,
                    "UseBackgroundBlur is on but no BackgroundBlurPrefab is assigned.", this);
        }
#endif
    }
}
