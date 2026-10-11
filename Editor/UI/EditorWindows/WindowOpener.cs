using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UniRx;
using UniRxExtension;

internal class WindowOpener: EditorWindow
{
    public static Rect windowPosition = new Rect(0f, 0f, 1200f, 624f);
    public static Vector2 windowMinSize = new Vector2(900f, 500f);




    [MenuItem(Consts.MenuName + Consts.Window_TemplateManager_Name)]
    internal static void OpenTemplateManager()
    {
        if (string.IsNullOrEmpty(ProjectSettingsService.Instance.GetCurrentProjectDirectory()))
        {
            var wnd = GetWindow<SelectProjectWindow>();
            wnd.titleContent = new GUIContent(Consts.Window_SelectProject_Name);
            wnd.SetOnComplete(OpenTemplateManagerPrivate);
            wnd.position = windowPosition;
            SetWindowSizeAboveMin(wnd);
            wnd.Show();

        } else
        {
            OpenTemplateManagerPrivate();
        }
    }

    private static void OpenTemplateManagerPrivate()
    {
        var wnd = GetWindow<TemplateManager>();
        wnd.titleContent = new GUIContent(Consts.Window_TemplateManager_Name);
        wnd.position = windowPosition;

        SetWindowSizeAboveMin(wnd);
        wnd.Show();
    }
    
    private const string IncludeDemosMenu = Consts.MenuName + "Include Demo Files";

    [MenuItem(IncludeDemosMenu, false, 200)]
    private static void ToggleIncludeDemos()
    {
        TemplateService.Instance.IncludeDemos = !TemplateService.Instance.IncludeDemos;
    }

    [MenuItem(IncludeDemosMenu, true)]
    private static bool ToggleIncludeDemosValidate()
    {
        Menu.SetChecked(IncludeDemosMenu, TemplateService.Instance.IncludeDemos);
        return true;
    }

    [MenuItem(Consts.MenuName + Consts.Window_RuntimeInspector_Name)]
    public static void OpenRuntimeInspector()
    {
        RunTimeInspector wnd = GetWindow<RunTimeInspector>();
        wnd.titleContent = new GUIContent(Consts.Window_RuntimeInspector_Name);
        wnd.Show();
        wnd.position = windowPosition;
        SetWindowSizeAboveMin(wnd);
    }

    [MenuItem(Consts.MenuName + Consts.Window_Logger_Name)]
    internal static void OpenAiLogger()
    {
        AiLogWindow wnd = GetWindow<AiLogWindow>();
        wnd.titleContent = new GUIContent(Consts.Window_Logger_Name);
        wnd.Show();
        wnd.position = windowPosition;
        SetWindowSizeAboveMin(wnd);
    }

    [MenuItem(Consts.MenuName + Consts.Window_AiTickerManager_Name)]
    public static void OpenAiTickerManager()
    {
        AiTickerSettingsWindow wnd = GetWindow<AiTickerSettingsWindow>();
        wnd.titleContent = new GUIContent(Consts.Window_AiTickerManager_Name);
        wnd.position = windowPosition;
        SetWindowSizeAboveMin(wnd);
        wnd.Show();

    }

    private static void SetWindowSizeAboveMin(EditorWindow wnd)
    {
        wnd.minSize = windowMinSize;

        var pos = wnd.position;
        if (pos.width < windowMinSize.x || pos.height < windowMinSize.y)
        {
            pos.width = Mathf.Max(pos.width, windowMinSize.x);
            pos.height = Mathf.Max(pos.height, windowMinSize.y);
            wnd.position = pos;
        }
    }
}
