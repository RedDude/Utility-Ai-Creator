using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using UniRx;
using System.Linq;

using System.IO;
using System.Threading.Tasks;
using UniRxExtension;
using Unity.Profiling.Editor;

internal class TemplateManager : EditorWindow
{
    private IDisposable activeCollectionChangedSub;
    private readonly CompositeDisposable disposables = new CompositeDisposable();
    private readonly CompositeDisposable modelsChangedSubscriptions = new CompositeDisposable();

    private VisualElement root;
    private VisualElement leftPanel;
    private ListView listView;
    private readonly List<AiObjectModel> listModels = new List<AiObjectModel>();
    private VisualElement rightPanel;
    private Button addElementButton;
    private List<string> addElementChoices = new List<string>();
    private List<string> dropDownChoices;
    private DropdownField dropDown;
    private TemplateService templateService => TemplateService.Instance;

    private AiObjectViewModel  currentMainWindowViewModel;
    private AiObjectModel selectedModel;
    private PersistenceAPI persistenceAPI => PersistenceAPI.Instance;

    private readonly Dictionary<AiObjectModel, AiObjectViewModel> componentsByModels = new Dictionary<AiObjectModel, AiObjectViewModel>();
    private Toolbar toolbar;
    private long lastClearClick;
    private long clickConfirmationTimeMs = 200;
    
    private AiObjectModel SelectedModel
    {
        get => selectedModel;
        set
        {
            selectedModel = value;
            UpdateButtons();
        }
    }

    internal void CreateGUI()
    {
        // Load Objects
        root = rootVisualElement;
        DontDestroyOnLoad(this);

        var treeAsset = AssetService.GetVisualTreeAsset(GetType().FullName);
        treeAsset.CloneTree(root);
        toolbar = root.Q<Toolbar>();
        toolbar.style.alignItems = Align.Center; // buttons and menus on one line, vertically centred

        dropDown = root.Q<DropdownField>("TypeDropdown");

        leftPanel = root.Q<VisualElement>("left-panel");
        SplitCollapseService.Attach(root.Q<TwoPaneSplitView>(), "Categories");
        InitList(leftPanel.Q<ListView>("ButtonContainer"));

        root.Q<VisualElement>("Buttons");
        addElementButton = new Button { text = "+ Add element", tooltip = "Add a new element (searchable list)" };
        addElementButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        var addElementPopupContainer = root.Q<VisualElement>("AddElementPopupContainer");
        rightPanel = root.Q<VisualElement>("right-panel");

        
        // Init Objects
        InitToolbarFile();
        InitToolbarDebug();
        InitToolBarSettings();
        InitDropdown();
        UpdateLeftPanel();
        addElementPopupContainer.Add(addElementButton);
        SearchableDropdown.Attach(addElementButton, () => addElementChoices, () => null, picked => AddAiObject(picked));

        
        // Subscribe
        TemplateService.Instance.OnIncludeDemosChanged
            .Subscribe(_ =>
            {
                UpdateAddElementPopup();
            })
            .AddTo(disposables);

        activeCollectionChangedSub = TemplateService.Instance
            .OnCollectionChanged
            .Subscribe(UpdateLeftPanelIfActiveCollectionChanged);

        ProjectSettingsService.Instance.OnProjectSettingsChanged
            .Subscribe(_ =>
            {
                var projectName = ProjectSettingsService.Instance.GetCurrentProjectName();
                titleContent.text = projectName;
            })
            .AddTo(disposables);
    }

    private async void AddAiObject(string picked)
    {
        if (picked != null && picked != Consts.LineBreakBaseTypes && picked != Consts.LineBreakTemplates && picked != Consts.LineBreakDemos)
        {
            var type = MainWindowService.Instance.GetTypeFromString(dropDown.value);
            var collection = templateService.GetCollection(type);
            var aiObject = await AddCopyService.GetAiObjectClone(picked, collection.Values);
            if (aiObject == null)
            {
                Debug.LogWarning("UAI: could not create \"" + picked + "\".");
                return;
            }
            templateService.Add(aiObject);
            ModelSelected(aiObject);
        }
    }

    void OnEnable()
    {
        // await UasTemplateService.Instance.LoadCurrentProject(true);
        var mws = MainWindowService.Instance;
        // mws.OnUpdateStateChanged
        //     .Subscribe(state =>
        //     {
        //         var projectName = ProjectSettingsService.Instance.GetCurrentProjectName();
        //         this.titleContent.text = state ? projectName + " - Loading" : projectName;
        //     })
        //     .AddTo(disposables);
        mws.Start();

        templateService.OnStateChanged
            .Subscribe(state =>
            {
                var projectName = ProjectSettingsService.Instance.GetCurrentProjectName();
                DebugService.Log("Setting state: " + state, this);
                if (string.IsNullOrEmpty(state))
                {
                    titleContent.text = projectName;
                }
                else
                {
                    titleContent.text = projectName + " - " + state;
                }
            })
            .AddTo(disposables);
    }

    private void InitToolbarFile()
    {
        var menu = new ToolbarMenu
        {
            text = "File"
        };

        menu.menu.AppendAction("New Project", NewProject);

        menu.menu.AppendAction("Save", SaveUas);

        async void SaveProjectAs(DropdownMenuAction _)
        {
            await ProjectSettingsService.Instance.SaveProjectAs();
            rightPanel.Clear();
        }

        menu.menu.AppendAction("Save As", SaveProjectAs);

        menu.menu.AppendAction("Open Project", OpenProject);

 

        menu.menu.AppendAction("Export File(s)", ExportFiles);

        menu.menu.AppendAction("Import File(s)", ImportFiles);

        menu.menu.AppendAction("Reload Project", ReloadProject);

        menu.menu.AppendAction("Save Backup - TEST", _ =>
        {
            Task.Factory.StartNew(() => templateService.Save(true));
        });

        menu.menu.AppendAction("Load Backup - TEST", LoadTemporary);

        menu.menu.AppendAction("Exit", _ =>
        {
            Close();
        });
        
        toolbar.Add(menu);
    }

    private async void LoadTemporary(DropdownMenuAction _)
    {
        await templateService.LoadCurrentProject(true);
    }

    private async void ReloadProject(DropdownMenuAction _)
    {
        await templateService.LoadCurrentProject();
    }

    private void InitToolBarSettings()
    {
        var printDebug = new ToolbarToggle
        {
            text = "Print debug"
        };
        printDebug.SetValueWithoutNotify(DebugService.PrintDebug);
        
        printDebug.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            DebugService.PrintDebug = evt.newValue;
        });
        toolbar.Add(printDebug);
        var reloadPlayAbleAis = new ToolbarButton()
        {
            text = "Force Update Playable Ais"
        };
        reloadPlayAbleAis.RegisterCallback<MouseUpEvent>(_ =>
        {
            templateService.SetState("Saving");
            PlayAbleAiService.Instance.UpdateAisFromTemplateService(true);
            templateService.SetState("Ready");
        });
        toolbar.Add(reloadPlayAbleAis);

        var printSequence = new ToolbarButton()
        {
            text = "Timer Print Sequence"
        };
        printSequence.RegisterCallback<MouseUpEvent>(_ =>
        {
            TimerService.Instance.DebugLogSequence();
        });
        toolbar.Add(printSequence);
    }

    private async void SaveUas(DropdownMenuAction _)
    {
        await templateService.Save();
        await templateService.Save(true);
        // MainThreadDispatcher.StartCoroutine(uASTemplateService.SaveCoroutine());
    }

    private async void ImportFiles(DropdownMenuAction _)
    {
        var path = EditorUtility.OpenFilePanelWithFilters("Import",
            ProjectSettingsService.Instance.model.LastProjectDirectory, Consts.FileExtensionsFilters);

        var stateType = FileExtensionService.GetStateTypeFromFileName(path);
        var objectType = FileExtensionService.GetTypeFromFileName(path);
        var loadedMetaData = PersistenceAPI.Instance.Persister.LoadObject(path, stateType);
        var importedObjectState = loadedMetaData.LoadedObject as AiObjectModelSingleFileState;
        var toCollection = templateService.GetCollection(objectType);
        var importedObject =
            await PersistSingleFile.Restore(importedObjectState, objectType) as
                AiObjectModel;
        
        toCollection.Add(importedObject);
    }
    
    private async void ExportFiles(DropdownMenuAction _)
    {
        var saveObjects = new List<AiObjectModel>();
        selectedObjects.ForEach(model => saveObjects.Add(model));
        var path = EditorUtility.SaveFolderPanel("Export", ProjectSettingsService.Instance.model.LastProjectDirectory,"Export Folder");
        foreach (var saveObject in saveObjects)
        {
            var savePath = path + "/" + saveObject.Name + "." + FileExtensionService.GetFileExtensionFromType(saveObject.GetType());
            DebugService.Log("Saving to: " + savePath, this);
            await saveObject.SaveToFile(savePath);
        }
        
        // await persistenceAPI.SaveObjectsSingleFilePanelAsync(saveObjects);
    }

    private async void OpenProject(DropdownMenuAction _)
    {
        await PopUpService.AskToSaveIfProjectNotSavedThenSelectProjectToLoad();
    }

    private async void NewProject(DropdownMenuAction _)
    {
        await PopUpService.AskToSaveIfProjectNotSavedThenCreateNew();
        UpdateLeftPanel(dropDown.value);
        rightPanel.Clear();
    }

    private void InitToolbarDebug()
    {
        var menu = new ToolbarMenu
        {
            text = "Debug"
        };

        menu.menu.AppendAction("Timer Print Time Log", _ =>
        {
            TimerService.Instance.DebugLogTime();
        });
        


        menu.menu.AppendAction("Timer Reset", _ =>
        {
            TimerService.Instance.Reset();
        });
        toolbar.Add(menu);
    }


    private void UpdateButtons()
    {
        // Copy / Delete / Sort live in the list context menu; nothing to enable or disable.
    }

    private void AddNewAiObject(string aiObjectName)
    {
        var aiObject = AssetService.GetInstanceOfType<AiObjectModel>(aiObjectName);
        templateService.Add(aiObject);
        ModelSelected(aiObject);
    }

    private async void CopySelectedElements()
    {
        // var tasks = selectedObjects
        //     .Select(selected => Task.Run(selected.Key.Clone))
        //     .ToList();
        //
        // await Task.WhenAll(tasks);
        // foreach (var task in tasks)
        // {
        //     templateService.Add(task.Result);
        // }
        // if (tasks.Count == 0)
        // {
        //     DebugService.Log("No element selected to copy", this);
        //     return;
        // }
        if(selectedObjects.Count == 0) return;
        AiObjectModel modelToSelect = null;
        var results = new List<AiObjectModel>();
        foreach (var o in selectedObjects)
        {
            var result = o.Clone();
            results.Add(result);
            modelToSelect = result;
        }
        templateService.Add(results);

        SelectedModel = modelToSelect;
    }

    private void DeleteSelectedElements()
    {
        var toDelete = selectedObjects.ToList();
        
        
        foreach(var element in toDelete)
        {
            templateService.Remove(element);
            if (!componentsByModels.ContainsKey(element)) continue;
            var component = componentsByModels[element];
            rightPanel.Remove(component);
            componentsByModels.Remove(element);
        }

        SelectedModel = null;
    }

    private void InitDropdown()
    {
        dropDownChoices = new List<string> {
                Consts.Label_UAIModel, 
                Consts.Label_BucketModel, 
                Consts.Label_DecisionModel, 
                Consts.Label_ConsiderationModel,
                Consts.Label_AgentActionModel,
                Consts.Label_ResponseCurve
        };

        dropDown.label = "Categories";
        dropDown.choices = dropDownChoices;
        var savedCategory = TemplateManagerStateService.GetCategory();
        dropDown.value = dropDownChoices.Contains(savedCategory) ? savedCategory : dropDownChoices[0];
        dropDown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            TemplateManagerStateService.SetCategory(evt.newValue);
            if(currentMainWindowViewModel != null)
            {
                currentMainWindowViewModel.style.display = DisplayStyle.None;
            }

            UpdateLeftPanel(evt.newValue);
        });
    }

    private void UpdateLeftPanel(string label = "")
    {
        DebugService.Log("Updating left panel",this);
        titleContent.text = ProjectSettingsService.Instance.GetCurrentProjectName();

        if (string.IsNullOrEmpty(label))
        {
            label = dropDown.value;
        }
        var models = templateService.GetCollection(label);
        UpdateLeftPanelIfActiveCollectionChanged(models);
    }

    private void UpdateLeftPanelIfActiveCollectionChanged(ReactiveList<AiObjectModel> modifiedList)
    {
        var activeCollection = templateService.GetCollection(dropDown.value);
        if (modifiedList != activeCollection)
        {
            DebugService.Log("Skipped updating collections were equal", this);
            return;
        }

        LoadModels(activeCollection.Values);
        UpdateAddElementPopup();
    }

    private void UpdateAddElementPopup()
    {
        var type = MainWindowService.Instance.GetTypeFromString(dropDown.value);
        var collection = templateService.GetCollection(type);
        addElementChoices = AddCopyService.GetChoices(type, collection.Values);
    }

    // True while the list is rebuilt, so the selection cleared by the rebuild is not saved over the remembered one.
    private bool loadingModels;

    private void LoadModels(List<AiObjectModel> models)
    {
        var rememberedSelection = TemplateManagerStateService.GetSelection(dropDown.value);

        SelectedModel = null;
        selectedObjects.Clear();
        modelsChangedSubscriptions.Clear();
        DebugService.Log("Loading " + models.Count + " models", this);

        loadingModels = true;
        listModels.Clear();
        listModels.AddRange(models);
        listView.itemsSource = listModels;
        listView.ClearSelection();
        listView.Rebuild();
        loadingModels = false;
        RestoreSelection(rememberedSelection);

        foreach (var model in models)
        {
            model.OnNameChanged
                .Subscribe(_ => listView.RefreshItems())
                .AddTo(modelsChangedSubscriptions);
        }
        UpdateButtons();
    }

    private void RestoreSelection(HashSet<Guid> remembered)
    {
        var indices = new List<int>();
        for (var i = 0; i < listModels.Count; i++)
        {
            if (remembered.Contains(listModels[i].Guid)) indices.Add(i);
        }
        if (indices.Count == 0) return;

        listView.SetSelection(indices);
        listView.ScrollToItem(indices[0]);
    }

    private List<AiObjectModel> selectedObjects = new List<AiObjectModel>();

    // The left side is a real list: one row per element, native single / ctrl / shift selection,
    // and a context menu for copy, delete and sort.
    private void InitList(ListView list)
    {
        listView = list;
        listView.itemsSource = listModels;
        listView.selectionType = SelectionType.Multiple;
        listView.fixedItemHeight = 24;
        listView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
        listView.showBorder = true;
        listView.style.flexGrow = 1;

        listView.makeItem = () =>
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4;
            row.style.flexGrow = 1;
            var badge = new Label { name = "Badge" };
            badge.style.minWidth = 56;
            badge.style.marginRight = 6;
            var label = new Label { name = "Title" };
            label.style.flexGrow = 1;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            row.Add(badge);
            row.Add(label);
            return row;
        };
        listView.bindItem = (element, index) =>
        {
            if (index < 0 || index >= listModels.Count) return;
            var model = listModels[index];
            element.Q<Label>("Title").text = model.GetUiName();
            var badge = element.Q<Label>("Badge");
            badge.text = model.GetTypeDescription();
            TypeStyleService.Apply(badge, model, true);
        };

        listView.selectionChanged += items =>
        {
            selectedObjects = items.OfType<AiObjectModel>().ToList();
            if (!loadingModels) TemplateManagerStateService.SetSelection(dropDown.value, selectedObjects);
            var model = selectedObjects.LastOrDefault();
            SelectedModel = model;
            if (model != null) ShowModel(model);
        };

        listView.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Delete && selectedObjects.Count > 0)
            {
                DeleteSelectedElements();
                evt.StopPropagation();
            }
        });

        listView.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            var hasSelection = selectedObjects.Count > 0;
            var status = hasSelection ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            var position = evt.mousePosition;
            evt.menu.AppendAction("+ Add element", _ =>
                SearchableDropdown.Show(position, 280f, addElementChoices, null, picked => AddAiObject(picked)));
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Copy", _ => CopySelectedElements(), status);
            evt.menu.AppendAction("Delete", _ => DeleteSelectedElements(), status);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Sort by name", _ =>
            {
                var type = MainWindowService.Instance.GetTypeFromString(dropDown.value);
                var collection = templateService.GetCollection(type) as ReactiveListNameSafe<AiObjectModel>;
                collection?.Sort();
            });
        }));
    }

    private void ShowModel(AiObjectModel model)
    {
        SelectedModel = model;
        if (currentMainWindowViewModel != null)
        {
            currentMainWindowViewModel.style.display = DisplayStyle.None;
        }

        if (componentsByModels.ContainsKey(model))
        {
            DebugService.Log("Updating Existing MVC", this);
            currentMainWindowViewModel = componentsByModels[model];
            currentMainWindowViewModel.UpdateUi(model);
        }
        else
        {
            DebugService.Log("Updating new MVC", this);
            var mvc = MainWindowService.Instance.GetAiObjectComponent(model);
            componentsByModels.Add(model, mvc);
            mvc.UpdateUi(model);
            rightPanel.Add(mvc);
            currentMainWindowViewModel = mvc;
        }
        currentMainWindowViewModel.style.display = DisplayStyle.Flex;
    }

    // Select an element in the list (used after adding / copying).
    private void ModelSelected(AiObjectModel model)
    {
        var index = listModels.IndexOf(model);
        if (index >= 0)
        {
            listView.SetSelection(index);
            listView.ScrollToItem(index);
        }
        else
        {
            ShowModel(model);
        }
    }

    private void OnDisable()
    {
        OnClose();
    }

    private void OnClose()
    {
        WindowOpener.windowPosition = position;
        ClearSubscriptions();
    }

    private void OnDestroy()
    {
        OnClose();
    }

    private void ClearSubscriptions()
    {
        activeCollectionChangedSub?.Dispose();
        modelsChangedSubscriptions.Clear();
        disposables.Clear();
    }
}