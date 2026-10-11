using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UniRx;
using UnityEngine;


internal class ResponseCurveLcViewModel : VisualElement
{
    private readonly CompositeDisposable functionDisposables = new CompositeDisposable();
    private readonly CompositeDisposable responseCurveDisposables = new CompositeDisposable();
    private readonly CompositeDisposable disposables = new CompositeDisposable();
    private readonly CompositeDisposable toggleDisposable = new CompositeDisposable();

    private readonly Label ownerLabel;
    private ResponseCurve responseCurve;
    private readonly LineChartComponent lineChart;

    public IObservable<ResponseCurve> OnResponseCurveChanged => onResponseCurveChanged;
    private readonly Subject<ResponseCurve> onResponseCurveChanged = new Subject<ResponseCurve>();

    private float Min => (float)responseCurve.MinX;
    private float Max => (float)responseCurve.MaxX;
    private readonly int steps = ConstsEditor.ResponseCurve_Steps;

    private Button foldButton;
    private readonly VisualElement functionsContainer;
    private readonly VisualElement header;
    private readonly VisualElement segmentBar;
    private readonly Button reverseButton;
    private readonly DropdownField curveDropDown;

    private readonly List<VisualElement> functionCards = new List<VisualElement>();
    private readonly List<VisualElement> segmentBlocks = new List<VisualElement>();
    private readonly List<VisualElement> segmentHandles = new List<VisualElement>();
    private int draggedCardIndex = -1;

    private Guid guid;

    public ResponseCurveLcViewModel()
    {
        var root = AssetService.GetTemplateContainer(GetType().FullName);
        Add(root);
        style.flexGrow = 1;
        root.style.flexGrow = 1;
        guid = Guid.NewGuid();
        root.styleSheets.Add(StylesService.GetStyleSheet("ResponseCurve"));
        header = root.Q<VisualElement>("Header");
        ownerLabel = root.Q<Label>("Name-Label");
        foldButton = root.Q<Button>("FoldButton");
        var curveContainer = root.Q<VisualElement>("CurveContainer");
        functionsContainer = root.Q<VisualElement>("FunctionsContainer");
        segmentBar = root.Q<VisualElement>("SegmentBar");
        var headerActions = root.Q<VisualElement>("HeaderActions");

        var addFunctionButton = root.Q<Button>("AddFunctionButton");
        lineChart = new LineChartComponent
        {
            OnSegmentDragged = (index, x) => responseCurve?.MoveSegment(index, x)
        };
        curveContainer.Add(lineChart);

        curveDropDown = root.Q<DropdownField>("ResponseCurve-Dropdown");
        SearchableDropdown.Attach(curveDropDown);
        curveDropDown.RegisterCallback<ChangeEvent<string>>(OnCurveDropdownValueChanged);
        curveDropDown.label = "";
        // Same header as the decision item: a narrow picker, then save-as-template and reverse to its right.
        curveDropDown.style.maxWidth = 160;
        curveDropDown.style.minWidth = 0;
        curveDropDown.style.flexGrow = 0;
        curveDropDown.style.flexShrink = 1;
        curveDropDown.style.marginRight = 0;
        curveDropDown.Q(className: "unity-base-popup-field__input").style.minWidth = 0;

        var saveTemplateButton = IconButtonService.CreateSave("Save this curve as a template");
        saveTemplateButton.RegisterCallback<ClickEvent>(_ => SaveTemplate());
        headerActions.Add(saveTemplateButton);

        reverseButton = new Button { text = "Reverse", tooltip = "Flip the curve (inverse)" };
        reverseButton.style.flexShrink = 0;
        reverseButton.style.marginLeft = 2;
        reverseButton.style.marginRight = 0;
        reverseButton.RegisterCallback<ClickEvent>(_ => ToggleReverse());
        headerActions.Add(reverseButton);

        addFunctionButton.RegisterCallback<ClickEvent>(evt => AddFunction());

        TemplateService.Instance.ResponseCurves.OnValueChanged
            .Subscribe(InitCurveDropDownChoices)
            .AddTo(disposables);

        InitCurveDropDownChoices(TemplateService.Instance.ResponseCurves.Values);
    }

    private async void SaveTemplate()
    {
        if (responseCurve == null) return;
        var clone = await responseCurve.CloneAsync();
        TemplateService.Instance.Add(clone);
    }

    // Appends a new function after the last one.
    private void AddFunction()
    {
        if (responseCurve == null) return;
        var function = AssetService
            .GetFirstInstanceOfType<ResponseFunction>();
        responseCurve.AddResponseFunction(function);
    }

    private void ToggleReverse()
    {
        if (responseCurve == null) return;
        responseCurve.IsInversed = !responseCurve.IsInversed;
    }

    internal void UpdateUi(ResponseCurve rCurve, bool showSelection = true, string newOwnerName = null)
    {
        responseCurve = rCurve;

        IconButtonService.ShowToggled(reverseButton, responseCurve.IsInversed);

        if (newOwnerName != null)
        {
            ownerLabel.text = "Owner: " + newOwnerName;
        }
        else
        {
            ownerLabel.text = "Template";
        }
        if (showSelection)
        {
            curveDropDown.SetValueWithoutNotify(rCurve.Name);
        }
        else
        {
            curveDropDown.SetEnabled(false);
        }

        responseCurveDisposables.Clear();
        responseCurve.OnCurveValueChanged
            .Subscribe(_ =>
            {
                ReDrawChart();
            })
            .AddTo(responseCurveDisposables);

        responseCurve
            .OnParametersChanged
            .Subscribe(_ =>
            {
                ReDrawChart();
            })
            .AddTo(responseCurveDisposables);

        responseCurve.OnFunctionsChanged
            .Subscribe(_ => UpdateFunctionsAndRedrawChart())
            .AddTo(responseCurveDisposables);

        UpdateFunctionsAndRedrawChart();
    }

    private async void OnCurveDropdownValueChanged(ChangeEvent<string> evt)
    {
        if (evt.newValue != null && evt.newValue != Consts.LineBreakBaseTypes &&
            evt.newValue != Consts.LineBreakTemplates && evt.newValue != Consts.LineBreakDemos)
        {
            var rC =
                await AddCopyService.GetAiObjectClone(evt.newValue, TemplateService.Instance.ResponseCurves.Values);

            onResponseCurveChanged.OnNext(rC as ResponseCurve);
        }
        curveDropDown.SetValueWithoutNotify(null);
    }

    private void UpdateFunctionsAndRedrawChart()
    {
        //Debug.LogWarning("This could be more effective by using a pool");
        functionsContainer.Clear();
        functionDisposables.Clear();
        functionCards.Clear();
        foreach (var function in responseCurve.ResponseFunctions)
        {
            var functionIndex = responseCurve.ResponseFunctions.IndexOf(function);
            var card = CreateFunctionCard(function, functionIndex);
            functionCards.Add(card);
            functionsContainer.Add(card);
        }
        RebuildSegmentBar();
        ReDrawChart();
    }

    #region Function cards

    private VisualElement CreateFunctionCard(ResponseFunction function, int functionIndex)
    {
        var card = new VisualElement();
        AddFunctionContextMenu(card, function);
        card.AddToClassList("function-card");
        card.AddToClassList(functionIndex % 2 == 0 ? "function-card--even" : "function-card--odd");
        card.style.borderTopColor = LineChartComponent.SegmentColor(functionIndex);

        var cardHeader = new VisualElement();
        cardHeader.AddToClassList("function-card__header");
        var grip = CreateGrip();
        cardHeader.Add(grip);
        var title = new Label("Function " + (functionIndex + 1));
        title.AddToClassList("function-card__title");
        cardHeader.Add(title);
        card.Add(cardHeader);
        RegisterCardDrag(grip, card, functionIndex);

        var functionComponent = new ResponseFunctionViewModel();
        if (responseCurve.ResponseFunctions.Count <= 1)
        {
            functionComponent.UpdateUi(function, true);
        }
        else
        {
            functionComponent.UpdateUi(function);
        }
        card.Add(functionComponent);

        functionComponent
            .OnRemoveClicked
            .Subscribe(f => responseCurve.RemoveResponseFunction(f))
            .AddTo(functionDisposables);

        functionComponent
            .OnResponseFunctionChanged
            .Subscribe(f =>
            {
                HandleFunctionChanged(function, f);
                functionComponent.UpdateUi(f);
            })
            .AddTo(functionDisposables);

        if (responseCurve.Segments.Count > functionIndex)
        {
            var segmentComponent = new ParameterComponent();
            segmentComponent.AddToClassList("function-card__segment");
            segmentComponent.UpdateUi(responseCurve.Segments[functionIndex]);
            if (segmentComponent.field is BaseField<float> segmentField)
            {
                segmentField.label = "Ends at";
            }
            card.Add(segmentComponent);
        }
        else
        {
            var endLabel = new Label("Runs to the end of the range");
            endLabel.AddToClassList("function-card__end-label");
            card.Add(endLabel);
        }

        return card;
    }

    // Right-click on a function's card or its bar segment: add a function, invert this one or delete it.
    private void AddFunctionContextMenu(VisualElement target, ResponseFunction function)
    {
        target.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            var inverse = function.ParameterContainer.GetParamBool("Inverse");
            evt.menu.AppendAction("Add", _ => AddFunction());
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Invert",
                _ => inverse.Value = !inverse.Value,
                _ => inverse.Value ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("Delete",
                _ => responseCurve.RemoveResponseFunction(function),
                _ => responseCurve.ResponseFunctions.Count > 1
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
        }));
    }

    private static VisualElement CreateGrip()
    {
        var grip = new VisualElement { tooltip = "Drag to reorder" };
        grip.AddToClassList("function-card__grip");
        for (var i = 0; i < 3; i++)
        {
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("function-card__grip-line");
            grip.Add(line);
        }
        return grip;
    }

    private void RegisterCardDrag(VisualElement grip, VisualElement card, int cardIndex)
    {
        grip.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            draggedCardIndex = cardIndex;
            card.AddToClassList("function-card--dragging");
            grip.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });

        grip.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!grip.HasPointerCapture(evt.pointerId)) return;
            ShowDropTarget(DropIndexAt(evt.position.x));
            evt.StopPropagation();
        });

        grip.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!grip.HasPointerCapture(evt.pointerId)) return;
            var dropIndex = DropIndexAt(evt.position.x);
            var fromIndex = draggedCardIndex;
            grip.ReleasePointer(evt.pointerId);
            ClearDragState();
            // Rebuilds the cards through OnFunctionsChanged.
            responseCurve.MoveResponseFunction(fromIndex, dropIndex);
            evt.StopPropagation();
        });

        grip.RegisterCallback<PointerCaptureOutEvent>(_ => ClearDragState());
    }

    // The card whose centre is closest to the pointer is where the dragged function lands.
    private int DropIndexAt(float pointerX)
    {
        var best = 0;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < functionCards.Count; i++)
        {
            var distance = Mathf.Abs(functionCards[i].worldBound.center.x - pointerX);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }
        return best;
    }

    private void ShowDropTarget(int dropIndex)
    {
        foreach (var card in functionCards)
        {
            card.RemoveFromClassList("function-card--drop-before");
            card.RemoveFromClassList("function-card--drop-after");
        }
        if (dropIndex == draggedCardIndex) return;
        functionCards[dropIndex].AddToClassList(dropIndex < draggedCardIndex
            ? "function-card--drop-before"
            : "function-card--drop-after");
    }

    private void ClearDragState()
    {
        draggedCardIndex = -1;
        foreach (var card in functionCards)
        {
            card.RemoveFromClassList("function-card--dragging");
            card.RemoveFromClassList("function-card--drop-before");
            card.RemoveFromClassList("function-card--drop-after");
        }
    }

    #endregion

    #region Segment bar

    private void RebuildSegmentBar()
    {
        segmentBar.Clear();
        segmentBlocks.Clear();
        segmentHandles.Clear();

        var functions = responseCurve.ResponseFunctions;
        for (var i = 0; i < functions.Count; i++)
        {
            var color = LineChartComponent.SegmentColor(i);
            color.a = 0.45f;
            var block = new Label(functions[i].Name) { tooltip = "Function " + (i + 1) };
            block.AddToClassList("segment-block");
            block.style.backgroundColor = color;
            AddFunctionContextMenu(block, functions[i]);
            segmentBlocks.Add(block);
            segmentBar.Add(block);
        }

        // Handles go in last so they sit above the blocks.
        for (var i = 0; i < responseCurve.Segments.Count && i < functions.Count - 1; i++)
        {
            var handle = new VisualElement { tooltip = "Drag to move where function " + (i + 1) + " ends" };
            handle.AddToClassList("segment-handle");
            RegisterSegmentHandleDrag(handle, i);
            segmentHandles.Add(handle);
            segmentBar.Add(handle);
        }
    }

    private void RegisterSegmentHandleDrag(VisualElement handle, int segmentIndex)
    {
        handle.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            handle.AddToClassList("segment-handle--active");
            handle.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });

        handle.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!handle.HasPointerCapture(evt.pointerId)) return;
            var inner = segmentBar.contentRect;
            if (inner.width <= 0) return;
            var fraction = Mathf.Clamp01((segmentBar.WorldToLocal(evt.position).x - inner.x) / inner.width);
            responseCurve.MoveSegment(segmentIndex, Min + fraction * (Max - Min));
            evt.StopPropagation();
        });

        handle.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!handle.HasPointerCapture(evt.pointerId)) return;
            handle.ReleasePointer(evt.pointerId);
            handle.RemoveFromClassList("segment-handle--active");
            evt.StopPropagation();
        });

        handle.RegisterCallback<PointerCaptureOutEvent>(_ => handle.RemoveFromClassList("segment-handle--active"));
    }

    private void PositionSegmentBar()
    {
        var range = Max - Min;
        if (range <= 0) return;

        var from = Min;
        for (var i = 0; i < segmentBlocks.Count; i++)
        {
            var to = i < responseCurve.Segments.Count ? responseCurve.Segments[i].Value : Max;
            if (i == segmentBlocks.Count - 1) to = Max;
            segmentBlocks[i].style.left = Length.Percent((from - Min) / range * 100f);
            segmentBlocks[i].style.width = Length.Percent((to - from) / range * 100f);
            from = to;
        }

        for (var i = 0; i < segmentHandles.Count; i++)
        {
            segmentHandles[i].style.left = Length.Percent((responseCurve.Segments[i].Value - Min) / range * 100f);
        }
    }

    #endregion

    private void HandleFunctionChanged(ResponseFunction oldFunction, ResponseFunction newFunction)
    {
        responseCurve.UpdateFunction(oldFunction, newFunction);
    }

    private void InitCurveDropDownChoices(List<AiObjectModel> responseCurveTemplates)
    {
        curveDropDown.choices = AddCopyService.GetChoices(typeof(ResponseCurve), responseCurveTemplates);
        curveDropDown.SetValueWithoutNotify(null);
    }

    private void ReDrawChart()
    {
        var points = new List<Vector2>();
        var stepSize = (Max - Min) / steps;
        for (var i = 0; i <= steps; i++)
        {
            var x = i * stepSize + Min;
            var y = responseCurve.CalculateResponse(x);
            points.Add(new Vector2(x, y));
        }

        IconButtonService.ShowToggled(reverseButton, responseCurve.IsInversed);
        PositionSegmentBar();
        lineChart?.SetSegments(responseCurve.Segments.Select(s => s.Value));
        lineChart?.DrawCurve(points, Min, Max);
        onResponseCurveChanged.OnNext(responseCurve);
    }

    /// <summary>Puts an element at the left end of the header row (before the owner label).</summary>
    internal void InsertInHeaderStart(VisualElement element)
    {
        header.Insert(0, element);
        // Owner label takes the free space so the picker and buttons stay on the right.
        ownerLabel.style.flexGrow = 1;
        ownerLabel.style.flexShrink = 1;
        ownerLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
    }

    /// <summary>Stops reacting to the curve and the template list; call when the editor is closed.</summary>
    internal void ReleaseSubscriptions()
    {
        functionDisposables.Clear();
        responseCurveDisposables.Clear();
        disposables.Clear();
        toggleDisposable.Clear();
    }

    ~ResponseCurveLcViewModel(){
        functionDisposables.Clear();
        responseCurveDisposables.Clear();
        disposables.Clear();
        toggleDisposable.Clear();
    }
}
