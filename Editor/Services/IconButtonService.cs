using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Small icon buttons using Unity's built-in editor icons (with a text fallback if an icon is missing).</summary>
internal static class IconButtonService
{
    internal static Button Create(string iconName, string tooltip, string fallbackText)
    {
        var button = new Button { tooltip = tooltip };
        var icon = EditorGUIUtility.IconContent(iconName).image as Texture2D;
        if (icon != null)
        {
            button.style.backgroundImage = Background.FromTexture2D(icon);
            button.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }
        else
        {
            button.text = fallbackText;
        }
        button.style.width = 24;
        button.style.height = 20;
        button.style.marginLeft = 2;
        button.style.marginRight = 2;
        return button;
    }

    internal static Button CreateSave(string tooltip = "Save a copy as a template") => Create("SaveAs", tooltip, "Save");

    /// <summary>Makes a toggle-style button read as "on" while <paramref name="on"/> is true.</summary>
    internal static void ShowToggled(Button button, bool on)
    {
        button.style.backgroundColor = on
            ? new StyleColor(new Color(0.23f, 0.47f, 0.73f, 0.55f))
            : new StyleColor(StyleKeyword.Null);
        button.style.unityFontStyleAndWeight = on ? FontStyle.Bold : FontStyle.Normal;
    }

    internal static Button CreateMenu(string tooltip = "More actions") => Create("_Menu", tooltip, "...");
}
