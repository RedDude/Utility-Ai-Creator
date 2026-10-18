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

/// <summary>
/// Tabs, or with <c>sideBySide</c> one column per group, all open at once. Each column keeps the tab
/// as its header (title, count, the list's add/sort buttons), so callers use it the same way.
/// A handle between two columns resizes them; views with the same <c>layoutKey</c> share (and remember) the widths.
/// </summary>
internal class TabViewViewModel : VisualElement
{
    // Below this width the columns stack instead of squeezing: a nested item (a decision inside a bucket) is narrow.
    private const float StackBelowWidth = 600f;
    // Neither neighbour of a handle can be dragged narrower than this share of the two together.
    private const float MinColumnShare = 0.2f;

    private static readonly Dictionary<string, float[]> sharedWeights = new Dictionary<string, float[]>();
    private static event Action<string> WeightsChanged;

    private readonly bool sideBySide;
    private readonly string layoutKey;
    private readonly List<VisualElement> columns = new List<VisualElement>();
    private readonly List<float> weights = new List<float>(); // flex-grow of each column
    private bool stacked;
    private readonly List<VisualElement> tabActions = new List<VisualElement>();
    private VisualElement actionsSlot;
    private readonly List<Button> tabs = new List<Button>();
    private readonly List<VisualElement> contents = new List<VisualElement>();

    private readonly VisualElement tabContainer;
    private readonly VisualElement content;
    private int selectedIndex = -1;

    public TabViewViewModel(bool sideBySide = false, string layoutKey = null)
    {
        this.sideBySide = sideBySide;
        this.layoutKey = layoutKey;
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

        if (sideBySide)
        {
            tabContainer.style.display = DisplayStyle.None; // every column carries its own header
            this.content.AddToClassList("uai-columns");
            this.content.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var narrow = evt.newRect.width < StackBelowWidth;
                if (stacked == narrow) return;
                stacked = narrow;
                this.content.EnableInClassList("uai-columns--stacked", stacked);
                ApplyWeights();
            });

            if (layoutKey != null)
            {
                // Follow the widths dragged in another view of the same kind while this one is on screen.
                RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    WeightsChanged += OnSharedWeightsChanged;
                    OnSharedWeightsChanged(layoutKey);
                });
                RegisterCallback<DetachFromPanelEvent>(_ => WeightsChanged -= OnSharedWeightsChanged);
            }
        }
    }

    private void OnSharedWeightsChanged(string key)
    {
        if (key != layoutKey) return;
        LoadSharedWeights();
        ApplyWeights();
    }

    private void LoadSharedWeights()
    {
        if (layoutKey == null) return;
        if (!sharedWeights.TryGetValue(layoutKey, out var saved))
        {
            saved = TemplateManagerStateService.GetColumnWeights(layoutKey);
            sharedWeights[layoutKey] = saved;
        }
        if (saved == null || saved.Length != columns.Count) return;

        for (var i = 0; i < saved.Length; i++) weights[i] = saved[i];
    }

    // Stacked columns each take the full width, so the weights only apply side by side.
    private void ApplyWeights()
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (stacked) columns[i].style.flexGrow = StyleKeyword.Null;
            else columns[i].style.flexGrow = weights[i];
        }
    }

    private void SaveWeights()
    {
        if (layoutKey == null) return;
        sharedWeights[layoutKey] = weights.ToArray();
        TemplateManagerStateService.SetColumnWeights(layoutKey, weights);
        WeightsChanged?.Invoke(layoutKey);
    }

    /// <summary>The handle between column <paramref name="leftIndex"/> and the next one; dragging it moves width between the two.</summary>
    private VisualElement CreateSplitter(int leftIndex)
    {
        var splitter = new VisualElement { tooltip = "Drag to resize" };
        splitter.AddToClassList("uai-splitter");
        var line = new VisualElement { pickingMode = PickingMode.Ignore };
        line.AddToClassList("uai-splitter-line");
        splitter.Add(line);

        var dragging = false;
        splitter.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            dragging = true;
            splitter.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });
        splitter.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging) return;
            Resize(leftIndex, evt.position.x);
            evt.StopPropagation();
        });
        splitter.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging) return;
            dragging = false;
            splitter.ReleasePointer(evt.pointerId);
            SaveWeights();
            evt.StopPropagation();
        });
        // Capture lost without a pointer-up (window closed, focus change): keep what was dragged so far.
        splitter.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            if (!dragging) return;
            dragging = false;
            SaveWeights();
        });
        return splitter;
    }

    // The handle follows the pointer; the two columns split their combined width (which a drag never changes).
    private void Resize(int leftIndex, float pointerX)
    {
        var left = columns[leftIndex].worldBound;
        var right = columns[leftIndex + 1].worldBound;
        var handleWidth = right.xMin - left.xMax;
        var span = right.xMax - left.xMin - handleWidth;
        if (span <= 0f) return;

        var share = UnityEngine.Mathf.Clamp((pointerX - left.xMin - handleWidth / 2f) / span, MinColumnShare, 1f - MinColumnShare);
        var total = weights[leftIndex] + weights[leftIndex + 1];
        weights[leftIndex] = total * share;
        weights[leftIndex + 1] = total * (1f - share);
        ApplyWeights();
    }

    private static Button CreateTab(string tabTitle, VisualElement content)
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
        return tab;
    }

    // The area under a tab gets a background in the tab's type colour (see UaiTheme.uss).
    private static void TintBody(VisualElement target, VisualElement body)
    {
        var typeClass = (body as ITabActions)?.TabTypeClass;
        if (string.IsNullOrEmpty(typeClass)) return;
        target.AddToClassList("uai-tab-body");
        target.AddToClassList(typeClass);
    }

    /// <summary>One column: a header (the tab, as a title, plus the list's buttons) above the content.</summary>
    private Button AddColumn(string title, VisualElement body, float weight)
    {
        var tab = CreateTab(title, body);
        tab.AddToClassList("uai-tab--selected"); // not clickable: it only titles the column and carries the type colour

        var header = new VisualElement();
        header.AddToClassList("uai-tab-strip");
        header.AddToClassList("uai-column-header");
        header.style.marginBottom = 0; // no gap between the header and the first item, as in the tab strip
        header.Add(tab);

        var actions = (body as ITabActions)?.TabActions;
        if (actions != null)
        {
            var slot = new VisualElement();
            slot.AddToClassList("uai-tab-actions");
            slot.Add(actions);
            header.Add(slot);
        }

        var column = new VisualElement();
        column.AddToClassList("uai-column");
        column.EnableInClassList("uai-column--first", columns.Count == 0); // USS here has no :first-child
        TintBody(column, body);
        column.Add(header);
        column.Add(body);

        if (columns.Count > 0) content.Add(CreateSplitter(columns.Count - 1));
        columns.Add(column);
        weights.Add(weight);
        content.Add(column);
        LoadSharedWeights();
        ApplyWeights();
        return tab;
    }

    /// <param name="columnWeight">Side by side only: this column's width relative to the others (until the user drags a handle).</param>
    internal Button AddTabGroup(string tabTitle, VisualElement content, int elementCount = -1, float columnWeight = 1f)
    {
        if (sideBySide) return AddColumn(tabTitle, content, columnWeight);

        var tab = CreateTab(tabTitle, content);
        TintBody(content, content);

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
                // Only a click is remembered: the automatic Select(0) while tabs are added must not overwrite it.
                if (layoutKey != null) TemplateManagerStateService.SetSelectedTab(layoutKey, tabTitle);
            }
        });

        // Reopen the tab that was open before the reload.
        if (layoutKey != null && tabs.Count > 1 && tabTitle == TemplateManagerStateService.GetSelectedTab(layoutKey))
        {
            Deselect(selectedIndex);
            Select(tabs.IndexOf(tab));
        }

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