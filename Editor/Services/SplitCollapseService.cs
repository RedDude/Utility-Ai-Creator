using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Double-clicking the divider of a split view collapses its left pane; a vertical title marks the strip.</summary>
internal static class SplitCollapseService
{
    private const float StripWidth = 24f;

    public static void Attach(TwoPaneSplitView split, string title)
    {
        if (split == null || split.childCount < 2) return;
        var leftPane = split[0];
        var rightPane = split[1];
        var collapsed = false;

        // Vertical title, shown only while collapsed. Rotating a wide, short label turns it into a tall, thin one.
        var strip = new VisualElement { tooltip = "Double-click to expand " + title };
        strip.style.position = Position.Absolute;
        strip.style.left = 0;
        strip.style.top = 0;
        strip.style.bottom = 0;
        strip.style.width = StripWidth;
        strip.style.display = DisplayStyle.None;
        strip.style.backgroundColor = new Color(0f, 0f, 0f, 0.12f);
        var label = new Label(title) { pickingMode = PickingMode.Ignore };
        label.style.position = Position.Absolute;
        label.style.width = 120;
        label.style.height = StripWidth;
        label.style.left = (StripWidth - 120) / 2f; // centre of the label sits in the middle of the strip
        label.style.top = 50;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.opacity = 0.8f;
        label.style.rotate = new Rotate(-90f);
        strip.Add(label);
        split.hierarchy.Add(strip); // not split.Add: a two-pane split view accepts exactly two content children

        void Toggle()
        {
            collapsed = !collapsed;
            if (collapsed) split.CollapseChild(0);
            else split.UnCollapse();
            strip.style.display = collapsed ? DisplayStyle.Flex : DisplayStyle.None;
            rightPane.style.marginLeft = collapsed ? StripWidth : 0;
        }

        // The divider captures the pointer to drag, so listen on the way down.
        var anchor = split.Q<VisualElement>(className: "unity-two-pane-split-view__dragline-anchor");
        anchor?.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 0 && evt.clickCount == 2) Toggle();
        }, TrickleDown.TrickleDown);
        strip.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 0 && evt.clickCount == 2) Toggle();
        });
    }
}
