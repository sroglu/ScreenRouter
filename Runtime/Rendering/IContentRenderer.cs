using UnityEngine;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// The presentation surface for a piece of content. A content prefab carries BOTH a
    /// <see cref="ContentBase"/> behaviour and one component implementing this interface; the base
    /// finds it via GetComponent at Awake (there is no central view factory). The router and the
    /// animation manager only ever touch content visuals through this contract, so uGUI and UI
    /// Toolkit backends stay interchangeable.
    /// </summary>
    public interface IContentRenderer
    {
        /// <summary>Draw order relative to other content; higher paints on top.</summary>
        int SortingOrder { get; set; }

        /// <summary>Overall opacity in the 0..1 range; the animation manager drives this.</summary>
        float Alpha { get; set; }

        /// <summary>Whether the content responds to pointer input.</summary>
        bool Interactable { get; set; }

        /// <summary>Whether the content swallows raycasts (prevents click-through to what is below).</summary>
        bool BlocksRaycasts { get; set; }

        /// <summary>Called once, when the owning content first awakes. Wire up backend references here.</summary>
        void Initialize(GameObject owner);

        /// <summary>Snap to the fully-visible, interactive resting state.</summary>
        void SetStateOpened();

        /// <summary>Snap to the fully-hidden, non-interactive resting state.</summary>
        void SetStateClosed();

        /// <summary>Enter the transitional state: visible but not interactive, and still blocking
        /// raycasts so nothing underneath is clicked mid-animation.</summary>
        void SetStateAnimating();
    }
}
