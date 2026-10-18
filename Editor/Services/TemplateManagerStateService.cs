using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;

/// <summary>
/// Remembers what the Template Manager was showing (category, selected elements per category) so it survives
/// script reloads, project reloads and editor restarts. Stored in EditorPrefs, separately for each UAS project.
/// </summary>
internal static class TemplateManagerStateService
{
    private static string Key(string name, string category = null)
    {
        var key = "UAS.TemplateManager." + ProjectSettingsService.Instance.GetCurrentProjectDirectory() + "." + name;
        return category == null ? key : key + "." + category;
    }

    internal static string GetCategory() => EditorPrefs.GetString(Key("Category"), "");

    internal static void SetCategory(string category) => EditorPrefs.SetString(Key("Category"), category ?? "");

    internal static HashSet<Guid> GetSelection(string category)
    {
        var saved = EditorPrefs.GetString(Key("Selection", category), "");
        var guids = new HashSet<Guid>();
        foreach (var part in saved.Split(','))
        {
            if (Guid.TryParse(part, out var guid)) guids.Add(guid);
        }
        return guids;
    }

    internal static void SetSelection(string category, IEnumerable<AiObjectModel> selected)
    {
        EditorPrefs.SetString(Key("Selection", category), string.Join(",", selected.Select(m => m.Guid)));
    }

    private static HashSet<Guid> unfolded;

    private static HashSet<Guid> Unfolded()
    {
        if (unfolded != null) return unfolded;
        unfolded = new HashSet<Guid>();
        foreach (var part in EditorPrefs.GetString(Key("Unfolded"), "").Split(','))
        {
            if (Guid.TryParse(part, out var guid)) unfolded.Add(guid);
        }
        return unfolded;
    }

    /// <summary>True when the item was left expanded (items are folded unless remembered otherwise).</summary>
    internal static bool IsUnfolded(Guid guid) => Unfolded().Contains(guid);

    internal static void SetUnfolded(Guid guid, bool value)
    {
        var set = Unfolded();
        if (!(value ? set.Add(guid) : set.Remove(guid))) return;
        EditorPrefs.SetString(Key("Unfolded"), string.Join(",", set));
    }

    /// <summary>Title of the tab last opened in a tab view (empty when none).</summary>
    internal static string GetSelectedTab(string layout) => EditorPrefs.GetString(Key("Tab", layout), "");

    internal static void SetSelectedTab(string layout, string title) => EditorPrefs.SetString(Key("Tab", layout), title ?? "");

    internal static float GetScroll(string collectionKey) => EditorPrefs.GetFloat(Key("Scroll", collectionKey), 0f);

    internal static void SetScroll(string collectionKey, float offset) => EditorPrefs.SetFloat(Key("Scroll", collectionKey), offset);

    /// <summary>Relative column widths the user dragged a side-by-side layout to (null when never changed).</summary>
    internal static float[] GetColumnWeights(string layout)
    {
        var saved = EditorPrefs.GetString(Key("Columns", layout), "");
        if (saved.Length == 0) return null;

        var parts = saved.Split(',');
        var weights = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out weights[i]) || weights[i] <= 0f)
            {
                return null;
            }
        }
        return weights;
    }

    internal static void SetColumnWeights(string layout, IEnumerable<float> weights)
    {
        var text = string.Join(",", weights.Select(w => w.ToString("R", CultureInfo.InvariantCulture)));
        EditorPrefs.SetString(Key("Columns", layout), text);
    }
}
