using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using UniRx;
using UniRxExtension;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

internal class AiViewModel : AiObjectViewModel 
{
    private readonly CompositeDisposable subscriptions = new CompositeDisposable();
    private IDisposable bucketTabSub;
    private readonly TemplateContainer root;
    private readonly DropdownContainerViewModel<UtilityContainerSelector> bucketDropdown;
    private readonly DropdownContainerViewModel<UtilityContainerSelector> decisionDropdown;
    private readonly DropdownDescriptionViewModel<IUtilityScorer> utilityScorerDropdown;
    private Uai uaiModel;
    private readonly VisualElement collectionsContainer;
    private readonly Toggle playableToggle;

    private readonly TabViewViewModel tabView;
    private readonly Button bucketTab;
    private Button settingsTab;

    private readonly CollectionViewModel<Bucket> bucketCollection;

    internal AiViewModel() : base()
    {
        root = AssetService.GetTemplateContainer(GetType().FullName);
        Body.Clear();
        Body.Add(root);
        collectionsContainer = root.Q<VisualElement>("CollectionsContainer");
        tabView = new TabViewViewModel(layoutKey: "Ai");
        collectionsContainer.Add(tabView);

        bucketCollection = new CollectionViewModel<Bucket>(TemplateService.Instance.Buckets, "Bucket", "Buckets");

        var settingsContainer = new VisualElement();

        playableToggle = new Toggle("Playable")
        {
            name = "Playable-Toggle"
        };
        // The default 150px label pushed the checkbox out of the narrow Scores column, so it looked missing.
        playableToggle.labelElement.style.minWidth = 0;
        playableToggle.labelElement.style.width = StyleKeyword.Auto;
        playableToggle.labelElement.style.marginRight = 6;
        playableToggle.style.flexShrink = 0;
        playableToggle.style.marginLeft = 4;
        playableToggle.style.marginRight = 4;

        bucketDropdown = new DropdownContainerViewModel<UtilityContainerSelector>("Bucket Selector");
        settingsContainer.Add(bucketDropdown);
        decisionDropdown = new DropdownContainerViewModel<UtilityContainerSelector>("Decision Selector");
        settingsContainer.Add(decisionDropdown);

        utilityScorerDropdown = new DropdownDescriptionViewModel<IUtilityScorer>();
        settingsContainer.Add(utilityScorerDropdown);
        settingsContainer.name = "SettingsContainer";

        bucketDropdown.name = "DropdownScorerCollection";
        decisionDropdown.name = "DropdownScorerCollection";
        utilityScorerDropdown.name = "DropdownScorerCollection";

        // Settings as one form column: a fixed label column, then the value, one setting per row.
        // Inline styles: the old stylesheet rules (#SettingsContainer row, 30% widths) are ID selectors that beat classes.
        settingsContainer.style.flexDirection = FlexDirection.Row;
        settingsContainer.style.width = new Length(100, LengthUnit.Percent);
        settingsContainer.style.justifyContent = Justify.SpaceBetween;
        settingsContainer.style.alignItems = Align.FlexStart;
        settingsContainer.style.paddingTop = 4;
        var columns = new VisualElement[] { bucketDropdown, decisionDropdown, utilityScorerDropdown };
        for (var i = 0; i < columns.Length; i++)
        {
            StyleSettingsColumn(columns[i], isLast: i == columns.Length - 1);
        }

        bucketTab = tabView.AddTabGroup("Buckets", bucketCollection);
        settingsTab = tabView.AddTabGroup("Settings", settingsContainer);
         
        playableToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            if (uaiModel == null) return;
            uaiModel.IsPLayAble = evt.newValue;
            uaiModel.RefreshInfo(); // the model does not recompute its warning when the flag changes
        });
    }

    // One settings column: label above the picker, description and options underneath.
    // flex-basis 0 + equal grow keeps every column at the same width whatever option is picked.
    private static void StyleSettingsColumn(VisualElement row, bool isLast)
    {
        row.style.width = StyleKeyword.Auto;
        row.style.flexGrow = 1;
        row.style.flexShrink = 1;
        row.style.flexBasis = 0;
        row.style.minWidth = 0;
        row.style.alignSelf = Align.Stretch;
        row.style.marginLeft = 0;
        row.style.marginRight = 0;
        row.style.marginTop = 0;
        row.style.marginBottom = 0;
        row.style.paddingLeft = 12;
        row.style.paddingRight = 12;
        row.style.paddingTop = 6;
        row.style.paddingBottom = 8;
        // A divider between neighbouring columns.
        row.style.borderRightWidth = isLast ? 0 : 1;
        row.style.borderRightColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        foreach (var container in row.Query<VisualElement>().Build().ToList())
        {
            if (container.name == "Horizontal" || container.name == "Left" || container.name == "DropdownContainer")
            {
                container.style.flexGrow = 1;
                container.style.alignItems = Align.Stretch;
            }
            else if (container.name == "Right")
            {
                container.style.display = DisplayStyle.None;
            }
        }

        var dropdown = row.Q<DropdownField>();
        if (dropdown == null) return;
        dropdown.style.flexDirection = FlexDirection.Column;
        dropdown.style.alignSelf = Align.Stretch;
        dropdown.style.marginLeft = 0;
        dropdown.style.marginRight = 0;
        dropdown.labelElement.style.minWidth = 0;
        dropdown.labelElement.style.width = StyleKeyword.Auto;
        dropdown.labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;
        var input = dropdown.Q(className: "unity-base-popup-field__input");
        if (input != null)
        {
            input.style.flexGrow = 1;
            input.style.minWidth = 0;
        }
    }

    protected override void UpdateInternal(AiObjectModel model)
    {
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        uaiModel = model as Uai;

        ScoreContainer.Add(playableToggle);

        Debug.Assert(uaiModel != null, nameof(uaiModel) + " != null");
        bucketTab.text = "Buckets (" + uaiModel.Buckets.Count + ")";
        bucketTabSub?.Dispose();
        bucketTabSub = uaiModel.Buckets.OnValueChanged
            .Subscribe(list => bucketTab.text = "Buckets (" + list.Count + ")");
        
        
        playableToggle.SetValueWithoutNotify(uaiModel.IsPLayAble);

        if (uaiModel == null)
        {
            bucketCollection.SetElements(new ReactiveList<Bucket>());
        }
        else
        {
            bucketCollection.SetElements(uaiModel.Buckets);
        }
        subscriptions.Clear();

        var currentDecisionIndex = uaiModel.DecisionSelectors.IndexOf(uaiModel.CurrentDecisionSelector);
        decisionDropdown.UpdateUi(uaiModel.DecisionSelectors, currentDecisionIndex);
        decisionDropdown
            .OnSelectedObjectChanged
            .Subscribe(selector => {
                if (uaiModel != null)
                {
                    uaiModel.CurrentDecisionSelector = selector;
                }
            })
            .AddTo(subscriptions);

        var currentBucketindex = uaiModel.BucketSelectors.IndexOf(uaiModel.CurrentBucketSelector);
        bucketDropdown.UpdateUi(uaiModel.BucketSelectors,currentBucketindex);
        bucketDropdown
            .OnSelectedObjectChanged
            .Subscribe(selector =>
            {
                if (uaiModel != null)
                {
                    uaiModel.CurrentBucketSelector = selector;
                }
            })
            .AddTo(subscriptions);

        utilityScorerDropdown.UpdateUi(ScorerService.Instance.UtilityScorers, "Utility Scorer", uaiModel.UtilityScorer.GetName());
        utilityScorerDropdown
            .OnDropdownValueChanged
            .Subscribe(uS =>
            {
                if (uaiModel != null)
                {
                    uaiModel.UtilityScorer = uS;
                }
            })
            .AddTo(subscriptions);
        TimerService.Instance.LogCall(sw.ElapsedMilliseconds, "UpdateInternal AI");
    }

    ~AiViewModel()
    {
        bucketTabSub?.Dispose();
        subscriptions.Clear();
    }
}
