using System;
using System.Collections.Generic;

public enum PreBattleSquadUnavailableReason
{
    None,
    MissingData,
    MissingStableId,
    CommanderLost,
    InactiveNoWarriors,
    NoWarriors,
    InvalidPersistentData
}

public sealed class PreBattleSquadOption
{
    public string SquadId { get; }
    public string CommanderId { get; }
    public string PortraitId { get; }
    public CommanderRace Race { get; }
    public PersistentSquadStatus Status { get; }
    public int LivingWarriors { get; }
    public int MaximumWarriors { get; }
    public SquadCalculatedStats CalculatedStats { get; }
    public PreBattleSquadUnavailableReason UnavailableReason { get; }
    public string UnavailableMessage { get; }
    public bool IsAvailable => UnavailableReason == PreBattleSquadUnavailableReason.None;

    public PreBattleSquadOption(
        SquadData squad,
        PreBattleSquadUnavailableReason unavailableReason,
        string unavailableMessage,
        EquipmentDefinitionCatalog equipmentCatalog = null)
    {
        SquadId = squad?.Id ?? string.Empty;
        CommanderId = squad?.Commander?.id ?? string.Empty;
        PortraitId = squad?.CommanderPortraitId ?? string.Empty;
        Race = squad?.Commander?.race ?? default;
        Status = squad?.Status ?? PersistentSquadStatus.InactiveNoWarriors;
        LivingWarriors = squad?.Warriors?.Count ?? 0;
        MaximumWarriors = SquadData.MaximumWarriors;
        CalculatedStats = SquadStatsCalculator.Calculate(squad, equipmentCatalog);
        UnavailableReason = unavailableReason;
        UnavailableMessage = unavailableMessage ?? string.Empty;
    }
}

public static class PreBattleSquadSelectionService
{
    public static IReadOnlyList<PreBattleSquadOption> BuildOptions(
        IReadOnlyList<SquadData> squads,
        EquipmentDefinitionCatalog equipmentCatalog = null)
    {
        List<PreBattleSquadOption> options = new List<PreBattleSquadOption>();
        if (squads != null)
        {
            for (int i = 0; i < squads.Count; i++)
            {
                Evaluate(squads[i], out PreBattleSquadUnavailableReason reason, out string message);
                options.Add(new PreBattleSquadOption(
                    squads[i], reason, message, equipmentCatalog));
            }
        }

        options.Sort((left, right) => string.Compare(
            left.SquadId,
            right.SquadId,
            StringComparison.Ordinal));
        return options;
    }

    public static bool TryResolveEligible(
        SquadSaveParticipant repository,
        string squadId,
        out SquadData squad,
        out string reason)
    {
        squad = null;
        if (repository == null)
        {
            reason = "Persistent squad repository is unavailable.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(squadId))
        {
            reason = "Select a squad before confirming battle preparation.";
            return false;
        }

        squad = repository.GetSquad(squadId);
        if (!Evaluate(squad, out _, out reason))
        {
            squad = null;
            return false;
        }
        return true;
    }

    public static bool Evaluate(
        SquadData squad,
        out PreBattleSquadUnavailableReason unavailableReason,
        out string message)
    {
        if (squad == null)
            return Fail(PreBattleSquadUnavailableReason.MissingData, "Squad data is missing.", out unavailableReason, out message);
        if (string.IsNullOrWhiteSpace(squad.Id))
            return Fail(PreBattleSquadUnavailableReason.MissingStableId, "Squad stable ID is missing.", out unavailableReason, out message);
        if (squad.Status == PersistentSquadStatus.CommanderLost)
            return Fail(PreBattleSquadUnavailableReason.CommanderLost, "Commander lost — squad unavailable.", out unavailableReason, out message);
        if (squad.Status == PersistentSquadStatus.InactiveNoWarriors)
            return Fail(PreBattleSquadUnavailableReason.InactiveNoWarriors, "No battle-ready warriors remain.", out unavailableReason, out message);
        if (squad.Warriors == null || squad.Warriors.Count < SquadData.MinimumWarriors)
            return Fail(PreBattleSquadUnavailableReason.NoWarriors, "At least one living warrior is required.", out unavailableReason, out message);

        SquadValidationResult validation = squad.Validate();
        if (validation == null || !validation.IsValid || !squad.IsBattleEligible)
        {
            return Fail(
                PreBattleSquadUnavailableReason.InvalidPersistentData,
                validation?.ToString() ?? "Persistent squad data is invalid.",
                out unavailableReason,
                out message);
        }

        unavailableReason = PreBattleSquadUnavailableReason.None;
        message = string.Empty;
        return true;
    }

    private static bool Fail(
        PreBattleSquadUnavailableReason reason,
        string failureMessage,
        out PreBattleSquadUnavailableReason unavailableReason,
        out string message)
    {
        unavailableReason = reason;
        message = failureMessage;
        return false;
    }
}
