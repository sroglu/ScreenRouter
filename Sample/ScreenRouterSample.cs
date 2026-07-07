using UnityEngine;
using PFound.ScreenRouter;
using PFound.ScreenRouter.Unity;

namespace PFound.ScreenRouter.Sample
{
    /// <summary>
    /// End-to-end usage of <see cref="PrefabContentRenderer"/> without any pre-authored assets: it
    /// builds two placeholder "prefabs" (plain GameObjects) at runtime, registers one as a screen and
    /// one as a modal frame, wires a <see cref="ScreenRouter"/>, then opens the screen and stacks a
    /// frame on it. In a real project the prefabs would be assets assigned on a
    /// <see cref="ScreenRouterHost"/> in the inspector, and the content classes would carry your own
    /// view logic. Attach this to an empty GameObject and press Play.
    /// </summary>
    public sealed class ScreenRouterSample : MonoBehaviour
    {
        // Content types: pure ContentBase subclasses. A frame that wants config implements IConfigurable.
        public sealed class MainScreen : ContentBase
        {
            protected override void OnOpen() => Debug.Log("[Sample] MainScreen opened.");
            protected override void OnBecomeBackground() => Debug.Log("[Sample] MainScreen backgrounded by a frame.");
            protected override void OnBecomeForeground() => Debug.Log("[Sample] MainScreen foregrounded again.");
        }

        public sealed class ConfirmFrame : ContentBase, IConfigurable<string>
        {
            public void Configure(string message) => Debug.Log("[Sample] ConfirmFrame configured: " + message);
            protected override void OnOpen() => Debug.Log("[Sample] ConfirmFrame opened on top.");
        }

        private ScreenRouter _router;

        private void Start()
        {
            // Two hierarchy roots; the frame root is created after (later sibling) so frames sort on top.
            var screenRoot = new GameObject("ScreenRoot").transform;
            var frameRoot = new GameObject("FrameRoot").transform;
            screenRoot.SetParent(transform, false);
            frameRoot.SetParent(transform, false);

            var registry = new PrefabContentRegistry()
                .Register<MainScreen>(MakePlaceholderPrefab("MainScreen-Prefab"), ContentKind.Screen)
                .Register<ConfirmFrame>(MakePlaceholderPrefab("ConfirmFrame-Prefab"), ContentKind.Frame);

            var renderer = new PrefabContentRenderer(screenRoot, frameRoot, registry.Resolve);
            _router = new ScreenRouter(renderer);

            _router.SwitchScreen<MainScreen>();
            _router.OpenFrame<ConfirmFrame, string>("Are you sure?");
            // _router.CloseFrame(); // foregrounds MainScreen again
        }

        // Stand-in for a prefab asset. Instantiate() works on any GameObject, so this doubles as a prefab.
        private static GameObject MakePlaceholderPrefab(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false); // prefabs are typically instantiated inactive by the renderer anyway
            return go;
        }
    }
}
