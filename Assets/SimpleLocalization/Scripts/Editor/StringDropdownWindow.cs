using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

public class StringDropdownWindow : EditorWindow
{
    private List<string> options;
    private string searchQuery = "";
    private Action<string> onSelected;
    private Vector2 scrollPos;
    private string currentValue;

    public static void Show(Rect activatorRect, List<string> options, string currentValue, Action<string> onSelected)
    {
        StringDropdownWindow window = CreateInstance<StringDropdownWindow>();
        window.options = options;
        window.onSelected = onSelected;
        window.currentValue = currentValue;

        // Use mouse position instead of activator rect
        Vector2 mouseScreenPos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
        Rect windowRect = new Rect(mouseScreenPos.x, mouseScreenPos.y, activatorRect.width, 200);

        window.ShowAsDropDown(windowRect, new Vector2(activatorRect.width, 200));
        window.Focus();
    }

    void OnGUI()
    {
        // Search bar
        GUI.SetNextControlName("SearchField");
        searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
        EditorGUI.FocusTextInControl("SearchField");

        string[] filtered = options
            .Where(o => string.IsNullOrEmpty(searchQuery) || o.ToLower().Contains(searchQuery.ToLower()))
            .ToArray();

        if (filtered.Length == 0)
        {
            EditorGUILayout.LabelField("No results found", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (string option in filtered)
        {
            bool isSelected = option == currentValue;

            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(8, 8, 3, 3),
                fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal
            };

            Rect itemRect = GUILayoutUtility.GetRect(new GUIContent(option), style, GUILayout.ExpandWidth(true));

            if (isSelected)
                EditorGUI.DrawRect(itemRect, new Color(0.2f, 0.5f, 1f, 0.25f));

            // Hover highlight
            if (itemRect.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(itemRect, new Color(1f, 1f, 1f, 0.05f));
                Repaint();
            }

            if (GUI.Button(itemRect, option, style))
            {
                onSelected?.Invoke(option);
                Close();
            }
        }

        EditorGUILayout.EndScrollView();

        // Close on escape
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            Close();
        }
    }
}