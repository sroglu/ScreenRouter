using System;
using UnityEngine;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// Authoring record for a modal frame: pooling and prefab from the base, plus stacking policy,
    /// background-click behaviour, and the open/close animation presets.
    /// </summary>
    [CreateAssetMenu(menuName = "PFound/ScreenRouter/Frame Definition", fileName = "FrameDefinition")]
    public sealed class FrameDefinition : ContentDefinition
    {
        [Header("Stacking")]
        [SerializeField]
        [Tooltip("When set, refuse to have other frames stacked over this one unless exempted below.")]
        private bool _blockOverlays;

        [SerializeField]
        [Tooltip("Frame types exempt from this frame's overlay block, so they may still open on top.")]
        private SerializableType[] _overlayExemptions = Array.Empty<SerializableType>();

        [SerializeField]
        [Tooltip("When set, this frame ignores the current top's stacking rules and always opens.")]
        private bool _alwaysStackable;

        [Header("Behaviour")]
        [SerializeField]
        [Tooltip("Whether a press on the dimmed backdrop closes this frame.")]
        private bool _closeOnBackdropPress = true;

        [Header("Animation")]
        [SerializeField] private FrameAnimationPreset _openingAnimation;
        [SerializeField] private FrameAnimationPreset _closingAnimation;

        public bool BlockOverlays => _blockOverlays;
        public bool AlwaysStackable => _alwaysStackable;
        public bool CloseOnBackdropPress => _closeOnBackdropPress;
        public FrameAnimationPreset OpeningAnimation => _openingAnimation;
        public FrameAnimationPreset ClosingAnimation => _closingAnimation;

        /// <summary>Builds the engine-free stacking rules this frame presents when it is the top.</summary>
        public StackingRules ToStackingRules()
        {
            // Unity always deserializes the array as non-null (empty at worst); check emptiness only.
            Type[] exemptions = null;
            if (_overlayExemptions.Length > 0)
            {
                exemptions = new Type[_overlayExemptions.Length];
                for (int i = 0; i < exemptions.Length; i++)
                    exemptions[i] = _overlayExemptions[i].Value;
            }
            return new StackingRules(_blockOverlays, exemptions, _alwaysStackable);
        }

        /// <summary>
        /// Creates an in-memory definition for runtime registration (no asset on disk). Pass null
        /// presets to fall back to the built-in pop-in/out defaults.
        /// </summary>
        public static FrameDefinition CreateRuntime(
            Type contentType,
            GameObject prefab,
            PoolingType pooling = PoolingType.Ephemeral,
            bool blockOverlays = false,
            Type[] overlayExemptions = null,
            bool alwaysStackable = false,
            bool closeOnBackdropPress = true,
            FrameAnimationPreset? openingAnimation = null,
            FrameAnimationPreset? closingAnimation = null)
        {
            var def = CreateInstance<FrameDefinition>();
            def.InitializeShared(contentType, prefab, pooling);
            def._blockOverlays = blockOverlays;
            def._alwaysStackable = alwaysStackable;
            def._closeOnBackdropPress = closeOnBackdropPress;
            def._openingAnimation = openingAnimation ?? FrameAnimationPreset.DefaultOpen();
            def._closingAnimation = closingAnimation ?? FrameAnimationPreset.DefaultClose();

            if (overlayExemptions != null && overlayExemptions.Length > 0)
            {
                def._overlayExemptions = new SerializableType[overlayExemptions.Length];
                for (int i = 0; i < overlayExemptions.Length; i++)
                    def._overlayExemptions[i] = new SerializableType(overlayExemptions[i]);
            }
            else
            {
                def._overlayExemptions = Array.Empty<SerializableType>();
            }
            return def;
        }
    }
}
