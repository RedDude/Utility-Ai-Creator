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
        // A name wider than the row is cut with an ellipsis; it used to run underneath the scores.
        nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
        nameLabel.style.textOverflow = TextOverflow.Ellipsis;
        nameLabel.style.overflow = Overflow.Hidden;
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
        // Min / Max / Return value... follow the title on the same line and drop to a line below when they do not fit.
        // The title row (see the UXML) must have no growing child: a row that grows and holds a growing child is measured
        // as full width, so the footer never fits beside it. The scores take the spare space with an auto margin instead.
        // Inline styles: the enclosing editor's #Footer rule (margin, 24px max height, space-between) reaches this element.
        footer.style.flexWrap = Wrap.Wrap;
        footer.style.flexGrow = 0;
        footer.style.flexShrink = 1;
        footer.style.marginTop = 0;
        footer.style.marginBottom = 0;
        footer.style.marginLeft = 6;
        footer.style.marginRight = 0;
        footer.style.minHeight = 0;
        footer.style.maxHeight = StyleKeyword.None;
        footer.style.justifyContent = Justify.FlexStart;
        footer.style.overflow = Overflow.Visible;
        var headerRow = this.Q<VisualElement>("Header");
        headerRow.Add(footer); // after the title row (type, name, labels, score), which the header wraps it away from
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
        // Min / Max look like Base / Score and come first, so the row reads: labels, Min, Max, Base, Score.
        if (aiObjectModel is Consideration cons)
        {
            AddParamScore(cons.MinFloat);
            AddParamScore(cons.MaxFloat);
        }

        foreach (var scoreModel in aiObjectModel.ScoreModels)
        {
            // ScoreViewModel follows its model's value changes by itself.
            var component = new ScoreViewModel(scoreModel);
            component.style.marginRight = 8;
            scoreContainer.Add(component);
        }
    }

    private void AddParamScore(ParamFloat param)
    {
        var scoreModel = new ScoreModel(param.Name, Convert.ToSingle(param.Value));
        param.OnValueChange
            .Subscribe(_ => scoreModel.Value = Convert.ToSingle(param.Value))
            .AddTo(disposables);
        var component = new ScoreViewModel(scoreModel);
        component.style.marginRight = 8;
        scoreContainer.Add(component);
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
        // Min / Max of a consideration are shown with the scores (see SetScores).
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

        // Nothing to show: take no space (an empty footer could still force an empty line below the title).
        footer.style.display = footer.childCount == 0 ? DisplayStyle.None : DisplayStyle.Flex;
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