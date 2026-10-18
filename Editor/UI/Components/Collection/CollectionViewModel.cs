using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MoreLinq;
using UniRx;
using UniRxExtension;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

internal class CollectionViewModel<T> : VisualElement, ITabActions where T : AiObjectModel
{
    private readonly CompositeDisposable subscriptions = new CompositeDisposable();
    private readonly CompositeDisposable listViewSubscriptions = new CompositeDisposable();
    private readonly CompositeDisposable nameChangeSubscriptions = new CompositeDisposable();
    private IDisposable collectionUpdatedSub;

    private readonly TemplateContainer root;

    private readonly Button sortCollectionButton;
    private VisualElement tempHeader;
    private readonly Button addButton;
    private List<string> addChoices = new List<string>();

    //private Label titleLabel;
    private readonly ScrollView elementsBody;

    private ReactiveList<T> collection;
    private readonly ReactiveList<AiObjectModel> templates;
    private readonly VisualElement dropdownContainer;

    private readonly HashSet<T> unfoldedElements = new HashSet<T>();
    private readonly List<AiObjectViewModel> expandedList = new List<AiObjectViewModel>();
    private readonly List<MainWindowFoldedViewModel> foldedList = new List<MainWindowFoldedViewModel>();

    /// <summary>The sort and add buttons; a tab view moves them onto its tab strip.</summary>
    public VisualElement TabActions => tempHeader;
    public string TabTypeClass => TypeStyleService.GetTypeClass(typeof(T));

    private string typeLabel = "";
    private Action sortAction;

    public void FillContextMenu(ContextualMenuPopulateEvent evt)
    {
        // A click on one of this list's items belongs to that item's menu (see ListViewModel).
        var origin = evt.triggerEvent?.target as VisualElement;
        var sawItem = false;
        for (var e = origin; e != null; e = e.parent)
        {
            if (e == this)
            {
                if (sawItem) return;
                break;
            }
            if (e is ListViewModel) sawItem = true;
        }

        var position = evt.mousePosition;
        evt.menu.AppendAction("+ " + typeLabel, _ => OpenAddList(position));
        if (sortAction != null)
        {
            evt.menu.AppendAction(sortCollectionButton.text, _ => sortAction());
        }
        evt.menu.AppendSeparator();
        evt.menu.AppendAction("Collapse all", _ => SetAllFolded(true));
        evt.menu.AppendAction("Expand all", _ => SetAllFolded(false));
        evt.StopPropagation();
    }

    // The same actions as the buttons on the tab strip: add and sort.
    public void AppendListActions(Action<string, Action> add, Vector2 position)
    {
        add("+ " + typeLabel, () => OpenAddList(position));
        if (sortAction != null)
        {
            add(sortCollectionButton.text, () => sortAction());
        }
    }

    public void OpenAdd(Vector2 panelPosition) => OpenAddList(panelPosition);

    private void OpenAddList(Vector2 panelPosition)
    {
        SearchableDropdown.Show(panelPosition, 280f, addChoices, null, picked => OnAddPicked(picked));
    }

    internal void SetAllFolded(bool folded)
    {
        foreach (var element in collection.Values) SetUnfolded(element, !folded);
        if (collection.Count > 0) UpdateCollection();
    }

    // Scroll position survives reloads. A list has no id, so it is keyed by its type and its first element.
    private string ScrollKey => collection != null && collection.Count > 0 ? typeof(T).Name + "." + collection.Values[0].Guid : null;
    // Rebuilding the list resets the scroller; nothing is saved until the remembered offset has been applied.
    private bool scrollRestored;
    private IVisualElementScheduledItem scrollSave;

    private void SaveScrollSoon()
    {
        if (!scrollRestored) return;
        // Debounced: EditorPrefs is the registry on Windows, too slow to write on every scroll tick.
        scrollSave ??= elementsBody.schedule.Execute(() =>
        {
            var key = ScrollKey;
            if (key != null) TemplateManagerStateService.SetScroll(key, elementsBody.scrollOffset.y);
        });
        scrollSave.ExecuteLater(400);
    }

    private void RestoreScroll()
    {
        if (scrollRestored) return;
        var key = ScrollKey;
        if (key == null) return;

        var saved = TemplateManagerStateService.GetScroll(key);
        if (saved <= 0f)
        {
            scrollRestored = true;
            return;
        }
        // The content has no size until the first layout pass.
        EventCallback<GeometryChangedEvent> apply = null;
        apply = _ =>
        {
            if (elementsBody.contentContainer.layout.height <= 0f) return;
            elementsBody.contentContainer.UnregisterCallback(apply);
            elementsBody.scrollOffset = new Vector2(0f, saved);
            scrollRestored = true;
        };
        elementsBody.contentContainer.RegisterCallback(apply);
    }

    // The fold state is also kept in EditorPrefs (by guid), so items reopen as they were after a script reload.
    private void SetUnfolded(T element, bool unfolded)
    {
        if (unfolded) unfoldedElements.Add(element);
        else unfoldedElements.Remove(element);
        TemplateManagerStateService.SetUnfolded(element.Guid, unfolded);
    }

    private bool IsUnfolded(T element)
    {
        return unfoldedElements.Contains(element) || TemplateManagerStateService.IsUnfolded(element.Guid);
    }

    public IObservable<bool> OnSortClicked => onSortClicked;
    private readonly Subject<bool> onSortClicked = new Subject<bool>();

    public CollectionViewModel(ReactiveList<AiObjectModel> templates, string tempLabel, string elementsLabel, string dropDownLabel = "Templates")
    {
        root = AssetService.GetTemplateContainer(GetType());
        Add(root);
        root.styleSheets.Add(StylesService.GetStyleSheet("CollectionComponent"));
        // No gap between the tab strip and the first item.
        style.marginTop = 0;
        style.marginLeft = 0; // AiObjectComponent.uss indents every collection by 12px
        root.Q<VisualElement>("ElementsContainer").style.marginTop = 0;

        this.collection = new ReactiveList<T>();
        this.templates = templates;

        sortCollectionButton = root.Q<Button>("SortCollection-Button");
        elementsBody = root.Q<ScrollView>("ElementsBody");
        elementsBody.verticalScroller.valueChanged += _ => SaveScrollSoon();
        elementsBody.contentContainer.style.paddingTop = 0;
        elementsBody.contentContainer.style.paddingBottom = 0;
        elementsBody.contentContainer.style.paddingLeft = 0;
        elementsBody.contentContainer.style.paddingRight = 0;
        tempHeader = root.Q<VisualElement>("TempHeader");
        dropdownContainer = root.Q<VisualElement>("DropdownContainer");
        // A "+" button pinned to the right; it opens the searchable list of things that can be added.
        typeLabel = tempLabel;
        this.AddManipulator(new ContextualMenuManipulator(FillContextMenu));
        addButton = new Button { text = "+ " + tempLabel, tooltip = "Add " + tempLabel + " (searchable list)" };
        addButton.style.marginRight = 0;
        addButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        dropdownContainer.Add(addButton);
        SearchableDropdown.Attach(addButton,
            () => addChoices,
            () => null,
            picked => OnAddPicked(picked));


        this.templates = templates;
        this.templates.OnValueChanged
            .Subscribe(temps =>
            {
                InitAddCopyPopup();
                nameChangeSubscriptions.Clear();
                foreach(var t in temps)
                {
                    t
                    .OnNameChanged
                    .Subscribe(t =>
                    {
                        InitAddCopyPopup();
                    })
                    .AddTo(nameChangeSubscriptions);
                }
            })
            .AddTo(subscriptions);

        InitAddCopyPopup();

        var t = collection.GetType();
        if (t == typeof(ReactiveList<Consideration>))
        {
            sortCollectionButton.text = Consts.Text_Button_SortByPerformance;
            sortAction = () => onSortClicked.OnNext(true);
            sortCollectionButton.RegisterCallback<MouseUpEvent>(evt => sortAction());
        }
        else if (t == typeof(ReactiveList<Bucket>))
        {
            sortCollectionButton.text = Consts.Text_Button_SortByWeight;
            sortAction = () =>
            {
                var cast = collection as ReactiveList<Bucket>;
                var sortedList = cast.Values.OrderByDescending(b => b.Weight.Value).ToList();
                cast.Clear();
                cast.Add(sortedList);
            };
            sortCollectionButton.RegisterCallback<MouseUpEvent>(evt => sortAction());
        }
        else
        {
            sortCollectionButton.style.display = DisplayStyle.None;
        }

        TemplateService.Instance.OnIncludeDemosChanged
            .Subscribe(value =>
            {
                InitAddCopyPopup();
            })
            .AddTo(subscriptions);
    }

    private async void OnAddPicked(string picked)
    {
        if (picked != null &&
            picked != Consts.LineBreakBaseTypes &&
            picked != Consts.LineBreakTemplates &&
            picked != Consts.LineBreakDemos)
        {
            await AddCopy(picked);
        }
    }

    private void InitAddCopyPopup()
    {
        addChoices = AddCopyService.GetChoices(typeof(T), templates.Values);
    }

    private async Task AddCopy(string aiObjectName)
    {
        var element = await AddCopyService.GetAiObjectClone(aiObjectName, templates.Values);
        if (element is not T typed)
        {
            Debug.LogWarning("UAI: could not create \"" + aiObjectName + "\" as a " + typeLabel + ".");
            return;
        }

        SetUnfolded(typed, true); // a new item opens expanded so it can be filled in straight away
        AddElement(typed);
    }

    internal void AddElement(T element)
    {
        collection.Add(element);
    }

    internal void SetElements(ReactiveList<T> elements)
    {

        this.collection = elements;
        collectionUpdatedSub?.Dispose();
        collectionUpdatedSub = collection.OnValueChanged
            .Subscribe(_ => UpdateCollection());
        UpdateCollection();
    }

    private void UpdateCollection()
    {
        //Debug.LogWarning("This could be more effective by using a pool");
        elementsBody.Clear();
        listViewSubscriptions.Clear();
        if (collection.Values.Count > expandedList.Count)
        {
            var diff = collection.Values.Count - expandedList.Count;
            var type = collection.Values[0].GetType();
            for(var i = 0; i < diff; i++)
            {
                var expanded = MainWindowService.Instance.GetAiObjectComponent(type);
                expandedList.Add(expanded);
                var folded = MainWindowService.Instance.GetMainWindowFoldedComponent();
                foldedList.Add(folded);
            }
        }
        for (var i = 0; i < collection.Values.Count; i++)
        {
            var element = collection.Values[i];
            var folded = foldedList[i];
            folded.style.display = DisplayStyle.Flex;

            var expanded = expandedList[i];
            expanded.style.display = DisplayStyle.Flex;

            var listView = new ListViewModel { ExtendMenu = AppendListActions };
            // Zebra stripes tinted with the tab's type colour (see UaiTheme.uss); every rebuild recreates the rows, so parity stays right.
            listView.styleSheets.Add(StylesService.GetStyleSheet("UaiTheme"));
            if (!string.IsNullOrEmpty(TabTypeClass)) listView.AddToClassList(TabTypeClass);
            listView.EnableInClassList("uai-row--odd", i % 2 == 1);
            // The expanded editor is built lazily, only when its item is open (see FoldableComponent).
            folded.UpdateUi(element);
            expanded.Model = element;
            // Keep each item's fold state across rebuilds (reorder, sort, add, remove).
            var startFolded = !IsUnfolded(element);
            listView.UpdateUi(expanded, folded, startFolded);

            listView.OnRemoveClicked
                .Subscribe(_ =>
                {
                    SetUnfolded(element, false);
                    collection.Remove(element);
                })
                .AddTo(listViewSubscriptions);

            listView.OnCollapseAll
                .Subscribe(_ => SetAllFolded(true))
                .AddTo(listViewSubscriptions);

            listView.OnExpandAll
                .Subscribe(_ => SetAllFolded(false))
                .AddTo(listViewSubscriptions);

            listView.OnMoveUp
                .Subscribe(_ => collection.DecreaseIndex(element))
                .AddTo(listViewSubscriptions);

            listView.OnMoveDown
                .Subscribe(_ => collection.IncreaIndex(element))
                .AddTo(listViewSubscriptions);

            listView.OnDuplicate
                .Subscribe(async _ =>
                {
                    var copy = await element.CloneAsync();
                    AddElement(copy as T);
                })
                .AddTo(listViewSubscriptions);

            listView.OnFoldChanged
                .Subscribe(isFolded =>
                {
                    SetUnfolded(element, !isFolded);
                })
                .AddTo(listViewSubscriptions);

            listView.OnDragMoved
                .Subscribe(pos => ShowDropIndicator(GetDropSlot(pos)))
                .AddTo(listViewSubscriptions);

            listView.OnDragEnded
                .Subscribe(pos => DropElement(element, pos))
                .AddTo(listViewSubscriptions);
            elementsBody.Add(listView);
        }

        RestoreScroll();

        if(expandedList.Count > collection.Count)
        {
            for(var i = collection.Count; i < expandedList.Count; i++)
            {
                expandedList[i].style.display = DisplayStyle.None;
                foldedList[i].style.display = DisplayStyle.None;
            }
        }
    }

    /// <summary>Index the dragged item would be inserted before (Count = after the last item); -1 if the pointer is invalid.</summary>
    private int GetDropSlot(Vector2 panelPosition)
    {
        if (float.IsNaN(panelPosition.y)) return -1;
        var rows = elementsBody.contentContainer.Children().OfType<ListViewModel>().ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            if (panelPosition.y < rows[i].worldBound.center.y) return i;
        }
        return rows.Count;
    }

    private void ShowDropIndicator(int slot)
    {
        var rows = elementsBody.contentContainer.Children().OfType<ListViewModel>().ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].SetDropIndicator(slot == i, slot == rows.Count && i == rows.Count - 1);
        }
    }

    private void DropElement(T element, Vector2 panelPosition)
    {
        var slot = GetDropSlot(panelPosition);
        ShowDropIndicator(-1);
        if (slot < 0) return;

        var from = collection.Values.IndexOf(element);
        if (from < 0) return;
        // Removing the item first shifts every later slot down by one.
        var to = slot > from ? slot - 1 : slot;
        collection.MoveTo(element, to);
    }

    private void ClearSubscriptions()
    {
        collectionUpdatedSub?.Dispose();
        listViewSubscriptions.Clear();
        subscriptions.Clear();
        nameChangeSubscriptions?.Clear();
    }

    ~CollectionViewModel()
    {
        // foreach(var comp in expandedList)
        // {
        //     MainWindowService.Instance.ReturnComponent(comp);
        // }
        //  foreach(var comp in foldedList)
        // {
        //     MainWindowService.Instance.ReturnMWFC(comp);
        // }
        ClearSubscriptions();
    }
}