using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UIElements;

/// <summary>Content that has buttons (sort, add...) which a tab view shows on the tab strip, next to the tabs.</summary>
internal interface ITabActions
{
    VisualElement TabActions { get; }

    /// <summary>Type colour class (see UaiTheme.uss) so the tab matches the objects it holds; may be null.</summary>
    string TabTypeClass { get; }

    /// <summary>Entries for the context menu of the tab and of its content (add, sort, collapse all...).</summary>
    void FillContextMenu(ContextualMenuPopulateEvent evt);

    /// <summary>The same add/sort actions for a GenericMenu (an item's menu lists the actions of the lists inside it).</summary>
    /// <summary>Opens the "+ type" picker at a panel position (double-clicking the tab does this).</summary>
    void OpenAdd(UnityEngine.Vector2 panelPosition);

    void AppendListActions(Action<string, Action> add, UnityEngine.Vector2 position);
}

internal class TabViewViewModel : VisualElement
{
    private readonly List<VisualElement> tabActions = new List<VisualElement>();
    private VisualElement actionsSlot;
    private readonly List<Button> tabs = new List<Button>();
    private readonly List<VisualElement> contents = new List<VisualElement>();

    private readonly VisualElement tabContainer;
    private readonly VisualElement content;
    private int selectedIndex = -1;

    public TabViewViewModel()
    {
        var root = AssetService.GetTemplateContainer(GetType().FullName);
        Add(root);
        tabContainer = root.Q<VisualElement>("Tabs");
        // Inline: AiObjectComponent.uss gives #Tabs 8px margins, and an ID rule beats the tab-strip class.
        tabContainer.style.marginTop = 0;
        tabContainer.style.marginBottom = 0;
        content = root.Q<VisualElement>("TabContent");
        content.style.marginTop = 0;
        content.style.marginBottom = 0;
        content.style.marginLeft = 0;
        content.style.marginRight = 0;
        content.style.paddingTop = 0;
        content.style.paddingBottom = 0;
        content.style.paddingLeft = 0;
        content.style.paddingRight = 0;
        styleSheets.Add(StylesService.GetStyleSheet("UaiTheme"));

        // Right end of the tab strip: holds the actions of the open tab.
        actionsSlot = new VisualElement();
        actionsSlot.AddToClassList("uai-tab-actions");
        tabContainer.Add(actionsSlot);
    }

    internal Button AddTabGroup(string tabTitle, VisualElement content, int elementCount = -1)
    {
        var tab = new Button();
        tab.text = tabTitle;
        tab.AddToClassList("uai-tab");
        if (content is ITabActions tabContent)
        {
            if (!string.IsNullOrEmpty(tabContent.TabTypeClass)) tab.AddToClassList(tabContent.TabTypeClass);
            tab.AddManipulator(new ContextualMenuManipulator(tabContent.FillContextMenu));
            tab.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount != 2) return;
                var b = tab.worldBound;
                tabContent.OpenAdd(new UnityEngine.Vector2(b.xMin, b.yMax));
            });
        }

        tabs.Add(tab);
        tabContainer.Insert(tabContainer.IndexOf(actionsSlot), tab);

        var actions = (content as ITabActions)?.TabActions;
        tabActions.Add(actions);
        if (actions != null)
        {
            actions.style.display = DisplayStyle.None;
            actionsSlot.Add(actions);
        }

        contents.Add(content);
        this.content.Add(content);
        if (selectedIndex < 0)
        {
            Select(0);
        } else
        {
            Deselect(tabs.IndexOf(tab));
        }

        tab.RegisterCallback<MouseUpEvent>(evt =>
        {
            var index = tabs.IndexOf(tab);
            if (selectedIndex == index)
            {
                return; // like a real tab, clicking the open one does nothing
            } else
            {
                Deselect(selectedIndex);
                Select(tabs.IndexOf(tab));
            }
        });

        return tab;
    }

    private void Deselect(int index)
    {
        //if (index != selectedIndex) return;
        if (contents.Count <= index || index < 0) return;
        contents[index].style.display = DisplayStyle.None;
        if (tabActions[index] != null) tabActions[index].style.display = DisplayStyle.None;
        tabs[index].RemoveFromClassList("uai-tab--selected");
    }

    private void Select(int index)
    {
        if (index == selectedIndex) return;

        selectedIndex = index;
        contents[index].style.display = DisplayStyle.Flex;
        if (tabActions[index] != null) tabActions[index].style.display = DisplayStyle.Flex;
        tabs[index].AddToClassList("uai-tab--selected");

    }
}