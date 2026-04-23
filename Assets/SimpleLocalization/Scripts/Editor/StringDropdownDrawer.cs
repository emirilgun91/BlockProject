using System.Collections.Generic;
using System.Linq;
using Attributes;
using UnityEditor;
using UnityEngine;


[CustomPropertyDrawer(typeof(LocalizationKeyDropDownAttribute))]
public class StringDropdownDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.HelpBox(position, "StringDropdown only works on string fields", MessageType.Error);
            return;
        }

        LocalizationKeyDropDownAttribute attr = (LocalizationKeyDropDownAttribute)attribute;

        EditorGUI.BeginProperty(position, label, property);

        Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
        Rect buttonRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, position.width - EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);

        EditorGUI.LabelField(labelRect, label);

        string displayValue = string.IsNullOrEmpty(property.stringValue) ? "Select..." : property.stringValue;

        if (EditorGUI.DropdownButton(buttonRect, new GUIContent(displayValue), FocusType.Keyboard))
        {
            SerializedProperty propertyCopy = property.Copy();
            SerializedObject serializedObjectCopy = property.serializedObject;

            StringDropdownWindow.Show(buttonRect, GetAllKeys(), property.stringValue, selected =>
            {
                propertyCopy.stringValue = selected;
                serializedObjectCopy.ApplyModifiedProperties();
            });
        }

        EditorGUI.EndProperty();
    }

    
    public List<string> GetAllKeys()
    {
        List<string> keys = new List<string>();

        // Load all TextAssets from Resources/Localization
        TextAsset[] csvFiles = Resources.LoadAll<TextAsset>("Localization");

        foreach (TextAsset csv in csvFiles)
        {
            string[] lines = csv.text.Split('\n');

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Split by comma and take first column
                string[] columns = line.Split(',');
                string firstColumn = columns[0].Trim();

                if (!string.IsNullOrEmpty(firstColumn) && firstColumn != "Key")
                    keys.Add(firstColumn);
            }
        }

        return keys;
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}