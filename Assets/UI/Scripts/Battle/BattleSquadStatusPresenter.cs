using System.Collections.Generic;
using UnityEngine;

public sealed class BattleSquadStatusPresenter : MonoBehaviour
{
    [SerializeField] private BattleSquadStatusView view;
    [SerializeField] private CommanderPortraitDatabase portraitDatabase;

    private readonly HashSet<string> warnedMissingPortraitIds = new HashSet<string>();
    private CommanderPortraitService portraitService;
    private SquadBattleController controller;
    private SquadBattleRuntime runtime;

    public bool IsBound => runtime != null;
    public SquadBattleController BoundController => controller;
    public CommanderPortraitDatabase PortraitDatabase => portraitDatabase;

    public void Configure(
        BattleSquadStatusView configuredView,
        CommanderPortraitDatabase configuredPortraitDatabase)
    {
        Unbind();
        view = configuredView;
        portraitDatabase = configuredPortraitDatabase;
        portraitService = portraitDatabase != null
            ? new CommanderPortraitService(portraitDatabase)
            : null;
    }

    public bool Bind(SquadBattleController squadController)
    {
        Unbind();
        if (squadController == null || !squadController.IsInitialized || squadController.Runtime == null)
        {
            view?.ShowEmpty();
            return false;
        }

        controller = squadController;
        runtime = squadController.Runtime;
        runtime.OnSquadStatsChanged += HandleStatsChanged;
        runtime.OnSquadCompositionChanged += HandleCompositionChanged;
        runtime.OnSquadHPChanged += HandleHealthChanged;
        runtime.OnMoraleChanged += HandleMoraleChanged;
        runtime.OnActionPointsChanged += HandleActionPointsChanged;
        Refresh();
        return true;
    }

    public void Unbind()
    {
        if (runtime != null)
        {
            runtime.OnSquadStatsChanged -= HandleStatsChanged;
            runtime.OnSquadCompositionChanged -= HandleCompositionChanged;
            runtime.OnSquadHPChanged -= HandleHealthChanged;
            runtime.OnMoraleChanged -= HandleMoraleChanged;
            runtime.OnActionPointsChanged -= HandleActionPointsChanged;
        }
        runtime = null;
        controller = null;
    }

    public void ShowEmpty(string reason = null)
    {
        Unbind();
        view?.ShowEmpty(reason);
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Refresh()
    {
        if (runtime == null)
        {
            view?.ShowEmpty();
            return;
        }

        Sprite portrait = ResolvePortrait(runtime.Data.CommanderPortraitId);
        view?.Render(BattleSquadStatusModel.FromRuntime(runtime, portrait));
    }

    private Sprite ResolvePortrait(string portraitId)
    {
        if (portraitService == null && portraitDatabase != null)
            portraitService = new CommanderPortraitService(portraitDatabase);

        bool hasConfiguredPortrait =
            portraitDatabase != null &&
            portraitDatabase.TryGetById(portraitId, out CommanderPortraitEntry entry) &&
            entry != null &&
            entry.Sprite != null;
        Sprite sprite = portraitService?.GetDisplaySprite(portraitId);

        if (!hasConfiguredPortrait)
        {
            string warningKey = string.IsNullOrWhiteSpace(portraitId) ? "<empty>" : portraitId;
            if (warnedMissingPortraitIds.Add(warningKey))
            {
                Debug.LogWarning(
                    $"BattleSquadStatusPresenter: portrait '{warningKey}' is unavailable; " +
                    "the configured UI development fallback will be shown.",
                    this);
            }
        }
        return sprite != null ? sprite : portraitDatabase?.FallbackPortrait;
    }

    private void HandleStatsChanged(SquadCalculatedStats value) => Refresh();
    private void HandleCompositionChanged() => Refresh();
    private void HandleHealthChanged(int value) => Refresh();
    private void HandleMoraleChanged(float value) => Refresh();
    private void HandleActionPointsChanged(int value) => Refresh();
}
