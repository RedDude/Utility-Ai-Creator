using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UniRx;
using System;

public class ListViewModel : VisualElement
{
    private const string DraggingClass = "list-item--dragging";
    private const string DropBeforeClass = "list-item--drop-before";
    private const string DropAfterClass = "list-item--drop-after";

    private readonly TemplateContainer root;

    private readonly Foldout foldToggle;
    private readonly Button menuButton;
    private readonly VisualElement dragHandle;
    private readonly VisualElement itemHeader;
    private AiObjectModel itemModel;

    public IObservable<bool> OnRemoveClicked => onRemoveClicked;
    private readonly Subject<bool> onRemoveClicked = new Subject<bool>();

    /// <summary>Lets the owning list add its own actions (add, sort) so the menu matches the buttons above the list.</summary>
    internal Action<Action<string, Action>, Vector2> ExtendMenu;

    public IObservable<bool> OnCollapseAll => onCollapseAll;
    private readonly Subject<bool> onCollapseAll = new Subject<bool>();

    public IObservable<bool> OnExpandAll => onExpandAll;
    private readonly Subject<bool> onExpandAll = new Subject<bool>();

    public IObservable<bool> OnMoveUp => onMoveUp;
    private readonly Subject<bool> onMoveUp = new Subject<bool>();

    public IObservable<bool> OnMoveDown => onMoveDown;
    private readonly Subject<bool> onMoveDown = new Subject<bool>();

    public IObservable<bool> OnDuplicate => onDuplicate;
    private readonly Subject<bool> onDuplicate = new Subject<bool>();

    /// <summary>Emits true when the item was folded, false when it was expanded.</summary>
    public IObservable<bool> OnFoldChanged => onFoldChanged;
    private readonly Subject<bool> onFoldChanged = new Subject<bool>();

    /// <summary>Pointer position (panel space) while the drag handle is being dragged.</summary>
    public IObservable<Vector2> OnDragMoved => onDragMoved;
    private readonly Subject<Vector2> onDragMoved = new Subject<Vector2>();

    public IObservable<Vector2> OnDragEnded => onDragEnded;
    private readonly Subject<Vector2> onDragEnded = new Subject<Vector2>();

    private readonly VisualElement centerContainer;
    private readonly FoldableComponent FoldableComponent;
    private bool dragging;
    private Label dragGhost;
    private string itemName = "";

    public ListViewModel()
    {
        root = AssetService.GetTemplateContainer(GetType().FullName);
        Add(root);
        foldToggle = root.Q<Foldout>("Fold-Foldout");
        centerContainer = root.Q<VisualElement>("Center");
        dragHandle = root.Q<VisualElement>("DragHandle");
        itemHeader = root.Q<VisualElement>("ItemHeader");

        root.styleSheets.Add(StylesService.GetStyleSheet("ListViewComponent"));
        FoldableComponent = new FoldableComponent();
        centerContainer.Add(FoldableComponent);

        for (var i = 0; i < 3; i++)
        {
            var bar = new VisualElement();
            bar.AddToClassList("drag-bar");
            bar.pickingMode = PickingMode.Ignore;
            dragHandle.Add(bar);
        }

        foldToggle.RegisterValueChangedCallback(evt =>
        {
            // The arrow points down while the item is open.
            if (FoldableComponent.IsFolded == !evt.newValue) return;
            FoldableComponent.Toggle();
            UpdateToggleTooltip();
            onFoldChanged.OnNext(FoldableComponent.IsFolded);
        });

        // One "more actions" button at the right of the row; right-clicking the row opens the same menu.
        menuButton = IconButtonService.CreateMenu("Actions: save to template, duplicate, move, delete");
        menuButton.name = "Menu-Button";
        menuButton.RegisterCallback<ClickEvent>(evt =>
        {
            ShowMenu(menuButton.worldBound);
            evt.StopPropagation();
        });
        itemHeader.Add(menuButton);
        // Right-click anywhere on the item (header, summary, description, expanded editor) opens the item's menu.
        // The enclosing list also has a context menu (add, sort); the item's own populate must run first and stop it.
        this.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            // Every manipulator on the way up reacts to the same mouse-up with its own menu and the last one wins,
            // so decide by where the click really landed (evt.target is only the element the manipulator sits on).
            var origin = evt.triggerEvent?.target as VisualElement ?? evt.target as VisualElement;
            if (!BelongsToThisItem(origin)) return;
            var pos = evt.mousePosition;
            BuildMenu(
                (label, action) => evt.menu.AppendAction(label, _ => action()),
                () => evt.menu.AppendSeparator(),
                pos);
            evt.StopPropagation();
        }));

        // Double-click anywhere on the item's summary expands it (a folded item shows its summary).
        RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.clickCount != 2 || !FoldableComponent.IsFolded) return;
            var target = evt.target as VisualElement;
            if (target is TextField || target is Button || target == dragHandle || target is Foldout) return;
            if (target is Label l && l.ClassListContains(InlineRename.EditableClass)) return; // double-click renames
            if (!BelongsToThisItem(target)) return;
            foldToggle.value = true;
            evt.StopPropagation();
        });

        RegisterDrag();
    }

    // Nested items, nested lists and text fields have menus of their own; only handle clicks that belong to this item.
    private bool BelongsToThisItem(VisualElement target)
    {
        for (var e = target; e != null && e != this; e = e.parent)
        {
            if (e is ListViewModel || e is ITabActions || e is TextField) return false; // labels count as the item itself
            if (e is Button b && b.ClassListContains("uai-tab")) return false; // the tab strip has its own menu
        }
        return true;
    }

    private void ShowMenu(Rect position)
    {
        var menu = new GenericMenu();
        BuildMenu(
            (label, action) => menu.AddItem(new GUIContent(label), false, () => action()),
            () => menu.AddSeparator(""),
            position.center);
        menu.DropDown(position);
    }

    // One entry list for both the ⋮ button (GenericMenu) and right-click (ContextualMenu).
    private void BuildMenu(Action<string, Action> add, Action separator, Vector2 position)
    {
        var folded = FoldableComponent.IsFolded;
        add(folded ? "Expand" : "Collapse", () => foldToggle.value = folded);
        add("Collapse all", () => onCollapseAll.OnNext(true));
        add("Expand all", () => onExpandAll.OnNext(true));
        separator();
        add("Save to template", SaveAsTemplate);
        add("Duplicate", () => onDuplicate.OnNext(true));
        separator();
        add("Move up", () => onMoveUp.OnNext(true));
        add("Move down", () => onMoveDown.OnNext(true));
        separator();
        add("Delete", () => onRemoveClicked.OnNext(true));
        // The item's own action menu: what its inner lists offer (+ Consideration, + Action, sort...).
        var inner = new List<ITabActions>();
        if (FoldableComponent.Expanded != null)
        {
            if (folded) FoldableComponent.Expanded.Touch(); // builds the editor so its lists exist
            CollectTabActions(FoldableComponent.Expanded, inner);
        }
        if (inner.Count > 0)
        {
            separator();
            foreach (var actions in inner) actions.AppendListActions(add, position);
        }
        if (ExtendMenu != null)
        {
            separator();
            ExtendMenu(add, position);
        }
    }

    // Lists directly inside this item; lists of nested items belong to those items.
    private static void CollectTabActions(VisualElement parent, List<ITabActions> result)
    {
        foreach (var child in parent.Children())
        {
            if (child is ListViewModel) continue;
            if (child is ITabActions actions)
            {
                result.Add(actions);
                continue;
            }
            CollectTabActions(child, result);
        }
    }

    private async void SaveAsTemplate()
    {
        if (itemModel == null) return;
        var clone = await itemModel.CloneAsync();
        TemplateService.Instance.Add(clone);
    }

    private void RegisterDrag()
    {
        dragHandle.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            dragging = true;
            dragHandle.CapturePointer(evt.pointerId);
            AddToClassList(DraggingClass);
            ShowGhost(evt.position);
            evt.StopPropagation();
        });

        dragHandle.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging) return;
            MoveGhost(evt.position);
            onDragMoved.OnNext(evt.position);
        });

        dragHandle.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging) return;
            dragging = false;
            dragHandle.ReleasePointer(evt.pointerId);
            RemoveFromClassList(DraggingClass);
            HideGhost();
            onDragEnded.OnNext(evt.position);
        });

        // Capture lost without a pointer-up (window closed, focus change): drop nothing.
        dragHandle.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            if (!dragging) return;
            dragging = false;
            RemoveFromClassList(DraggingClass);
            HideGhost();
            onDragEnded.OnNext(new Vector2(float.NaN, float.NaN));
        });
    }

    // A label with the item's name follows the pointer so it is clear what is being moved.
    private void ShowGhost(Vector2 position)
    {
        if (panel == null) return;
        dragGhost = new Label(string.IsNullOrEmpty(itemName) ? "(unnamed)" : itemName);
        dragGhost.pickingMode = PickingMode.Ignore;
        var s = dragGhost.style;
        s.position = Position.Absolute;
        s.paddingLeft = 8; s.paddingRight = 8; s.paddingTop = 3; s.paddingBottom = 3;
        s.backgroundColor = new Color(0.23f, 0.47f, 0.73f, 0.92f);
        s.color = Color.white;
        s.borderTopLeftRadius = 4; s.borderTopRightRadius = 4; s.borderBottomLeftRadius = 4; s.borderBottomRightRadius = 4;
        s.unityFontStyleAndWeight = FontStyle.Bold;
        panel.visualTree.Add(dragGhost);
        MoveGhost(position);
    }

    private void MoveGhost(Vector2 position)
    {
        if (dragGhost == null) return;
        dragGhost.style.left = position.x + 12;
        dragGhost.style.top = position.y + 8;
    }

    private void HideGhost()
    {
        dragGhost?.RemoveFromHierarchy();
        dragGhost = null;
    }

    /// <summary>Shows or hides the insertion line above or below this item.</summary>
    internal void SetDropIndicator(bool before, bool after)
    {
        EnableInClassList(DropBeforeClass, before);
        EnableInClassList(DropAfterClass, after);
    }

    internal void UpdateUi(AiObjectViewModel expanded, MainWindowFoldedViewModel folded, bool startFolded = true)
    {
        TypeStyleService.Apply(centerContainer, folded.Model, false);
        MoveSummaryIntoHeader(folded);
        itemName = folded.Model?.Name;
        itemModel = folded.Model;
        FoldableComponent.UpdateUi(expanded, folded, startFolded);
        foldToggle.SetValueWithoutNotify(!FoldableComponent.IsFolded);
        UpdateToggleTooltip();
    }

    // One row: [grip] [arrow] [type] [name] [labels] [weight] [x]. Clicking the title toggles the item.
    private void MoveSummaryIntoHeader(MainWindowFoldedViewModel folded)
    {
        var summary = folded.HeaderRow;
        if (summary == null) return;

        summary.style.flexGrow = 1;
        summary.style.flexShrink = 1;
        summary.style.marginBottom = 0;
        itemHeader.Insert(itemHeader.IndexOf(menuButton), summary);

        // The summary element is pooled and reused by later list items: drop the previous item's handler.
        if (summary.userData is EventCallback<ClickEvent> previous)
        {
            summary.UnregisterCallback(previous);
        }
        EventCallback<ClickEvent> onClick = evt =>
        {
            // The name label is edited by double-clicking, so it does not toggle the item.
            var isTitle = evt.target is Label label && !label.ClassListContains(InlineRename.EditableClass);
            if (isTitle || evt.target == summary)
            {
                foldToggle.value = !foldToggle.value;
            }
        };
        summary.userData = onClick;
        summary.RegisterCallback(onClick);
    }

    private void UpdateToggleTooltip()
    {
        foldToggle.tooltip = FoldableComponent.IsFolded ? Consts.Tooltip_Button_Folded : Consts.Tooltip_Button_Expanded;
    }
}
