using System.Collections;
using UnityEngine;

public sealed class BattleHUDController : MonoBehaviour
{
    [SerializeField] private SquadBattleBootstrap squadBootstrap;
    [SerializeField] private BattleSquadStatusPresenter squadStatusPresenter;
    [SerializeField] private InitiativeQueuePresenter initiativePresenter;
    [SerializeField] private BattleActionBarView actionBar;
    [SerializeField] private AbilityDetailsPanelView abilityDetails;
    [SerializeField] private GameObject hudContentRoot;
    [Header("Runtime state sources")]
    [SerializeField] private BattleSquadSelectionController selectionController;
    [SerializeField] private BattleTurnController turnController;

    private Coroutine bindingRoutine;
    private bool runtimeListenersBound;

    public bool HasBoundPlayer { get; private set; }
    public SquadBattleController BoundPlayerController { get; private set; }
    public SquadBattleController DisplayedController => squadStatusPresenter?.BoundController;
    public AbilityDetailsPanelView AbilityDetails => abilityDetails;
    public int SuccessfulBindingCount { get; private set; }

    public void Configure(
        SquadBattleBootstrap bootstrap,
        BattleSquadStatusPresenter statusPresenter,
        InitiativeQueuePresenter queuePresenter,
        BattleActionBarView configuredActionBar,
        AbilityDetailsPanelView configuredAbilityDetails,
        GameObject contentRoot)
    {
        squadBootstrap = bootstrap;
        squadStatusPresenter = statusPresenter;
        initiativePresenter = queuePresenter;
        actionBar = configuredActionBar;
        abilityDetails = configuredAbilityDetails;
        hudContentRoot = contentRoot;
    }

    public void ConfigureRuntimeState(
        BattleSquadSelectionController selection,
        BattleTurnController turns)
    {
        UnbindRuntimeStateListeners();
        selectionController = selection;
        turnController = turns;
        if (isActiveAndEnabled)
            BindRuntimeStateListeners();
    }

    private void OnEnable()
    {
        actionBar?.SetActionsAvailable(false);
        abilityDetails?.ShowUnavailable();
        BindRuntimeStateListeners();
        bindingRoutine = StartCoroutine(BindWhenBootstrapCompletes());
    }

    private void OnDisable()
    {
        if (bindingRoutine != null)
            StopCoroutine(bindingRoutine);
        bindingRoutine = null;
        squadStatusPresenter?.Unbind();
        initiativePresenter?.Unbind();
        UnbindRuntimeStateListeners();
        BoundPlayerController = null;
        HasBoundPlayer = false;
    }

    public bool TryBindFromProductionState()
    {
        if (HasBoundPlayer)
            return true;
        if (squadBootstrap == null || !squadBootstrap.HasBootstrapped)
            return false;

        SquadBattleController player = null;
        int playerCount = 0;
        foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
        {
            if (controller != null && controller.IsInitialized && controller.Side == BattleSide.Player)
            {
                player = controller;
                playerCount++;
            }
        }

        if (playerCount != 1 || player == null)
        {
            ShowControlledEmptyState(
                $"Battle HUD requires exactly one initialized Player-side squad; found {playerCount}.");
            return false;
        }

        SquadBattleController displayTarget = ResolveDisplayTarget(player);
        if (squadStatusPresenter == null || displayTarget == null ||
            !squadStatusPresenter.Bind(displayTarget))
        {
            ShowControlledEmptyState("Battle HUD could not bind the Player-side squad runtime.");
            return false;
        }

        initiativePresenter?.Bind(
            squadBootstrap.InitiativeOrder,
            selectionController?.SelectedSquad?.SquadId,
            turnController?.ActiveSquadId);
        BoundPlayerController = player;
        HasBoundPlayer = true;
        SuccessfulBindingCount++;
        if (hudContentRoot != null)
            hudContentRoot.SetActive(true);
        return true;
    }

    private SquadBattleController ResolveDisplayTarget(SquadBattleController playerFallback = null)
    {
        SquadBattleController selected = selectionController?.SelectedSquad;
        if (selected != null && selected.IsInitialized)
            return selected;

        SquadBattleController active = turnController?.ActiveSquad;
        if (active != null && active.IsInitialized && active.ControlType == SquadControlType.Human)
            return active;

        return playerFallback ?? BoundPlayerController;
    }

    private void RefreshRuntimeState()
    {
        if (!HasBoundPlayer)
            return;

        SquadBattleController target = ResolveDisplayTarget();
        if (target != null && squadStatusPresenter?.BoundController != target)
            squadStatusPresenter?.Bind(target);
        initiativePresenter?.SetSelectedSquad(selectionController?.SelectedSquad?.SquadId);
        initiativePresenter?.SetActiveSquad(turnController?.ActiveSquadId);
    }

    private void BindRuntimeStateListeners()
    {
        if (runtimeListenersBound)
            return;
        if (selectionController != null)
            selectionController.OnSelectedSquadChanged += HandleSelectedSquadChanged;
        if (turnController != null)
            turnController.OnActiveSquadChanged += HandleActiveSquadChanged;
        runtimeListenersBound = true;
    }

    private void UnbindRuntimeStateListeners()
    {
        if (!runtimeListenersBound)
            return;
        if (selectionController != null)
            selectionController.OnSelectedSquadChanged -= HandleSelectedSquadChanged;
        if (turnController != null)
            turnController.OnActiveSquadChanged -= HandleActiveSquadChanged;
        runtimeListenersBound = false;
    }

    private void HandleSelectedSquadChanged(SquadBattleController controller) => RefreshRuntimeState();
    private void HandleActiveSquadChanged(SquadBattleController controller) => RefreshRuntimeState();

    private IEnumerator BindWhenBootstrapCompletes()
    {
        if (squadBootstrap == null)
        {
            ShowControlledEmptyState("SquadBattleBootstrap reference is missing.");
            yield break;
        }

        while (squadBootstrap.State == SquadBootstrapState.NotInitialized ||
               squadBootstrap.State == SquadBootstrapState.Initializing)
        {
            yield return null;
        }

        bindingRoutine = null;
        if (squadBootstrap.State == SquadBootstrapState.Failed)
        {
            ShowControlledEmptyState(
                $"Squad bootstrap failed: {squadBootstrap.FailureReason}");
            yield break;
        }

        TryBindFromProductionState();
    }

    private void ShowControlledEmptyState(string reason)
    {
        HasBoundPlayer = false;
        BoundPlayerController = null;
        squadStatusPresenter?.ShowEmpty(reason);
        initiativePresenter?.ShowEmpty();
        Debug.LogWarning($"BattleHUDController: {reason}", this);
    }

    public void ShowAttackPreview(
        BattleAttackPreview preview,
        SquadBattleController target,
        AttackDefinition definition)
    {
        Sprite portrait = target?.Runtime?.Data != null
            ? squadStatusPresenter?.GetDisplayPortrait(
                target.Runtime.Data.CommanderPortraitId)
            : null;
        abilityDetails?.ShowAttackPreview(
            preview,
            target?.SquadId ?? "Unavailable target",
            portrait,
            definition);
    }

    public void ShowAttackResult(
        BattleAttackResult result,
        SquadBattleController target)
    {
        abilityDetails?.ShowAttackResult(result);
    }

    public void ClearAttackPreview() => abilityDetails?.ShowUnavailable();

    public void SetBattleCommandsAvailable(bool available)
    {
        actionBar?.SetActionsAvailable(available);
        if (!available)
            abilityDetails?.ShowUnavailable();
    }
}
