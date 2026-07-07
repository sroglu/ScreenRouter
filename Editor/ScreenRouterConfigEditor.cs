using UnityEditor;
using UnityEngine;

namespace PFound.ScreenRouter.EditorTools
{
    /// <summary>
    /// Custom inspector for <see cref="ScreenRouterConfig"/>. Its only job beyond the default layout
    /// is to make the authored blur toggle drive the visibility of the optional blur fields: when
    /// "Use Background Blur" is off, the prefab and blur-tuning sliders are greyed out. Presence is
    /// authored through the toggle, never inferred from the prefab being null.
    /// </summary>
    [CustomEditor(typeof(ScreenRouterConfig))]
    public sealed class ScreenRouterConfigEditor : UnityEditor.Editor
    {
        // Optional blur fields hidden behind the authored toggle.
        private static readonly string[] BlurGatedFields =
        {
            "_backgroundBlurPrefab",
            "_blurBaseAlpha",
            "_blurPerFrameAlpha",
            "_blurMaxAlpha",
            "_blurFadeDuration",
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var useBlur = serializedObject.FindProperty("_useBackgroundBlur");

            var iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                // The script reference field is read-only by convention.
                if (iterator.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(iterator, true);
                    continue;
                }

                bool gated = System.Array.IndexOf(BlurGatedFields, iterator.propertyPath) >= 0;
                using (new EditorGUI.DisabledScope(gated && !useBlur.boolValue))
                    EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
