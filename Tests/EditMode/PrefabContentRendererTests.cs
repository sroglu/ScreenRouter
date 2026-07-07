#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using PFound.ScreenRouter;
using PFound.ScreenRouter.Unity;

namespace PFound.ScreenRouter.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the production <see cref="PrefabContentRenderer"/> driven through a real
    /// <see cref="ScreenRouter"/>: prefab instantiation, screen-vs-frame parenting, show/hide toggling,
    /// destruction, presenter binding, and CanvasGroup visibility. EditMode is enough because the
    /// renderer uses <c>DestroyImmediate</c> off the play loop, so destruction is observable in-line.
    /// </summary>
    public sealed class PrefabContentRendererTests
    {
        private Transform _screenRoot;
        private Transform _frameRoot;
        private GameObject _screenPrefab;
        private GameObject _framePrefab;
        private ScreenRouter _router;
        private PrefabContentRenderer _renderer;
        private GameObject _hostRoot;

        // Content types.
        private sealed class ScreenA : ContentBase { }
        private sealed class ScreenB : ContentBase { }
        private sealed class FrameX : ContentBase { }

        // A prefab-root component that records the ContentBase it was bound to.
        private sealed class RecordingPresenter : MonoBehaviour, IContentPresenter
        {
            public ContentBase Bound;
            public void Bind(ContentBase content) => Bound = content;
        }

        [SetUp]
        public void SetUp()
        {
            _hostRoot = new GameObject("Host");
            _screenRoot = new GameObject("ScreenRoot").transform;
            _frameRoot = new GameObject("FrameRoot").transform;
            _screenRoot.SetParent(_hostRoot.transform, false);
            _frameRoot.SetParent(_hostRoot.transform, false);

            _screenPrefab = new GameObject("ScreenPrefab");
            _screenPrefab.AddComponent<RecordingPresenter>();
            _screenPrefab.SetActive(false);

            _framePrefab = new GameObject("FramePrefab");
            _framePrefab.AddComponent<CanvasGroup>();
            _framePrefab.SetActive(false);

            var registry = new PrefabContentRegistry()
                .Register<ScreenA>(_screenPrefab, ContentKind.Screen)
                .Register<ScreenB>(_screenPrefab, ContentKind.Screen)
                .Register<FrameX>(_framePrefab, ContentKind.Frame);

            _renderer = new PrefabContentRenderer(_screenRoot, _frameRoot, registry.Resolve);
            _router = new ScreenRouter(_renderer);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hostRoot != null) Object.DestroyImmediate(_hostRoot);
            if (_screenPrefab != null) Object.DestroyImmediate(_screenPrefab);
            if (_framePrefab != null) Object.DestroyImmediate(_framePrefab);
        }

        [Test]
        public void SwitchScreen_InstantiatesActiveViewUnderScreenRoot()
        {
            _router.SwitchScreen<ScreenA>();

            Assert.AreEqual(1, _screenRoot.childCount, "one screen view should exist");
            Assert.AreEqual(0, _frameRoot.childCount, "no frame views");
            Assert.IsTrue(_screenRoot.GetChild(0).gameObject.activeSelf, "shown view should be active");
        }

        [Test]
        public void SwitchScreen_BindsContentToPresenter()
        {
            _router.SwitchScreen<ScreenA>();

            var content = _router.GetActiveScreen<ScreenA>();
            var view = _renderer.ViewOf(content);
            var presenter = view.GetComponent<RecordingPresenter>();
            Assert.AreSame(content, presenter.Bound, "presenter should receive its owning content");
        }

        [Test]
        public void SwitchScreen_DestroysPreviousScreenView()
        {
            _router.SwitchScreen<ScreenA>();
            var first = _router.GetActiveScreen<ScreenA>();
            var firstView = _renderer.ViewOf(first);

            _router.SwitchScreen<ScreenB>();

            Assert.IsTrue(firstView == null, "previous screen view should be destroyed");
            Assert.AreEqual(1, _screenRoot.childCount, "only the new screen remains");
        }

        [Test]
        public void OpenFrame_InstantiatesUnderFrameRoot_AndTogglesCanvasGroup()
        {
            _router.SwitchScreen<ScreenA>();
            _router.OpenFrame<FrameX>();

            Assert.AreEqual(1, _frameRoot.childCount, "frame view lives under the frame root");
            var frameView = _frameRoot.GetChild(0);
            Assert.IsTrue(frameView.gameObject.activeSelf, "frame should be shown");
            var group = frameView.GetComponent<CanvasGroup>();
            Assert.AreEqual(1f, group.alpha, "CanvasGroup made fully visible on show");
            Assert.IsTrue(group.interactable, "CanvasGroup interactable on show");
            Assert.IsTrue(group.blocksRaycasts, "CanvasGroup blocks raycasts on show");
        }

        [Test]
        public void CloseFrame_DestroysFrameView_ScreenSurvives()
        {
            _router.SwitchScreen<ScreenA>();
            _router.OpenFrame<FrameX>();
            _router.CloseFrame();

            Assert.AreEqual(0, _frameRoot.childCount, "frame view destroyed on close");
            Assert.AreEqual(1, _screenRoot.childCount, "screen view survives");
        }
    }
}
#endif
