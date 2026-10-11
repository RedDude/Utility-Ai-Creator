using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine.UIElements;

/// <summary>
/// Shows the response curve editor in place of whatever the hosting editor window currently displays
/// (e.g. the template viewer), with a Back button that restores it.
/// </summary>
internal class ResponseCurveEditorOverlay : VisualElement
{
    internal ResponseCurveLcViewModel Editor { get; }
    internal bool IsOpen => parent != null;

    /// <summary>Raised when the editor closes; true when the user chose "Back without saving".</summary>
    internal IObservable<bool> OnClosed => onClosed;
    private readonly Subject<bool> onClosed = new Subject<bool>();

    private readonly List<KeyValuePair<VisualElement, StyleEnum<DisplayStyle>>> hiddenSiblings =
        new List<KeyValuePair<VisualElement, StyleEnum<DisplayStyle>>>();

    internal ResponseCurveEditorOverlay()
    {
        style.flexGrow = 1;
        style.paddingLeft = 8;
        style.paddingRight = 8;
        style.paddingTop = 4;
        style.paddingBottom = 8;

        Editor = new ResponseCurveLcViewModel();
        Editor.style.flexGrow = 1;
        Add(Editor);

        // Back sits at the start of the editor's own header row, next to the owner and the curve picker.
        var discardButton = new Button(() => Close(true))
        {
            text = "Back without saving",
            tooltip = "Go back and undo every change made in this editor"
        };
        discardButton.style.marginLeft = 0;
        discardButton.style.marginRight = 8;
        discardButton.style.flexShrink = 0;
        Editor.InsertInHeaderStart(discardButton);

        var backButton = new Button(() => Close(false)) { text = "< Back", tooltip = "Back to where you were" };
        backButton.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
        backButton.style.marginLeft = 0;
        backButton.style.marginRight = 4;
        backButton.style.flexShrink = 0;
        Editor.InsertInHeaderStart(backButton);
    }

    /// <summary>Hides everything in the window that hosts <paramref name="anchor"/> and shows the editor instead.</summary>
    internal void Open(VisualElement anchor)
    {
        if (IsOpen || anchor?.panel == null) return;

        // The topmost element below the panel is the window's root element.
        var windowRoot = anchor;
        while (windowRoot.parent != null && windowRoot.parent != anchor.panel.visualTree)
        {
            windowRoot = windowRoot.parent;
        }

        hiddenSiblings.Clear();
        foreach (var child in windowRoot.Children())
        {
            hiddenSiblings.Add(new KeyValuePair<VisualElement, StyleEnum<DisplayStyle>>(child, child.style.display));
            child.style.display = DisplayStyle.None;
        }
        windowRoot.Add(this);
    }

    internal void Close(bool discardChanges = false)
    {
        if (!IsOpen) return;

        parent.Remove(this);
        foreach (var hidden in hiddenSiblings)
        {
            hidden.Key.style.display = hidden.Value;
        }
        hiddenSiblings.Clear();
        Editor.ReleaseSubscriptions();
        onClosed.OnNext(discardChanges);
    }
}
