using System;
using System.Collections.Generic;
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
}
