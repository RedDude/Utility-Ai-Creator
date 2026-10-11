using System;
using System.Collections.Generic;
using UniRx;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
    public class ResponseCurveMinimizedViewModel : VisualElement
    {
        private ResponseCurve responseCurve;
        private string ownerName;

        private readonly DropdownField curveDropDown;
        private readonly LineChartButton responseCurveButton;
        private readonly Button inverseButton;
        private ResponseCurveEditorOverlay curveEditor;

        public IObservable<ResponseCurve> OnResponseCurveChanged => onResponseCurveChanged;
        private readonly Subject<ResponseCurve> onResponseCurveChanged = new Subject<ResponseCurve>();

        private readonly CompositeDisposable rcDisposable = new CompositeDisposable();
        private readonly CompositeDisposable rcWindowDisposable = new CompositeDisposable();
        private readonly CompositeDisposable disposable = new CompositeDisposable();

        public ResponseCurveMinimizedViewModel()
        {
            var root = AssetService.GetTemplateContainer(GetType());
            Add(root);
            styleSheets.Add(StylesService.GetStyleSheet("ResponseCurveMinimizedComponent"));

            // Header
            curveDropDown = root.Q<DropdownField>("ResponseCurve-DropDown");
            SearchableDropdown.Attach(curveDropDown);
            TemplateService.Instance.ResponseCurves.OnValueChanged
                .Subscribe(InitCurveDropDownChoices)
                .AddTo(disposable);
            InitCurveDropDownChoices(TemplateService.Instance.ResponseCurves.Values);
            curveDropDown.SetValueWithoutNotify(null);
            curveDropDown.RegisterCallback<ChangeEvent<string>>(OnCurveDropdownValueChanged);
        
            // Body
            responseCurveButton = new LineChartButton
            {
                name = "ResponseCurveButton"
            };

            responseCurveButton.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button == 0) OpenCurveEditor();
            });

            var body = root.Q<VisualElement>("Body");
            body.Add(responseCurveButton);
        
            // Footer
            // Save-as-template and reverse sit to the right of the curve picker; both are also in the curve's context menu.
            var header = root.Q<VisualElement>("Header");
            // The picker shrinks (and truncates its text) so save / reverse always fit above the curve.
            curveDropDown.style.minWidth = 0;
            curveDropDown.style.flexShrink = 1;
            curveDropDown.style.flexBasis = 0;
            curveDropDown.style.marginRight = 0;
            curveDropDown.labelElement.style.display = DisplayStyle.None;
            curveDropDown.Q(className: "unity-base-popup-field__input").style.minWidth = 0;
            header.style.overflow = Overflow.Hidden;
            var saveTemplateButton = IconButtonService.CreateSave("Save this curve as a template");
            saveTemplateButton.RegisterCallback<ClickEvent>(_ => SaveTemplate());
            header.Add(saveTemplateButton);

            inverseButton = new Button { text = "Reverse", tooltip = "Flip the curve (inverse)" };
            inverseButton.style.flexShrink = 0;
            inverseButton.style.marginLeft = 2;
            inverseButton.style.marginRight = 0;
            inverseButton.RegisterCallback<ClickEvent>(_ => ToggleReverse());
            header.Add(inverseButton);

            responseCurveButton.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Save as template", _ => SaveTemplate());
                evt.menu.AppendAction("Reverse", _ => ToggleReverse(),
                    _ => responseCurve != null && responseCurve.IsInversed
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
                evt.menu.AppendAction("Open curve editor", _ => OpenCurveEditor());
            }));
        }

        public void UpdateUi(ResponseCurve newResponseCurve, string newOwnerName)
        {
            responseCurve = newResponseCurve;
            ownerName = newOwnerName;
            rcDisposable.Clear();
            responseCurve.OnCurveValueChanged
                .Subscribe(ReDrawCurve)
                .AddTo(rcDisposable);
            responseCurve.OnFunctionsChanged
                .Subscribe(ReDrawCurve)
                .AddTo(rcDisposable);
            ShowReversed(responseCurve.IsInversed);
            curveDropDown.SetValueWithoutNotify(responseCurve.Name);

            UpdateCurveEditor();
            responseCurveButton.UpdateUi(responseCurve);
        }

        private void ReDrawCurve(bool notUsed)
        {
            responseCurveButton.UpdateUi(responseCurve);
        }

        private void OpenCurveEditor()
        {
            if (responseCurve == null || (curveEditor != null && curveEditor.IsOpen)) return;

            // The editor takes over the window this view lives in until Back is pressed.
            curveEditor = new ResponseCurveEditorOverlay();
            rcWindowDisposable.Clear();
            curveEditor.Editor.OnResponseCurveChanged
                .Subscribe(curve => onResponseCurveChanged.OnNext(curve))
                .AddTo(rcWindowDisposable);

            // Edits apply live, so "Back without saving" puts this snapshot of the curve back.
            var original = responseCurve;
            var snapshot = (ResponseCurve)original.Clone();
            curveEditor.OnClosed
                .Subscribe(discardChanges =>
                {
                    rcWindowDisposable.Clear();
                    if (!discardChanges) return;

                    original.RestoreFromSnapshot(snapshot);
                    // A different curve may have been picked inside the editor: go back to the original one.
                    if (responseCurve != original) onResponseCurveChanged.OnNext(original);
                });
            curveEditor.Open(this);
            UpdateCurveEditor();
        }

        private void ToggleReverse()
        {
            if (responseCurve == null) return;
            responseCurve.IsInversed = !responseCurve.IsInversed;
            ShowReversed(responseCurve.IsInversed);
        }

        // The button reads as "on" while the curve is reversed.
        private void ShowReversed(bool reversed)
        {
            IconButtonService.ShowToggled(inverseButton, reversed);
        }

        private async void SaveTemplate()
        {
            if (responseCurve == null) return;
            var clone = await responseCurve.CloneAsync();
            TemplateService.Instance.Add(clone);
            curveDropDown.SetValueWithoutNotify(responseCurve.Name);
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
            // curveDropDown.SetValueWithoutNotify(responseCurve.Name);
        }
        
        private void InitCurveDropDownChoices(List<AiObjectModel> responseCurveTemplates)
        {
            curveDropDown.choices = AddCopyService.GetChoices(typeof(ResponseCurve), responseCurveTemplates);
            curveDropDown.SetValueWithoutNotify(null);
        }

        // Keeps the open editor (if any) on the current curve.
        private void UpdateCurveEditor()
        {
            if (curveEditor == null || !curveEditor.IsOpen || responseCurve == null) return;
            curveEditor.Editor.UpdateUi(responseCurve, true, ownerName);
        }

        ~ResponseCurveMinimizedViewModel()
        {
            disposable.Clear();
            rcDisposable.Clear();
            rcWindowDisposable.Clear();
        }
    }