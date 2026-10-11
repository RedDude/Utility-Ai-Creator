using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using UniRx;
using System;

internal abstract class AiObjectViewModel : VisualElement
{
    protected readonly CompositeDisposable modelInfoChangedDisposable = new CompositeDisposable();

    private readonly Label typeLabel;
    private readonly TextField nameTextField;
    private readonly TextField descriptionTextField;
    internal AiObjectModel Model;
    protected readonly VisualElement ScoreContainer;
    protected readonly VisualElement Header;
    protected readonly VisualElement Body;
    protected readonly VisualElement Footer;
    protected InfoViewModel infoViewModel;
    protected readonly Button SaveToTemplateButton;
    protected readonly HelpBox HelpBox;
    protected readonly VisualElement HelpBoxContainer;
    private readonly LabelContainerViewModel labelContainer;
    private readonly Foldout rootFoldout;
    private readonly Label collapsedNameLabel;
    private readonly Label collapsedDescriptionLabel;
    private readonly Button rootSaveButton;
    private readonly VisualElement[] collapsibleSections;
    private readonly VisualElement typeHeader;
    private readonly VisualElement aiObjectHeader;
    private readonly VisualElement scoresHome;


    internal List<ScoreViewModel> ScoreComponents = new List<ScoreViewModel>();

    protected AiObjectViewModel ()
    {
        var root = AssetService.GetTemplateContainer(typeof(AiObjectViewModel ).FullName);
        Add(root);
        styleSheets.Add(StylesService.GetStyleSheet("AiObjectViewModel"));

        typeLabel = root.Q<Label>("Type-Label");
        typeLabel.text = "";
        // The header used to reserve 84px (and the identifier column 64px) even for a one-line description.
        root.Q<VisualElement>("AiObjectHeader").style.minHeight = 0;
        root.Q<VisualElement>("Identifier").style.minHeight = 0;
        nameTextField = this.Q<TextField>("Name-TextField");
        nameTextField.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            if (Model != null)
            {
                Model.Name = evt.newValue;
            }
        });
        
        HelpBoxContainer = root.Q<VisualElement>("HelpBoxContainer");
        HelpBox = new HelpBox("", HelpBoxMessageType.Info);
        HelpBox.style.display = DisplayStyle.None; // shown only when the model has help text
        HelpBoxContainer.Add(HelpBox);

        descriptionTextField = root.Q<TextField>("Description-TextField");
        // One line until you click into it, then it opens up for longer text.
        descriptionTextField.multiline = true;
        descriptionTextField.style.minHeight = 20;
        descriptionTextField.style.height = 20;
        descriptionTextField.style.flexGrow = 0;
        descriptionTextField.RegisterCallback<FocusInEvent>(_ =>
        {
            descriptionTextField.style.height = StyleKeyword.Auto;
            descriptionTextField.style.minHeight = 60;
        });
        descriptionTextField.RegisterCallback<FocusOutEvent>(_ =>
        {
            descriptionTextField.style.height = 20;
            descriptionTextField.style.minHeight = 20;
        });
        descriptionTextField.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            if (Model != null)
            {
                Model.Description = evt.newValue;
            }
        });

        ScoreContainer = this.Q<VisualElement>("Scores");
        Header = this.Q<VisualElement>("Header");
        Body = this.Q<VisualElement>("Body");
        Footer = this.Q<VisualElement>("Footer");
        SaveToTemplateButton = this.Q<Button>("SaveToTemplate-Button");
        SaveToTemplateButton.RegisterCallback<MouseUpEvent>(SaveToTemplate);
        var lc = this.Q<VisualElement>("LabelContainer");
        labelContainer = new LabelContainerViewModel();
        lc.Add(labelContainer);

        SetFooter();

        // The top-level editor (not one nested in a list item, which has its own arrow) can be collapsed to its title row.
        typeHeader = root.Q<VisualElement>("TypeHeader");
        aiObjectHeader = root.Q<VisualElement>("AiObjectHeader");
        scoresHome = ScoreContainer.parent;
        // Only the name / description / score fields fold away; the tabs below stay visible.
        collapsibleSections = new[]
        {
            root.Q<VisualElement>("HorizontalContainer")
        };
        nameTextField.style.display = DisplayStyle.None; // renamed by double-clicking the title instead

        rootFoldout = new Foldout { text = "", value = true, tooltip = "Collapse or expand this editor" };
        rootFoldout.style.display = DisplayStyle.None;
        rootFoldout.style.flexShrink = 0;
        typeHeader.Insert(0, rootFoldout);

        collapsedNameLabel = new Label();
        collapsedNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        InlineRename.Attach(collapsedNameLabel, () => Model?.Name, newName =>
        {
            if (Model == null) return;
            Model.Name = newName;
            collapsedNameLabel.text = newName;
        });
        collapsedNameLabel.style.marginLeft = 6;
        collapsedNameLabel.style.flexShrink = 1;
        typeHeader.Insert(2, collapsedNameLabel); // foldout, type badge, then the name on the same row

        collapsedDescriptionLabel = new Label();
        collapsedDescriptionLabel.style.display = DisplayStyle.None;
        collapsedDescriptionLabel.style.opacity = 0.7f;
        collapsedDescriptionLabel.style.marginLeft = 8;
        collapsedDescriptionLabel.style.flexShrink = 1;
        collapsedDescriptionLabel.style.overflow = Overflow.Hidden;
        collapsedDescriptionLabel.style.whiteSpace = WhiteSpace.NoWrap;
        collapsedDescriptionLabel.style.textOverflow = TextOverflow.Ellipsis;
        typeHeader.Insert(3, collapsedDescriptionLabel);

        // "Save as template" for the top-level object: an icon at the left of the title.
        rootSaveButton = IconButtonService.CreateSave();
        rootSaveButton.style.display = DisplayStyle.None;
        rootSaveButton.RegisterCallback<ClickEvent>(_ => SaveToTemplate(null));
        typeHeader.Insert(1, rootSaveButton);
        typeHeader.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            if (rootFoldout.style.display == DisplayStyle.None) return; // nested editors use the list item's menu
            evt.menu.AppendAction(rootFoldout.value ? "Collapse" : "Expand", _ => rootFoldout.value = !rootFoldout.value);
            evt.menu.AppendAction("Save to template", _ => SaveToTemplate(null));
        }));
        Footer.style.display = DisplayStyle.None; // only held the old "Save To Template" text button

        rootFoldout.RegisterValueChangedCallback(evt => SetCollapsed(!evt.newValue));
        RegisterCallback<AttachToPanelEvent>(_ =>
        {
            var isRoot = this.GetFirstAncestorOfType<ListViewModel>() == null;
            rootFoldout.style.display = isRoot ? DisplayStyle.Flex : DisplayStyle.None;
            rootSaveButton.style.display = isRoot ? DisplayStyle.Flex : DisplayStyle.None;
            if (isRoot)
            {
                typeHeader.style.flexDirection = FlexDirection.Row;
                typeHeader.style.alignItems = Align.Center;
            }
            else
            {
                SetCollapsed(false);
                // Inside a list item the row above already shows type, labels, scores and the save / remove buttons.
                typeHeader.style.display = DisplayStyle.None;
                ScoreContainer.style.display = DisplayStyle.None;
            }
        });
    }

    private void SetCollapsed(bool collapsed)
    {
        foreach (var section in collapsibleSections)
        {
            section.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
        }
        // Collapsed: no reserved space for the fields, and the scores (weight, playable...) join the title row.
        if (collapsed)
        {
            ScoreContainer.style.flexDirection = FlexDirection.Row;
            ScoreContainer.style.alignItems = Align.Center;
            ScoreContainer.style.minWidth = 0;
            ScoreContainer.style.marginLeft = StyleKeyword.Auto;
            typeHeader.Add(ScoreContainer);
        }
        else
        {
            ScoreContainer.style.flexDirection = StyleKeyword.Null;
            ScoreContainer.style.alignItems = StyleKeyword.Null;
            ScoreContainer.style.minWidth = StyleKeyword.Null;
            ScoreContainer.style.marginLeft = StyleKeyword.Null;
            scoresHome.Add(ScoreContainer);
        }
        collapsedNameLabel.text = Model?.Name;
        var hasDescription = !string.IsNullOrWhiteSpace(Model?.Description);
        collapsedDescriptionLabel.style.display = collapsed && hasDescription ? DisplayStyle.Flex : DisplayStyle.None;
        collapsedDescriptionLabel.text = Model?.Description;
    }

    private async void SaveToTemplate(MouseUpEvent evt)
    {
        var clone = await Model.CloneAsync();
        TemplateService.Instance.Add(clone);
    }

    internal void Touch()
    {
        UpdateUi(Model);
    }

    internal void UpdateUi(AiObjectModel model)
    {
        try
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            SetLabels();
            Model = model;
            typeLabel.text = Model.GetTypeDescription();
            TypeStyleService.Apply(typeLabel, Model, true);
            model.RefreshInfo();
            nameTextField.value = model.Name;
            if (collapsedNameLabel != null) collapsedNameLabel.text = model.Name;
            descriptionTextField.value = model.Description;
            TimerService.Instance.LogCall(sw.ElapsedMilliseconds, model.GetType() + "Update Ui Init");
            sw.Restart();
            if (string.IsNullOrEmpty(model.HelpText))
            {
                HelpBox.style.display = DisplayStyle.None;
            } else
            {
                HelpBox.style.display = DisplayStyle.Flex;
                HelpBox.text = model.HelpText;
            }
            TimerService.Instance.LogCall(sw.ElapsedMilliseconds, model.GetType() + "Update Ui HelpBox");
            sw.Restart();
            ScoreContainer.Clear();
            ScoreComponents = new List<ScoreViewModel>();
            foreach (var scoreModel in model.ScoreModels)
            {
                var scoreComponent = new ScoreViewModel(scoreModel);
                ScoreComponents.Add(scoreComponent);
                ScoreContainer.Add(scoreComponent);
                if (scoreModel.Name == "Error")
                {
                    // A red accent bar instead of flooding the whole editor red.
                    style.borderLeftWidth = 4;
                    style.borderLeftColor = new StyleColor(new Color(0.85f, 0.25f, 0.2f));
                }
            }
            TimerService.Instance.LogCall(sw.ElapsedMilliseconds, model.GetType() + "Update Ui ScoreComponents");
            sw.Restart();

            modelInfoChangedDisposable.Clear();
            Model.OnInfoChanged
                .Subscribe(info => infoViewModel.DispalyInfo(info))
                .AddTo(modelInfoChangedDisposable);
            infoViewModel.DispalyInfo(Model.Info);
            TimerService.Instance.LogCall(sw.ElapsedMilliseconds, model.GetType() + "Update Ui Subscribe");
            sw.Restart();
            UpdateInternal(model);
            TimerService.Instance.LogCall(sw.ElapsedMilliseconds, model.GetType() + "Update Ui Update Internal");
            sw.Restart();
        }
        catch (Exception ex)
        {
            Model.Name = "Error Updating Ui Component";
            Model.Description = ex.ToString();
            throw;
        }
    }

    protected abstract void UpdateInternal(AiObjectModel model);

    protected virtual void SetFooter()
    {
        // Full-width callout under the title instead of the footer's right-hand corner.
        infoViewModel = new InfoViewModel();
        HelpBoxContainer.Add(infoViewModel);
    }
    
    private void SetLabels()
    {
        labelContainer.ClearLabels();
        if (Model == null) return;
        if (Model.GetType().IsSubclassOf(typeof(Consideration)) || Model.GetType() == typeof(Consideration))
        {
            if (Model is not Consideration cons) return;
        
            if (cons.IsModifier)
            {
                labelContainer.AddLabel("Modifier");
            }
            
            if (cons.IsSetter)
            {
                labelContainer.AddLabel("Setter");
            }
            
            if (cons.IsScorer)
            {
                labelContainer.AddLabel("Scorer");
            }
            else
            {
                labelContainer.AddLabel("Boolean");
            }
        }
    }

    ~AiObjectViewModel ()
    {
        ClearSubscriptions();
    }

    protected virtual void ClearSubscriptions()
    {
        modelInfoChangedDisposable?.Clear();
    }
}