using UnityEditor;
using UnityEngine;

namespace Assets.SimpleLocalization.Scripts.Editor
{
    /// <summary>
    /// Adds "Sync" button to LocalizationSync script.
    /// </summary>
    [CustomEditor(typeof(LocalizationKeyHolder))]
    public class LocalizationKeyHolderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Localization Editor"))
            {
                LocalizationEditorWindow.Open();
            }
        }
    }
}