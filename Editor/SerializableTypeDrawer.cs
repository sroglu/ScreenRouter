using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PFound.ScreenRouter.EditorTools
{
    /// <summary>
    /// Inspector drawer for <see cref="SerializableType"/>. Rather than typing an assembly-qualified
    /// name by hand, the user picks from a dropdown of concrete <see cref="ContentBase"/> subclasses
    /// discovered in the loaded assemblies. The selection is stored back as the qualified name.
    /// </summary>
    [CustomPropertyDrawer(typeof(SerializableType))]
    public sealed class SerializableTypeDrawer : PropertyDrawer
    {
        // Discovered once per domain reload: display labels and their qualified names, in parallel.
        private static string[] _labels;
        private static string[] _qualifiedNames;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureCatalogue();

            var nameProp = property.FindPropertyRelative("_qualifiedName");
            int current = Mathf.Max(0, Array.IndexOf(_qualifiedNames, nameProp.stringValue));

            EditorGUI.BeginProperty(position, label, property);
            int picked = EditorGUI.Popup(position, label, current, BuildDisplay());
            if (picked != current)
                nameProp.stringValue = _qualifiedNames[picked];
            EditorGUI.EndProperty();
        }

        private static GUIContent[] BuildDisplay()
        {
            var display = new GUIContent[_labels.Length];
            for (int i = 0; i < _labels.Length; i++)
                display[i] = new GUIContent(_labels[i]);
            return display;
        }

        private static void EnsureCatalogue()
        {
            if (_labels != null) return;

            var labels = new List<string> { "<none>" };
            var names = new List<string> { string.Empty };

            foreach (var type in TypeCache.GetTypesDerivedFrom<ContentBase>())
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition) continue;
                labels.Add(type.Namespace == null ? type.Name : $"{type.Namespace}.{type.Name}");
                names.Add(type.AssemblyQualifiedName);
            }

            _labels = labels.ToArray();
            _qualifiedNames = names.ToArray();
        }
    }
}
