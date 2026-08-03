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

    private Coroutine bindingRoutine;

    public bool HasBoundPlayer { get; private set; }
    public SquadBattleController BoundPlayerController { get; private set; }
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

    private void OnEnable()
    {
        actionBar?.SetActionsAvailable(false);
        abilityDetails?.ShowUnavailable();
        bindingRoutine = StartCoroutine(BindWhenBootstrapCompletes());
    }

    private void OnDisable()
    {
        if (bindingRoutine != null)
            StopCoroutine(bindingRoutine);
        bindingRoutine = null;
        squadStatusPresenter?.Unbind();
        initiativePresenter?.Unbind();
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

        if (squadStatusPresenter == null || !squadStatusPresenter.Bind(player))
        {
            ShowControlledEmptyState("Battle HUD could not bind the Player-side squad runtime.");
            return false;
        }

        initiativePresenter?.Bind(squadBootstrap.InitiativeOrder, player.SquadId);
        BoundPlayerController = player;
        HasBoundPlayer = true;
        SuccessfulBindingCount++;
        if (hudContentRoot != null)
            hudContentRoot.SetActive(true);
        return true;
    }

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
}
