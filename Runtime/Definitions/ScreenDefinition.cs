using System;
using UnityEngine;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// Authoring record for a full-screen destination: prefab and pooling from the base, plus an
    /// optional transition animation played when the screen opens.
    /// </summary>
    [CreateAssetMenu(menuName = "PFound/ScreenRouter/Screen Definition", fileName = "ScreenDefinition")]
    public sealed class ScreenDefinition : ContentDefinition
    {
        [Header("Animation")]
        [SerializeField]
        [Tooltip("Optional transition played when this screen opens. Leave disabled to snap.")]
        private FrameAnimationPreset _transitionAnimation;

        public FrameAnimationPreset TransitionAnimation => _transitionAnimation;

        /// <summary>Creates an in-memory definition for runtime registration (no asset on disk).</summary>
        public static ScreenDefinition CreateRuntime(
            Type contentType,
            GameObject prefab,
            PoolingType pooling = PoolingType.Ephemeral,
            FrameAnimationPreset? transitionAnimation = null)
        {
            var def = CreateInstance<ScreenDefinition>();
            def.InitializeShared(contentType, prefab, pooling);
            def._transitionAnimation = transitionAnimation ?? default;
            return def;
        }
    }
}
