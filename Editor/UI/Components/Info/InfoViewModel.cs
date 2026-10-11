using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Shows an object's info or warning as a callout (HelpBox) and hides itself when there is nothing to say.</summary>
internal class InfoViewModel: VisualElement
{
    private readonly TemplateContainer root;
    private readonly HelpBox callout;

    internal InfoViewModel()
    {
        root = AssetService.GetTemplateContainer(GetType().FullName);
        Add(root);
        root.Q<Label>("InfoText").style.display = DisplayStyle.None;

        callout = new HelpBox("", HelpBoxMessageType.Info);
        callout.style.marginTop = 2;
        callout.style.marginBottom = 2;
        root.Add(callout);
        style.display = DisplayStyle.None;
    }

    internal void DispalyInfo(InfoModel info)
    {
        if (string.IsNullOrEmpty(info.Info))
        {
            style.display = DisplayStyle.None;
            return;
        }

        callout.messageType = info.InfoType == InfoTypes.Warning ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
        callout.text = info.Info;
        style.display = DisplayStyle.Flex;
    }
}
