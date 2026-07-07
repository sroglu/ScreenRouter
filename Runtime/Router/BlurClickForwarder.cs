using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// Attach to the background-blur prefab. Forwards a pointer-down on the dimmed area to a
    /// listener (the overlay), which decides whether the topmost frame should close.
    /// </summary>
    public sealed class BlurClickForwarder : MonoBehaviour, IPointerDownHandler
    {
        /// <summary>Raised when the user presses on the blur surface.</summary>
        public event Action Pressed;

        public void OnPointerDown(PointerEventData eventData) => Pressed?.Invoke();
    }
}
