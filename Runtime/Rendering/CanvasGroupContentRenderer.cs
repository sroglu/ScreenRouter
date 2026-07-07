using UnityEngine;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// uGUI presentation backend. A local <see cref="Canvas"/> with <c>overrideSorting</c> lets
    /// each content own its draw order, and a <see cref="CanvasGroup"/> carries alpha and input
    /// gating. During an animation the group keeps blocking raycasts so nothing underneath is
    /// clicked through the transition, but interaction is switched off.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CanvasGroupContentRenderer : MonoBehaviour, IContentRenderer
    {
        private Canvas _canvas;
        private CanvasGroup _group;

        public void Initialize(GameObject owner)
        {
            _canvas = GetComponent<Canvas>();
            _group = GetComponent<CanvasGroup>();
            _canvas.overrideSorting = true;
        }

        // Fields are set in Initialize via GetComponent; RequireComponent guarantees they exist,
        // and Initialize always runs before the router touches the renderer. Access them directly.
        public int SortingOrder
        {
            get => _canvas.sortingOrder;
            set => _canvas.sortingOrder = value;
        }

        public float Alpha
        {
            get => _group.alpha;
            set => _group.alpha = Mathf.Clamp01(value);
        }

        public bool Interactable
        {
            get => _group.interactable;
            set => _group.interactable = value;
        }

        public bool BlocksRaycasts
        {
            get => _group.blocksRaycasts;
            set => _group.blocksRaycasts = value;
        }

        public void SetStateOpened()
        {
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }

        public void SetStateClosed()
        {
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        public void SetStateAnimating()
        {
            // Visible-but-inert: no interaction, yet still catches raycasts to stop click-through.
            _group.interactable = false;
            _group.blocksRaycasts = true;
        }
    }
}
