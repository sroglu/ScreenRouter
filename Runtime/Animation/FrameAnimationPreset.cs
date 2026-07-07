using System;
using UnityEngine;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// A reusable, inspector-authored description of a frame transition. Scale and fade are driven
    /// independently — each has its own normalized curve evaluated over the shared duration — so a
    /// frame can, say, pop in scale while cross-fading on a different easing. A disabled preset
    /// means "snap instantly", which the animation manager treats as a zero-length transition.
    /// </summary>
    [Serializable]
    public struct FrameAnimationPreset
    {
        [Tooltip("When off, the transition is instant (no animation frames are ticked).")]
        public bool Enabled;

        [Min(0f)]
        [Tooltip("Total transition length in seconds.")]
        public float Duration;

        [Header("Scale")]
        public Vector3 ScaleFrom;
        public Vector3 ScaleTo;
        [Tooltip("Normalized 0..1 curve mapping progress to the scale lerp factor.")]
        public AnimationCurve ScaleCurve;

        [Header("Fade")]
        [Range(0f, 1f)] public float AlphaFrom;
        [Range(0f, 1f)] public float AlphaTo;
        [Tooltip("Normalized 0..1 curve mapping progress to the alpha lerp factor.")]
        public AnimationCurve FadeCurve;

        /// <summary>A sensible pop-in default: 0.85 -> 1 scale, 0 -> 1 alpha, ease-out over 0.2s.</summary>
        public static FrameAnimationPreset DefaultOpen() => new FrameAnimationPreset
        {
            Enabled = true,
            Duration = 0.2f,
            ScaleFrom = new Vector3(0.85f, 0.85f, 1f),
            ScaleTo = Vector3.one,
            ScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
            AlphaFrom = 0f,
            AlphaTo = 1f,
            FadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
        };

        /// <summary>The reverse of <see cref="DefaultOpen"/> for closing.</summary>
        public static FrameAnimationPreset DefaultClose() => new FrameAnimationPreset
        {
            Enabled = true,
            Duration = 0.15f,
            ScaleFrom = Vector3.one,
            ScaleTo = new Vector3(0.85f, 0.85f, 1f),
            ScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
            AlphaFrom = 1f,
            AlphaTo = 0f,
            FadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
        };

        /// <summary>Evaluates scale at normalized progress t (0..1), tolerating a null curve.</summary>
        public Vector3 EvaluateScale(float t)
        {
            float k = ScaleCurve != null ? ScaleCurve.Evaluate(t) : t;
            return Vector3.LerpUnclamped(ScaleFrom, ScaleTo, k);
        }

        /// <summary>Evaluates alpha at normalized progress t (0..1), tolerating a null curve.</summary>
        public float EvaluateAlpha(float t)
        {
            float k = FadeCurve != null ? FadeCurve.Evaluate(t) : t;
            return Mathf.LerpUnclamped(AlphaFrom, AlphaTo, k);
        }
    }
}
