using UnityEngine;
using UnityEngine.UIElements;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// UI Toolkit presentation backend. Draw order comes from the panel's sort order; alpha and
    /// input gating are pushed onto the document's root visual element. As with the uGUI backend,
    /// the animating state stays hittable (so it blocks input to layers below) but is not itself
    /// interactive.
    ///
    /// <see cref="UIDocument.rootVisualElement"/> is a genuine Unity boundary: it returns null until
    /// the panel is built. Those accesses use an explicit guard; the <see cref="UIDocument"/> and
    /// its <see cref="PanelSettings"/> are required wiring and are accessed directly (fail-fast).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UIDocumentContentRenderer : MonoBehaviour, IContentRenderer
    {
        private UIDocument _document;
        private int _sortingOrder;
        private bool _interactable = true;
        private bool _blocksRaycasts = true;

        private VisualElement Root => _document.rootVisualElement;

        public void Initialize(GameObject owner)
        {
            _document = GetComponent<UIDocument>();
            _sortingOrder = (int)_document.panelSettings.sortingOrder;
        }

        public int SortingOrder
        {
            get => _sortingOrder;
            set
            {
                _sortingOrder = value;
                _document.panelSettings.sortingOrder = value;
            }
        }

        public float Alpha
        {
            get
            {
                var root = Root;
                return root != null ? root.resolvedStyle.opacity : 1f;
            }
            set
            {
                var root = Root;
                if (root != null) root.style.opacity = Mathf.Clamp01(value);
            }
        }

        public bool Interactable
        {
            get => _interactable;
            set { _interactable = value; ApplyPicking(); }
        }

        public bool BlocksRaycasts
        {
            get => _blocksRaycasts;
            set { _blocksRaycasts = value; ApplyPicking(); }
        }

        public void SetStateOpened()
        {
            var root = Root;
            if (root != null)
            {
                root.style.opacity = 1f;
                root.style.display = DisplayStyle.Flex;
            }
            _interactable = true;
            _blocksRaycasts = true;
            ApplyPicking();
        }

        public void SetStateClosed()
        {
            var root = Root;
            if (root != null)
            {
                root.style.opacity = 0f;
                root.style.display = DisplayStyle.None;
            }
            _interactable = false;
            _blocksRaycasts = false;
            ApplyPicking();
        }

        public void SetStateAnimating()
        {
            var root = Root;
            if (root != null) root.style.display = DisplayStyle.Flex;
            _interactable = false;
            _blocksRaycasts = true;
            ApplyPicking();
        }

        // Picking mode is the UI Toolkit equivalent of raycast blocking: on whenever the element
        // should catch pointer events (either interactive, or merely blocking through).
        private void ApplyPicking()
        {
            var root = Root;
            if (root == null) return;
            bool catchesPointer = _interactable || _blocksRaycasts;
            root.pickingMode = catchesPointer ? PickingMode.Position : PickingMode.Ignore;
            root.SetEnabled(_interactable);
        }
    }
}
