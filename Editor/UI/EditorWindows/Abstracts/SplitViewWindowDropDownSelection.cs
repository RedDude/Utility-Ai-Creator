using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UniRx;
using UniRxExtension;
using UnityEditor.UIElements;

internal abstract class SplitViewWindowDropDownSelection<T> : EditorWindow
{
    private readonly CompositeDisposable elementNameUpdatedSub = new CompositeDisposable();

    private VisualElement root;
    private VisualElement leftContainer;
    private VisualElement rightContainer;
    private VisualElement buttonContainer;
    private RightPanelComponent<T> rightPanelComponent;
    private ListView listView;
    private HelpBox emptyState;
    private readonly List<T> listElements = new List<T>();

    private DropdownField identifierDropdown;

    private IDisposable elementChangedSub;
    private IDisposable agentTypesChangedSub;
    private IDisposable agentCollectionUpdatedSub;
    private T selectedElement;
    private ReactiveList<T> elements;
    // True while the list still shows the agents of a run that ended, until a new bot starts.
    private bool showingLastRun;
    private int SelectedIndex => listElements.IndexOf(selectedElement);
    private AgentManager agentManager => AgentManager.Instance;
    protected Toolbar ToolbarTop;
    public void CreateGUI()
    {
        root = rootVisualElement;
        var treeAsset = AssetService.GetVisualTreeAsset("SplitViewWindowDropDownSelection");
        treeAsset.CloneTree(root);

        leftContainer = root.Q<VisualElement>("LeftContainer");
        rightContainer = root.Q<VisualElement>("RightContainer");
        buttonContainer = root.Q<VisualElement>("ButtonContainer");
        SplitCollapseService.Attach(root.Q<TwoPaneSplitView>(), "Agents");

        identifierDropdown = root.Q<DropdownField>("AgentType-Dropdown");
        SearchableDropdown.Attach(identifierDropdown);
        identifierDropdown.label = "Agent Type";
        identifierDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            if (!agentManager.AgentIdentifiers.Contains(evt.newValue)) return;
            TemplateManagerStateService.SetText(StateKey("AgentType"), evt.newValue);
            UpdateLeftPanel(evt.newValue);
        });
        ToolbarTop = root.Q<Toolbar>("ToolbarTop");

        InitList();

        // Agents only exist while the game runs, so say so instead of showing an empty window.
        emptyState = new HelpBox("", HelpBoxMessageType.Info);
        emptyState.style.marginTop = 6;
        emptyState.style.marginLeft = 6;
        emptyState.style.marginRight = 6;

        rightPanelComponent = GetRightPanelComponent();
        rightContainer.Add(emptyState);
        rightContainer.Add(rightPanelComponent);
        rightPanelComponent.style.display = DisplayStyle.None;

        agentTypesChangedSub = agentManager
            .AgentIdentifiersUpdated
            .Subscribe(_ => InitDropDown());

        InitDropDown();

        agentCollectionUpdatedSub = agentManager
            .AgentsUpdated
            .Subscribe(agent =>
            {
                if (agent.TypeIdentifier == identifierDropdown.value)
                {
                    UpdateLeftPanel();
                }
            });

        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        // Trickle-down so the arrows move the selection exactly once, whether or not the list has focus.
        root.RegisterCallback<KeyDownEvent>(key =>
        {
            var typing = key.target is TextElement;
            var handled = false;
            if (typing || listElements.Count == 0)
            {
                // leave keys alone while typing or when there is nothing to select
            }
            else if (key.keyCode == KeyCode.UpArrow && key.ctrlKey)
            {
                SelectElementAtIndex(0);
                handled = true;
            }
            else if (key.keyCode == KeyCode.UpArrow)
            {
                SelectElementAtIndex(SelectedIndex - ConstsEditor.Logger_StepSize);
                handled = true;
            }
            else if (key.keyCode == KeyCode.DownArrow && key.ctrlKey)
            {
                SelectElementAtIndex(listElements.Count - 1);
                handled = true;
            }
            else if (key.keyCode == KeyCode.DownArrow)
            {
                SelectElementAtIndex(SelectedIndex + ConstsEditor.Logger_StepSize);
                handled = true;
            }

            KeyPressed(key);
            if (handled) key.StopImmediatePropagation();
        }, TrickleDown.TrickleDown);

        Init();
        UpdateEmptyState();
    }

    protected virtual void Init() { }

    // What the window remembers (agent type, selected agent) is stored per window class.
    private string StateKey(string name) => GetType().Name + "." + name;

    // The selected agent is remembered per agent type, by name (agents are recreated on every Play).
    private string RememberedElementName() =>
        TemplateManagerStateService.GetText(StateKey("Agent." + identifierDropdown.value));

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        UpdateEmptyState();
    }

    private void InitList()
    {
        listView = new ListView(listElements)
        {
            selectionType = SelectionType.Single,
            fixedItemHeight = 22,
            showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
            showBorder = true,
            makeItem = () =>
            {
                var label = new Label();
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.paddingLeft = 6;
                return label;
            }
        };
        listView.bindItem = (element, index) =>
        {
            if (index < 0 || index >= listElements.Count) return;
            ((Label) element).text = GetNameFromElement(listElements[index]);
        };
        listView.style.flexGrow = 1;
        listView.selectionChanged += items =>
        {
            var picked = items.OfType<T>().ToList();
            if (picked.Count == 0) return;
            if (EqualityComparer<T>.Default.Equals(picked[0], selectedElement)) return;
            SelectedElement = picked[0];
        };
        buttonContainer.Add(listView);
    }

    private void SelectElementAtIndex(int index)
    {
        if (listElements.Count == 0) return;

        if (index < 0)
        {
            index = 0;
        } else if (index >= listElements.Count)
        {
            index = listElements.Count - 1;
        }
        SelectedElement = listElements[index];
    }

    protected abstract RightPanelComponent<T> GetRightPanelComponent();

    private void InitDropDown()
    {
        identifierDropdown.choices = agentManager.AgentIdentifiers;
        if (agentManager.AgentIdentifiers.Count > 0)
        {
            // Keep what is showing; after a restart fall back to the last type used, then to the first.
            var choices = identifierDropdown.choices;
            var saved = TemplateManagerStateService.GetText(StateKey("AgentType"));
            var wanted = choices.Contains(identifierDropdown.value) ? identifierDropdown.value
                : choices.Contains(saved) ? saved
                : choices.First();
            identifierDropdown.SetValueWithoutNotify(wanted);
            identifierDropdown.SetEnabled(true);
            UpdateLeftPanel(identifierDropdown.value);
        }
        else
        {
            // No fake "value": the empty-state message explains what is going on.
            identifierDropdown.SetValueWithoutNotify(null);
            identifierDropdown.SetEnabled(false);
            elements = null;
            elementChangedSub?.Dispose();
            LoadElements(new List<T>());
        }
    }

    protected abstract ReactiveList<T> GetLeftPanelElements(string identifier);

    private void UpdateLeftPanel(string identifier = null)
    {
        identifier ??= identifierDropdown.value;
        if (string.IsNullOrEmpty(identifier)) return;
        elements = GetLeftPanelElements(identifier);

        elementChangedSub?.Dispose();
        elementChangedSub = elements
            .OnValueChanged
            .Subscribe(LoadElements);

        LoadElements(elements.Values);
    }

    private void LoadElements(List<T> newElements)
    {
        // Agents unregister when Play stops (or a bot is destroyed). Keep the last ones, and their logs,
        // on screen until a new bot starts instead of emptying the window.
        if (newElements.Count == 0 && listElements.Count > 0)
        {
            showingLastRun = true;
            UpdateEmptyState();
            return;
        }

        showingLastRun = false;
        SelectedElement = default(T);
        listElements.Clear();
        listElements.AddRange(newElements);
        listView.ClearSelection();
        listView.Rebuild();
        UpdateEmptyState();
        RestoreSelection();
    }

    // A rebuilt list (an agent registered, Play was pressed) keeps the agent the user was looking at.
    private void RestoreSelection()
    {
        if (listElements.Count == 0 || string.IsNullOrEmpty(identifierDropdown.value)) return;
        var remembered = RememberedElementName();
        if (string.IsNullOrEmpty(remembered)) return;
        var found = listElements.FindIndex(e => GetNameFromElement(e) == remembered);
        if (found >= 0) SelectedElement = listElements[found];
    }

    private void UpdateEmptyState()
    {
        if (emptyState == null) return;

        if (showingLastRun)
        {
            emptyState.style.display = DisplayStyle.Flex;
            emptyState.messageType = HelpBoxMessageType.Info;
            emptyState.text = "Showing the last run. It is replaced when a new bot starts.";
            return;
        }

        if (listElements.Count > 0)
        {
            emptyState.style.display = DisplayStyle.None;
            return;
        }

        emptyState.style.display = DisplayStyle.Flex;
        if (!Application.isPlaying)
        {
            emptyState.messageType = HelpBoxMessageType.Warning;
            emptyState.text = "This window needs Play Mode: agents only exist while the game is running " +
                              "(each one registers itself when it starts). Press Play and they will be listed here.";
        }
        else
        {
            emptyState.messageType = HelpBoxMessageType.Info;
            emptyState.text = "No agents found. An agent registers itself when it starts, " +
                              "so it needs an active AgentMono component in the scene.";
        }
    }

    protected virtual void KeyPressed(KeyDownEvent key)
    {

    }

    protected abstract string GetNameFromElement(T element);

    private void SelectedElementChanged()
    {
        var hasSelection = !EqualityComparer<T>.Default.Equals(SelectedElement, default(T));
        rightPanelComponent.style.display = hasSelection ? DisplayStyle.Flex : DisplayStyle.None;
        if (hasSelection)
        {
            rightPanelComponent.UpdateUi(SelectedElement);
        }

        var index = SelectedIndex;
        if (index >= 0 && listView != null)
        {
            listView.SetSelectionWithoutNotify(new[] { index });
            listView.ScrollToItem(index);
        }
    }

    private T SelectedElement
    {
        get => selectedElement;
        set
        {
            selectedElement = value;
            if (!EqualityComparer<T>.Default.Equals(value, default(T)) && !string.IsNullOrEmpty(identifierDropdown?.value))
            {
                TemplateManagerStateService.SetText(StateKey("Agent." + identifierDropdown.value), GetNameFromElement(value));
            }
            SelectedElementChanged();
        }
    }

    void ClearSubscriptions()
    {
        elementChangedSub?.Dispose();
        agentTypesChangedSub?.Dispose();
        agentCollectionUpdatedSub?.Dispose();
        elementNameUpdatedSub.Clear();
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnDestroy()
    {
        WindowOpener.windowPosition = this.position;
        ClearSubscriptions();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }
}
