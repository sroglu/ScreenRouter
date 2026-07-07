using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.ScreenRouter.Unity
{
    /// <summary>
    /// Production <see cref="IContentRenderer"/> that realizes each navigable <see cref="ContentBase"/>
    /// as an instantiated prefab GameObject. Deliberately UI-stack-agnostic: it only instantiates a
    /// prefab under a root, toggles its active state (and a <see cref="CanvasGroup"/> if the prefab
    /// carries one), and destroys it — so the same renderer serves a uGUI prefab or a prefab that
    /// hosts a UI-Toolkit <c>UIDocument</c> equally. It never references a specific UI framework.
    ///
    /// Screen-vs-frame: screens are parented under <c>screenRoot</c>, modal frames under
    /// <c>frameRoot</c>. As long as the frame root is ordered after the screen root in the hierarchy
    /// (a later sibling, or a higher-sorting canvas), frames render above the screen; each newly
    /// opened frame is sent to the last sibling so it stacks above earlier frames. Which root a
    /// content uses comes from its <see cref="PrefabBinding.Kind"/>, resolved per content type.
    ///
    /// The prefab for a content type is resolved through a caller-supplied delegate (see
    /// <see cref="PrefabContentRegistry"/> for the batteries-included map). The <see cref="ContentBase"/>
    /// object itself is produced by a content factory (defaults to <see cref="Activator"/>; inject a
    /// DI-backed factory to give content constructor dependencies).
    ///
    /// Fail-fast: a missing prefab binding or an un-createable content type throws rather than
    /// silently rendering nothing. Main-thread only, matching the router.
    /// </summary>
    public sealed class PrefabContentRenderer : IContentRenderer
    {
        private readonly Transform _screenRoot;
        private readonly Transform _frameRoot;
        private readonly Func<Type, PrefabBinding> _resolve;
        private readonly Func<Type, ContentBase> _createContent;
        private readonly Dictionary<ContentBase, GameObject> _views = new Dictionary<ContentBase, GameObject>();

        /// <param name="screenRoot">Parent for full-screen content.</param>
        /// <param name="frameRoot">Parent for stacked modal frames; order it above the screen root.</param>
        /// <param name="resolve">Maps a content type to its prefab and kind.</param>
        /// <param name="createContent">Builds the content instance; defaults to <see cref="Activator.CreateInstance(Type)"/>.</param>
        public PrefabContentRenderer(
            Transform screenRoot,
            Transform frameRoot,
            Func<Type, PrefabBinding> resolve,
            Func<Type, ContentBase> createContent = null)
        {
            _screenRoot = screenRoot ? screenRoot : throw new ArgumentNullException(nameof(screenRoot));
            _frameRoot = frameRoot ? frameRoot : throw new ArgumentNullException(nameof(frameRoot));
            _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
            _createContent = createContent ?? (type => (ContentBase)Activator.CreateInstance(type));
        }

        public ContentBase Create(Type contentType)
        {
            var binding = _resolve(contentType);
            var content = _createContent(contentType);

            var root = binding.Kind == ContentKind.Frame ? _frameRoot : _screenRoot;
            var view = UnityEngine.Object.Instantiate(binding.Prefab, root, false);
            view.transform.SetAsLastSibling(); // frames stack above earlier ones; harmless for screens
            view.SetActive(false);             // opens hidden; Show reveals it after Opening/Configure

            if (view.TryGetComponent<IContentPresenter>(out var presenter)) presenter.Bind(content);

            _views.Add(content, view);
            return content;
        }

        public void Show(ContentBase content)
        {
            var view = _views[content];
            view.SetActive(true);
            view.transform.SetAsLastSibling();
            SetGroupVisible(view, true);
        }

        public void Hide(ContentBase content)
        {
            var view = _views[content];
            SetGroupVisible(view, false);
            view.SetActive(false);
        }

        public void Destroy(ContentBase content)
        {
            var view = _views[content];
            _views.Remove(content);
            DestroyView(view);
        }

        /// <summary>The live GameObject realizing <paramref name="content"/> (for inspection/tests). Null once destroyed.</summary>
        public GameObject ViewOf(ContentBase content) => _views.TryGetValue(content, out var view) ? view : null;

        private static void SetGroupVisible(GameObject view, bool visible)
        {
            if (!view.TryGetComponent<CanvasGroup>(out var group)) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private static void DestroyView(GameObject view)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(view);
            else UnityEngine.Object.DestroyImmediate(view);
        }
    }
}
