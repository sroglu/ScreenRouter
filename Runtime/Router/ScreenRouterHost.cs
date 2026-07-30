using UnityEngine;
using UnityEngine.SceneManagement;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// Thin MonoBehaviour shell that owns the pure-C# <see cref="ScreenRouter"/>. It exists only to
    /// supply serialized wiring (the config and the content root), publish the router on its static
    /// seam, pump the per-frame tick, and forward scene-change and teardown lifecycle. All routing
    /// logic lives in the POCO router, not here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenRouterHost : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Project-wide router configuration asset.")]
        private ScreenRouterConfig _config;

        [SerializeField]
        [Tooltip("Transform under which content instances and the blur surface are spawned.")]
        private Transform _contentRoot;

        private ScreenRouter _router;

        /// <summary>The router this host drives; valid after Awake.</summary>
        public ScreenRouter Router => _router;

        private void Awake()
        {
            Debug.Assert(_config != null, "ScreenRouterHost is missing its ScreenRouterConfig.", this);
            Debug.Assert(_contentRoot != null, "ScreenRouterHost is missing its content root Transform.", this);

            _router = new ScreenRouter(_config, _contentRoot);
            ScreenRouter.Publish(_router);
        }

        private void OnEnable() => SceneManager.activeSceneChanged += HandleSceneChanged;

        private void OnDisable() => SceneManager.activeSceneChanged -= HandleSceneChanged;

        private void Update() => _router.Tick(Time.unscaledDeltaTime);

        private void OnDestroy() => _router.Shutdown();

        private void HandleSceneChanged(Scene from, Scene to) => _router.HandleSceneChanged();
    }
}
