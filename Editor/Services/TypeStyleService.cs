using UnityEngine.UIElements;

/// <summary>Colour-codes UAI elements by the type of object they show (see UaiTheme.uss).</summary>
internal static class TypeStyleService
{
    private static readonly string[] AllTypeClasses =
    {
        "uai-type--ai", "uai-type--bucket", "uai-type--decision",
        "uai-type--consideration", "uai-type--action", "uai-type--curve"
    };

    internal static string GetTypeClass(AiObjectModel model)
    {
        switch (model)
        {
            case Uai _: return "uai-type--ai";
            case Bucket _: return "uai-type--bucket";
            case Decision _: return "uai-type--decision";
            case Consideration _: return "uai-type--consideration";
            case AgentAction _: return "uai-type--action";
            case ResponseCurve _: return "uai-type--curve";
            default: return null;
        }
    }

    internal static string GetTypeClass(System.Type type)
    {
        if (typeof(Uai).IsAssignableFrom(type)) return "uai-type--ai";
        if (typeof(Bucket).IsAssignableFrom(type)) return "uai-type--bucket";
        if (typeof(Decision).IsAssignableFrom(type)) return "uai-type--decision";
        if (typeof(Consideration).IsAssignableFrom(type)) return "uai-type--consideration";
        if (typeof(AgentAction).IsAssignableFrom(type)) return "uai-type--action";
        if (typeof(ResponseCurve).IsAssignableFrom(type)) return "uai-type--curve";
        return null;
    }

    /// <param name="asBadge">true for a pill-shaped type label, false for a coloured left bar.</param>
    internal static void Apply(VisualElement element, AiObjectModel model, bool asBadge)
    {
        var theme = StylesService.GetStyleSheet("UaiTheme");
        if (theme != null && !element.styleSheets.Contains(theme))
        {
            element.styleSheets.Add(theme);
        }

        foreach (var c in AllTypeClasses)
        {
            element.RemoveFromClassList(c);
        }

        element.EnableInClassList(asBadge ? "uai-type-badge" : "uai-type-bar", true);
        var typeClass = model == null ? null : GetTypeClass(model);
        if (typeClass != null)
        {
            element.AddToClassList(typeClass);
        }
    }
}
