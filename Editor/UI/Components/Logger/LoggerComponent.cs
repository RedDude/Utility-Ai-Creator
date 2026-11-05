using UnityEditor;
using UnityEngine.UIElements;
using UniRx;

internal class LoggerComponent : RightPanelComponent<IAgent>
{
    private readonly CompositeDisposable disposables = new CompositeDisposable();
    
    private readonly TemplateContainer root;

    private LoggerState state;

    private bool isPlaying => EditorApplication.isPlaying;
    private bool isPaused => EditorApplication.isPaused;
    private IAgent agent;
    internal int CurrentTick;

    private readonly Button foldoutButton;

    private bool isExpanded;

    private const string ExpandAllKey = "Logger.ExpandAll";
    private const string ColorKey = "Logger.Color";
    private const string RecordKey = "Logger.Record";

    /// <summary>The last Expand All / Collapse All the user chose (null when never chosen), so new foldouts follow it.</summary>
    internal static bool? RememberedExpandAll
    {
        get
        {
            var saved = TemplateManagerStateService.GetText(ExpandAllKey);
            return saved.Length == 0 ? (bool?) null : saved == "1";
        }
    }

    public LoggerComponent()
    {
        root = AssetService.GetTemplateContainer(GetType().FullName);
        Add(root);
        root.styleSheets.Add(StylesService.GetStyleSheet("Logger"));
        var backLeapButton = root.Q<Button>("BackLeapButton");
        var backStepButton = root.Q<Button>("BackStepButton");
        var toggleStateButton = root.Q<Button>("ToggleButton");
        var forwardStepButton = root.Q<Button>("ForwardStepButton");
        var forwardLeapButton = root.Q<Button>("ForwardLeapButton");
        var body = root.Q<VisualElement>("Body");
        var footer = root.Q<VisualElement>("Footer");
        var tickSlider = root.Q<SliderInt>("Tick-Slider");
        foldoutButton = root.Q<Button>("Foldout-Button");
        var helpBox = new HelpBox
        {
            style =
            {
                display = DisplayStyle.None
            }
        };
        body.Add(helpBox);

        var agentLogComponent = new AgentLogViewModel();
        body.Add(agentLogComponent);
        agentLogComponent.style.display = DisplayStyle.None;

        var tickAgent = new Button
        {
            text = ConstsEditor.Button_TickAgent_Text,
            name = ConstsEditor.Button_TickAgent_Name
        };
        tickAgent.RegisterCallback<MouseUpEvent>(_ =>
        {
            if (agent == null) return;
            UaiTicker.Instance.TickAgent(agent);
        });
        footer.Add(tickAgent);


        backLeapButton.RegisterCallback<MouseUpEvent>(_ =>
        {
            state.BackLeapButtonPressed();
        });

        backStepButton.RegisterCallback<MouseUpEvent>(_ =>
        {
            state.BackStepButtonPressed();
        });

        toggleStateButton.RegisterCallback<MouseUpEvent>(_ =>
        {
            state.ToggleStateButtonPressed();
        });

        forwardStepButton.RegisterCallback<MouseUpEvent>(_ =>
        {
            state.ForwardStepButtonPressed();
        });

        forwardLeapButton.RegisterCallback<MouseUpEvent>(_ =>
        {
            state.ForwardLeapButtonPressed();
        });
        
        tickSlider.RegisterCallback<ChangeEvent<int>>(evt =>
        {
            state.TickSliderChanged(evt.newValue);
        });

        foldoutButton.RegisterCallback<MouseUpEvent>(_ =>
        {
            ToggleFoldStatus(isExpanded);
        });

        isExpanded = RememberedExpandAll ?? false;
        SetFoldButtonText();

        // Color and Record come back the way the user left them.
        var colorToggle = root.Q<Toggle>("Color-Toggle");
        var recordToggle = root.Q<Toggle>("Record-Toggle");
        colorToggle.SetValueWithoutNotify(TemplateManagerStateService.GetText(ColorKey) != "0");
        recordToggle.SetValueWithoutNotify(TemplateManagerStateService.GetText(RecordKey) != "0");
        colorToggle.RegisterCallback<ChangeEvent<bool>>(evt => TemplateManagerStateService.SetText(ColorKey, evt.newValue ? "1" : "0"));
        recordToggle.RegisterCallback<ChangeEvent<bool>>(evt => TemplateManagerStateService.SetText(RecordKey, evt.newValue ? "1" : "0"));

        EditorApplication.pauseStateChanged += _ => UpdateGameState();
        
        UpdateGameState();
    }

    private void ToggleFoldStatus(bool fold)
    {
        isExpanded = !fold;
        TemplateManagerStateService.SetText(ExpandAllKey, isExpanded ? "1" : "0");
        root
            //.Query<BucketLogComponent>()
            .Query<Foldout>("LoggerFoldout")
            .ForEach(foldout => foldout.value = isExpanded);
        SetFoldButtonText();
    }

    private void SetFoldButtonText()
    {
        foldoutButton.text = isExpanded ? "Collapse All" : "Expand All";
    }

    private void SetState(LoggerState newLoggerState)
    {
        this.state?.OnExit();
        this.state = newLoggerState;
        this.state.OnEnter(agent);
    }

    internal override void UpdateUi(IAgent element)
    {
        this.agent = element;
        state.UpdateUi(agent);
    }

    internal void KeyPressed(KeyDownEvent key)
    {
        state.KeyPressed(key);
    }

    private void UpdateGameState()
    {
        if (isPlaying && !isPaused)
        {
            SetState(new LoggerGameRunning(root, this));
        }
        else if (isPlaying && isPaused)
        {
            SetState(new LoggerGamePaused(root, this));
        }
        else
        {
            SetState(new LoggerGameStopped(root, this));
        }
    }

    ~LoggerComponent()
    {
        disposables.Clear();
    }
}

