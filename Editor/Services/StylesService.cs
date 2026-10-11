using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;

internal class StylesService
{
    // AssetDatabase.FindAssets scans the whole project; every component asked for its sheet on every build.
    private static readonly Dictionary<string, StyleSheet> cache = new Dictionary<string, StyleSheet>();

    internal static StyleSheet GetStyleSheet(string name)
    {
        if (cache.TryGetValue(name, out var cached) && cached != null) return cached;

        var sheet = FindStyleSheet(name);
        if (sheet != null) cache[name] = sheet;
        return sheet;
    }

    private static StyleSheet FindStyleSheet(string name)
    {
        if (name.Contains("ViewModel"))
        {
            name = name.Substring(0, name.Length - 9);
            name += "Component";
        }
        // FindAssets matches substrings, so pick the exact file name when there is one.
        var guids = AssetDatabase.FindAssets(name + " t:StyleSheet");
        var guid = guids.FirstOrDefault(g =>
            System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name) ?? guids.First();
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (!string.IsNullOrEmpty(path))
        {
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
        } else
        {
            return null;
        } 
    }
}