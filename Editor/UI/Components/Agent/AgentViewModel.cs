using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine.UIElements;
using System.Linq;
using MoreLinq;
using UniRx;
using UnityEditor.UIElements;
using UnityEngine;

internal class AgentViewModel: RightPanelComponent<IAgent>
{
    private readonly CompositeDisposable disposables = new CompositeDisposable();
    private readonly Label agentName;
    private readonly DropdownField aiDropdown;
    private readonly AiViewModel aiViewModel;
    private IAgent agent;

    internal AgentViewModel()
    {
        var root = AssetService.GetTemplateContainer(GetType().FullName);
        Add(root);
        styleSheets.Add(StylesService.GetStyleSheet("AiObjectComponent"));
        var body = root.Q<VisualElement>("Body");
        agentName = root.Q<Label>("AgentName");
        aiDropdown = root.Q<DropdownField>("Ai-Dropdown");
        SearchableDropdown.Attach(aiDropdown);
        var footer = root.Q<VisualElement>("Footer");
        aiViewModel = new AiViewModel();
        body.Add(aiViewModel);

        aiDropdown.label = "AI";
        aiDropdown.style.minWidth = 220;
        var tickAgent = new Button();
        tickAgent.text = "Tick agent";
        tickAgent.tooltip = "Run this agent's AI once, right now";
        tickAgent.RegisterCallback<MouseUpEvent>(evt =>
        {
            if (agent != null) UaiTicker.Instance.TickAgent(agent);
        });
        footer.Add(tickAgent);

        var tickAllButton = new Button();
        tickAllButton.text = "Tick all agents";
        tickAllButton.tooltip = "Run every agent's AI once, right now";
        tickAllButton.RegisterCallback<MouseUpEvent>(evt =>
        {
            UaiTicker.Instance.TickAis();
        });
        footer.Add(tickAllButton);

        var applyToAllButton = new Button();
        applyToAllButton.text = "Apply AI to all";
        applyToAllButton.tooltip = "Give every agent of this type a copy of this agent's AI";
        applyToAllButton.RegisterCallback<MouseUpEvent>(evt =>
        {
            if (agent == null) return;
            AgentManager.Instance.GetAgentsByIdentifier(agent.TypeIdentifier).Values
                .ForEach(SetAgentAiAsCurrentAgentsAi);

        });
        footer.Add(applyToAllButton);

        PlayAbleAiService.Instance.OnAisChanged
            .Subscribe(_ =>
            {
                InitDropdown();
            })
            .AddTo(disposables);

        aiDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            // The dropdown also fires while it is being filled, before any agent is selected.
            if (agent?.Uai == null || string.IsNullOrEmpty(evt.newValue)) return;
            if (agent.Uai.Name == evt.newValue) return;
            var newAi = PlayAbleAiService.Instance.GetAiByName(evt.newValue);
            if (newAi == null) return;
            agent.Uai = newAi;
            UpdateAiComponent();
        });
    }

    private async void SetAgentAiAsCurrentAgentsAi(IAgent a)
    {
        if (a == agent) return;
        var aiClone = await agent.Uai.CloneAsync();
        a.SetAi(aiClone as Uai);
    }

    internal override void UpdateUi(IAgent element)
    {
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        if (element == null) return;
        this.agent = element;

        agentName.text = agent.Model.Name;
        TimerService.Instance.LogCall(sw.ElapsedMilliseconds, "AgentComponent Init");
        sw.Restart();
        InitDropdown();
        TimerService.Instance.LogCall(sw.ElapsedMilliseconds, "AgentComponent InitDropdown");
        sw.Restart();
        UpdateAiComponent();
        TimerService.Instance.LogCall(sw.ElapsedMilliseconds, "AgentComponent UpdateAiComponent");
        sw.Restart();
    }

    private void InitDropdown()
    {
        aiDropdown.choices = PlayAbleAiService.Instance.PlayAbleAIs
            .OrderBy(a => a.Name)
            .Select(x => x.Name)
            .ToList();

        if (agent?.Uai != null && aiDropdown.choices.Contains(agent.Uai.Name))
        {
            aiDropdown.SetValueWithoutNotify(agent.Uai.Name);
        }
    }

    internal void UpdateAiComponent()
    {
        if (agent?.Uai == null) return;
        aiViewModel.UpdateUi(agent.Uai);
    }

    ~AgentViewModel()
    {
        disposables.Clear();
    }

}
