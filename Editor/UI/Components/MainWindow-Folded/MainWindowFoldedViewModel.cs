using System;
using UnityEngine.UIElements;
using UniRx;

internal class MainWindowFoldedViewModel : VisualElement
{
    private readonly CompositeDisposable disposables = new CompositeDisposable();

    private readonly Label typeLabel;
    private readonly Label nameLabel;
    private readonly Label descriptionLabel;
    private readonly VisualElement descriptionContainer;
    private bool descriptionExpanded;
    private readonly VisualElement scoreContainer;
    private readonly LabelContainerViewModel labelContainer;
    private readonly VisualElement footer;
    private ScoreViewModel baseScore;
    private AiObjectModel model;
    protected InfoViewModel infoViewModel;

    internal MainWindowFoldedViewModel()
    {
        var root = AssetService.GetTemplateContainer(GetType().FullName);
        Add(root);
        root.styleSheets.Clear();
        root.styleSheets.Add(StylesService.GetStyleSheet(GetType().FullName));

        HeaderRow = this.Q<VisualElement>("Header"); // cached: it is moved out of this view by the list item
        typeLabel = this.Q<Label>("Type-Label-Folded");
        nameLabel = this.Q<VisualElement>("NameIdentifier").Q<Label>("Value-Label");
        InlineRename.Attach(nameLabel, () => model?.Name, newName =>
        {
            if (model != null) model.Name = newName;
        });
        descriptionContainer = this.Q<VisualElement>("DescriptionIdentifier");
        descriptionLabel = descriptionContainer.Q<Label>("Value-Label");
        // One line with an ellipsis; click it to read the whole text.
        descriptionLabel.style.whiteSpace = WhiteSpace.NoWrap;
        descriptionLabel.style.textOverflow = TextOverflow.Ellipsis;
        descriptionLabel.style.overflow = Overflow.Hidden;
        descriptionLabel.style.flexShrink = 1;
        descriptionLabel.RegisterCallback<ClickEvent>(_ =>
        {
            descriptionExpanded = !descriptionExpanded;
            ApplyDescriptionWrap();
        });
        scoreContainer = this.Q<VisualElement>("ScoreContainer");
        footer = this.Q<VisualElement>("Footer");
        // Min / Max / Return value... sit in the same row as type and name, not on a second line.
        footer.style.flexWrap = Wrap.NoWrap;
        footer.style.flexGrow = 0;
        footer.style.flexShrink = 1;
        footer.style.marginTop = 0;
        footer.style.marginBottom = 0;
        footer.style.marginLeft = 6;
        footer.style.overflow = Overflow.Hidden;
        var headerRow = this.Q<VisualElement>("Header");
        headerRow.Insert(headerRow.IndexOf(this.Q<VisualElement>("ScoreContainer")), footer);
        var identifierContainer = this.Q<VisualElement>("IdentifierContainer");
        infoViewModel = new InfoViewModel();
        identifierContainer.Add(infoViewModel);
        var lc = this.Q<VisualElement>("LabelContainer");
        labelContainer = new LabelContainerViewModel();
        lc.Add(labelContainer);
    }

    internal AiObjectModel Model => model;

    /// <summary>Type badge, name, labels and weight. The list item moves it up next to its fold arrow.</summary>
    internal VisualElement HeaderRow { get; private set; }

    internal void Touch()
    {
        UpdateUi(model);
    }

    internal void UpdateUi(AiObjectModel aiObjectModel)
    {

        disposables.Clear();
        this.model = aiObjectModel;

        aiObjectModel.RefreshInfo();
        SetLabels();
        SetFooter();

        typeLabel.text = aiObjectModel.GetTypeDescription();
        TypeStyleService.Apply(typeLabel, aiObjectModel, true);
        nameLabel.text = aiObjectModel.Name;
        SetDescription(aiObjectModel.Description);

        aiObjectModel.OnNameChanged
            .Subscribe(aiObjectName => nameLabel.text = aiObjectName)
            .AddTo(disposables);

        aiObjectModel.OnDescriptionChanged
            .Subscribe(SetDescription)
            .AddTo(disposables);
        
        scoreContainer.Clear();
        SetScores(aiObjectModel);
        if (aiObjectModel.GetType() == typeof(Bucket) || aiObjectModel.GetType().IsAssignableFrom(typeof(Bucket)))
        {
            var b = aiObjectModel as Bucket;
            var weightComponent = new ParameterComponent();
            weightComponent.UpdateUi(b.Weight);
            scoreContainer.Add(weightComponent);
        }

        infoViewModel.DispalyInfo(aiObjectModel.Info);
        aiObjectModel.OnInfoChanged
            .Subscribe(info => infoViewModel.DispalyInfo(info))
            .AddTo(disposables);
    }

    // Live scores sit in the title row, so they are visible without opening the item.
    private void SetScores(AiObjectModel aiObjectModel)
    {
        foreach (var scoreModel in aiObjectModel.ScoreModels)
        {
            // ScoreViewModel follows its model's value changes by itself.
            var component = new ScoreViewModel(scoreModel);
            component.style.marginRight = 8;
            scoreContainer.Add(component);
        }
    }

    // The description row is hidden when there is nothing to show.
    private void SetDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            descriptionLabel.text = "";
            descriptionContainer.style.display = DisplayStyle.None;
            return;
        }

        descriptionContainer.style.display = DisplayStyle.Flex;
        descriptionLabel.text = description;
        descriptionLabel.tooltip = "Click to expand or collapse";
        ApplyDescriptionWrap();
    }

    private void ApplyDescriptionWrap()
    {
        descriptionLabel.style.whiteSpace = descriptionExpanded ? WhiteSpace.Normal : WhiteSpace.NoWrap;
    }

    private void SetFooter()
    {
        footer.Clear();
        if (model.GetType().IsSubclassOf(typeof(Consideration)) || model.GetType() == typeof(Consideration))
        {
            var cons = model as Consideration;
            var minLabel = new Label()
            {
                text = cons.MinFloat.Name + ": " + cons.MinFloat.Value,
                name = "FoldedFooterLabel"
            };
            footer.Add(minLabel);
            cons.MinFloat.OnValueChange
                .Subscribe(_ => minLabel.text = cons.MinFloat.Name + ": " + cons.MinFloat.Value)
                .AddTo(disposables);

            var maxLabel = new Label()
            {
                text = cons.MaxFloat.Name + ": " + cons.MaxFloat.Value,
                name = "FoldedFooterLabel"
            };
            footer.Add(maxLabel);
            cons.MaxFloat.OnValueChange
                .Subscribe(_ => maxLabel.text = cons.MaxFloat.Name + ": " + cons.MaxFloat.Value)
                .AddTo(disposables);

        }

        foreach (var parameter in model.Parameters)
        {
            var labelText = parameter.Name + ": " + parameter.GetValueAsString();
            if (labelText.Length > 64)
            {
                labelText = labelText.Substring(0, 64) + "...";
            }
            var pLabel = new Label()
            {
                text = labelText,
                name = "FoldedFooterLabel"
            };
            footer.Add(pLabel);
            parameter.OnValueChange
                .Subscribe(_ => pLabel.text = parameter.Name + ": " + parameter.GetValueAsString())
                .AddTo(disposables);
        }
    }

    private void SetLabels()
    {
        labelContainer.ClearLabels();
        if (model.GetType().IsSubclassOf(typeof(Consideration)) || model.GetType() == typeof(Consideration))
        {
            if (model is not Consideration cons) return;
        
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

    ~MainWindowFoldedViewModel()
    {
        disposables.Clear();
    }
}