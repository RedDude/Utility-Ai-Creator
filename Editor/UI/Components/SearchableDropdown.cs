using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Makes a dropdown (or any clickable element) open Unity's SearchWindow, the same searchable picker the
/// Shader Graph / Visual Scripting "create node" menu uses. Entries like "----Templates----" become groups.
/// </summary>
internal static class SearchableDropdown
{
    private static double lastOpened;
    private static Provider activeProvider;

    /// <summary>Makes a DropdownField open a searchable list of its choices.</summary>
    internal static void Attach(DropdownField field)
    {
        Attach(field, () => field.choices, () => field.value, picked => field.value = picked);
    }

    /// <summary>Makes any popup-style field or button open a searchable list.</summary>
    internal static void Attach(VisualElement field, Func<IList<string>> getChoices, Func<string> getCurrent, Action<string> onPick)
    {
        void Open(EventBase evt, int button, Vector2 panelPosition)
        {
            if (button != 0) return;

            // Replace the field's own menu.
            evt.StopImmediatePropagation();
            evt.PreventDefault();

            // Pointer and compatibility mouse events both arrive for one click; open once.
            if (EditorApplication.timeSinceStartup - lastOpened < 0.25) return;
            lastOpened = EditorApplication.timeSinceStartup;

            Show(panelPosition, field.worldBound.width, getChoices(), getCurrent(), onPick);
        }

        field.RegisterCallback<PointerDownEvent>(evt => Open(evt, evt.button, evt.position), TrickleDown.TrickleDown);
        field.RegisterCallback<MouseDownEvent>(evt => Open(evt, evt.button, evt.mousePosition), TrickleDown.TrickleDown);
    }

    /// <summary>Opens the searchable list at a panel position (used by context menus as well as fields).</summary>
    internal static void Show(Vector2 panelPosition, float width, IList<string> choices, string current, Action<string> onPick)
    {
        if (choices == null || choices.Count == 0) return;

        if (activeProvider != null) UnityEngine.Object.DestroyImmediate(activeProvider);
        activeProvider = ScriptableObject.CreateInstance<Provider>();
        activeProvider.Init(choices, current, onPick);

        var screenPosition = GUIUtility.GUIToScreenPoint(panelPosition);
        SearchWindow.Open(new SearchWindowContext(screenPosition, Mathf.Max(width, 280f), 360f), activeProvider);
    }

    private class Provider : ScriptableObject, ISearchWindowProvider
    {
        private IList<string> choices;
        private string current;
        private Action<string> onPick;

        internal void Init(IList<string> choices, string current, Action<string> onPick)
        {
            this.choices = choices;
            this.current = current;
            this.onPick = onPick;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry> { new SearchTreeGroupEntry(new GUIContent("Select"), 0) };

            // "Base Types" are always listed directly at the top; Templates and Demos are folders below them.
            var inFolder = false;
            foreach (var choice in choices)
            {
                if (choice == null) continue;

                if (choice.StartsWith("----"))
                {
                    var title = choice.Trim('-', ' ');
                    inFolder = !title.StartsWith("Base Types");
                    if (inFolder)
                    {
                        tree.Add(new SearchTreeGroupEntry(new GUIContent(title), 1));
                    }
                    continue;
                }

                var label = choice == current ? choice + "  (current)" : choice;
                tree.Add(new SearchTreeEntry(new GUIContent(label))
                {
                    level = inFolder ? 2 : 1,
                    userData = choice
                });
            }
            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is string picked)
            {
                onPick(picked);
            }
            return true;
        }
    }
}
