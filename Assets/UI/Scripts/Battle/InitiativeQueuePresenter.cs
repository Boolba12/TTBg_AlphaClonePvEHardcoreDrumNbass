using System.Collections.Generic;
using UnityEngine;

public sealed class InitiativeQueuePresenter : MonoBehaviour
{
    [SerializeField] private InitiativeQueueView view;
    [SerializeField] private CommanderPortraitDatabase portraitDatabase;

    private readonly List<SquadBattleController> subscribedControllers =
        new List<SquadBattleController>();
    private SquadInitiativeOrder order;
    private string selectedSquadId;

    public bool IsBound => order != null;

    public void Configure(
        InitiativeQueueView configuredView,
        CommanderPortraitDatabase configuredPortraitDatabase)
    {
        Unbind();
        view = configuredView;
        portraitDatabase = configuredPortraitDatabase;
    }

    public void Bind(SquadInitiativeOrder initiativeOrder, string selectedId)
    {
        Unbind();
        order = initiativeOrder;
        selectedSquadId = selectedId;
        if (order == null)
        {
            view?.ShowEmpty();
            return;
        }

        foreach (SquadBattleController controller in order.Entries)
        {
            if (controller?.Runtime == null)
                continue;
            controller.Runtime.OnSquadStatsChanged += HandleStatsChanged;
            controller.Runtime.OnSquadDefeated += HandleSquadDefeated;
            subscribedControllers.Add(controller);
        }
        Refresh();
    }

    public void Unbind()
    {
        foreach (SquadBattleController controller in subscribedControllers)
        {
            if (controller?.Runtime == null)
                continue;
            controller.Runtime.OnSquadStatsChanged -= HandleStatsChanged;
            controller.Runtime.OnSquadDefeated -= HandleSquadDefeated;
        }
        subscribedControllers.Clear();
        order = null;
        selectedSquadId = null;
    }

    public void ShowEmpty()
    {
        Unbind();
        view?.ShowEmpty();
    }

    public static List<InitiativeEntryModel> BuildModels(
        SquadInitiativeOrder initiativeOrder,
        CommanderPortraitDatabase database,
        string selectedId)
    {
        List<InitiativeEntryModel> models = new List<InitiativeEntryModel>();
        if (initiativeOrder == null)
            return models;

        CommanderPortraitService portraitService = database != null
            ? new CommanderPortraitService(database)
            : null;
        foreach (SquadBattleController controller in initiativeOrder.Entries)
        {
            if (controller?.Runtime == null)
                continue;
            Sprite portrait = portraitService?.GetDisplaySprite(
                controller.Runtime.Data.CommanderPortraitId);
            models.Add(new InitiativeEntryModel(
                controller.SquadId,
                portrait,
                controller.Runtime.Stats.Initiative,
                controller.Side,
                controller.SquadId == selectedId,
                controller.Runtime.State.IsDefeated));
        }
        return models;
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Refresh()
    {
        view?.SetEntries(BuildModels(order, portraitDatabase, selectedSquadId));
    }

    private void HandleStatsChanged(SquadCalculatedStats value) => Refresh();
    private void HandleSquadDefeated() => Refresh();
}
