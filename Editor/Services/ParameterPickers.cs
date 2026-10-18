using System;
using System.Collections.Generic;

/// <summary>
/// Lets a game give a text parameter a list to pick from (a Yarn node, a tag, a scene...). The parameter keeps its text field
/// and gains a "Pick" button that opens a searchable list. UAS does not know where the choices come from: the game registers a
/// provider once (an [InitializeOnLoad] class, since the list is empty after every script reload).
/// </summary>
public static class ParameterPickers
{
    private static readonly List<Func<AiObjectModel, string, Func<IList<string>>>> providers =
        new List<Func<AiObjectModel, string, Func<IList<string>>>>();

    /// <summary>
    /// The provider gets the object and the parameter name and answers with a function that lists the choices, or null when this
    /// parameter has no picker. The answer is cheap (a type check); the function runs only when the button is clicked, so it
    /// may scan assets.
    /// </summary>
    public static void Register(Func<AiObjectModel, string, Func<IList<string>>> provider)
    {
        if (provider != null && !providers.Contains(provider)) providers.Add(provider);
    }

    internal static Func<IList<string>> Find(AiObjectModel owner, string parameterName)
    {
        if (owner == null || string.IsNullOrEmpty(parameterName)) return null;

        foreach (var provider in providers)
        {
            var choices = provider(owner, parameterName);
            if (choices != null) return choices;
        }
        return null;
    }
}
