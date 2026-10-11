using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Double-click a label to edit its text in place. Enter or leaving the field commits, Escape cancels.</summary>
internal static class InlineRename
{
    internal const string EditableClass = "uai-editable-name";

    internal static void Attach(Label label, Func<string> getValue, Action<string> commit)
    {
        label.AddToClassList(EditableClass);
        label.tooltip = "Double-click to rename";

        label.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.clickCount != 2) return;
            var parent = label.parent;
            if (parent == null) return;
            evt.StopPropagation();

            var field = new TextField { value = getValue() ?? "" };
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.style.marginTop = 0;
            field.style.marginBottom = 0;
            label.style.display = DisplayStyle.None;
            parent.Insert(parent.IndexOf(label) + 1, field);
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectAll();
            });

            var finished = false;
            void Finish(bool apply)
            {
                if (finished) return;
                finished = true;
                var newValue = field.value;
                field.RemoveFromHierarchy();
                label.style.display = StyleKeyword.Null;
                if (apply && newValue != getValue())
                {
                    commit(newValue);
                }
            }

            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    Finish(true);
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    Finish(false);
                    e.StopPropagation();
                }
            });
            field.RegisterCallback<FocusOutEvent>(_ => Finish(true));
        });
    }
}
