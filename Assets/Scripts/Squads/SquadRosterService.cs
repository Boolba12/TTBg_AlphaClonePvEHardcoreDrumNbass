using System;
using System.Collections.Generic;

public enum SquadRosterOperationFailure
{
    None,
    MissingRepository,
    BattleLocked,
    MissingSquad,
    CommanderUnavailable,
    MissingWarrior,
    WarriorNotInReserve,
    WarriorNotAssigned,
    WarriorAssignedElsewhere,
    DuplicateId,
    SquadFull,
    InvalidPersistentState
}

public readonly struct SquadRosterOperationResult
{
    public bool Success => Failure == SquadRosterOperationFailure.None;
    public SquadRosterOperationFailure Failure { get; }
    public string Reason { get; }

    private SquadRosterOperationResult(
        SquadRosterOperationFailure failure,
        string reason)
    {
        Failure = failure;
        Reason = reason ?? string.Empty;
    }

    public static SquadRosterOperationResult Ok(string reason = null) =>
        new SquadRosterOperationResult(SquadRosterOperationFailure.None, reason);

    public static SquadRosterOperationResult Fail(
        SquadRosterOperationFailure failure,
        string reason) => new SquadRosterOperationResult(failure, reason);
}

public readonly struct SquadCompositionStatPreview
{
    public SquadCompositionStatPreview(
        string warriorId,
        bool adding,
        SquadCalculatedStats current,
        SquadCalculatedStats candidate)
    {
        WarriorId = warriorId ?? string.Empty;
        Adding = adding;
        Current = current;
        Candidate = candidate;
    }

    public string WarriorId { get; }
    public bool Adding { get; }
    public SquadCalculatedStats Current { get; }
    public SquadCalculatedStats Candidate { get; }
}

/// <summary>
/// Owns transactional assignment changes between the persistent Player Reserve and squads.
/// It never creates copies of Warriors and never touches battle runtime state.
/// </summary>
public sealed class SquadRosterService
{
    private readonly SquadSaveParticipant repository;
    private readonly EquipmentDefinitionCatalog equipmentCatalog;

    public SquadRosterService(
        SquadSaveParticipant configuredRepository,
        EquipmentDefinitionCatalog configuredEquipmentCatalog = null)
    {
        repository = configuredRepository;
        equipmentCatalog = configuredEquipmentCatalog;
    }

    public SquadRosterOperationResult TryAddWarrior(string squadId, string warriorId)
    {
        SquadRosterOperationResult common = ValidateMutableSquad(squadId, out SquadData squad);
        if (!common.Success)
            return common;
        int reserveIndex = repository.FindReserveIndex(warriorId);
        if (reserveIndex < 0)
            return Fail(SquadRosterOperationFailure.WarriorNotInReserve,
                $"Warrior '{warriorId}' is not available in Reserve.");
        if (squad.Warriors.Count >= SquadData.MaximumWarriors)
            return Fail(SquadRosterOperationFailure.SquadFull, "Squad is full.");
        string assignedSquad = repository.GetAssignedSquadId(warriorId);
        if (!string.IsNullOrWhiteSpace(assignedSquad))
            return Fail(SquadRosterOperationFailure.WarriorAssignedElsewhere,
                $"Warrior '{warriorId}' is already assigned to squad '{assignedSquad}'.");

        WarriorData warrior = repository.ReserveWarriors[reserveIndex];
        string rollback = repository.CaptureState();
        if (!squad.TryAddWarrior(warrior, false, out string error))
            return Fail(MapFailure(error), error);
        WarriorData removed = repository.RemoveReserveAt(reserveIndex);
        if (!ReferenceEquals(removed, warrior))
        {
            repository.RestoreState(rollback);
            return Fail(SquadRosterOperationFailure.InvalidPersistentState,
                "Reserve changed during assignment; the operation was rolled back.");
        }
        return ValidateAfterMutation("Warrior assigned to squad.", rollback);
    }

    public SquadRosterOperationResult TryRemoveWarrior(string squadId, string warriorId)
    {
        SquadRosterOperationResult common = ValidateMutableRepository(out SquadData squad, squadId);
        if (!common.Success)
            return common;
        WarriorData warrior = squad.GetWarrior(warriorId);
        if (warrior == null)
            return Fail(SquadRosterOperationFailure.WarriorNotAssigned,
                $"Warrior '{warriorId}' is not assigned to squad '{squadId}'.");
        if (repository.GetReserveWarrior(warriorId) != null)
            return Fail(SquadRosterOperationFailure.DuplicateId,
                $"Warrior '{warriorId}' is already duplicated in Reserve.");

        string rollback = repository.CaptureState();
        if (!squad.TryRemoveWarrior(warriorId, false, out string error))
            return Fail(MapFailure(error), error);
        repository.AddReserveUnchecked(warrior);
        return ValidateAfterMutation("Warrior returned to Reserve.", rollback);
    }

    public SquadRosterOperationResult TryRotateWarrior(
        string squadId,
        string assignedWarriorId,
        string reserveWarriorId)
    {
        SquadRosterOperationResult common = ValidateMutableSquad(squadId, out SquadData squad);
        if (!common.Success)
            return common;
        if (string.Equals(assignedWarriorId, reserveWarriorId, StringComparison.Ordinal))
            return Fail(SquadRosterOperationFailure.DuplicateId,
                "Rotation requires two different Warrior IDs.");
        if (squad.GetWarrior(assignedWarriorId) == null)
            return Fail(SquadRosterOperationFailure.WarriorNotAssigned,
                $"Warrior '{assignedWarriorId}' is not assigned to squad '{squadId}'.");
        int reserveIndex = repository.FindReserveIndex(reserveWarriorId);
        if (reserveIndex < 0)
            return Fail(SquadRosterOperationFailure.WarriorNotInReserve,
                $"Warrior '{reserveWarriorId}' is not available in Reserve.");
        string assignedSquad = repository.GetAssignedSquadId(reserveWarriorId);
        if (!string.IsNullOrWhiteSpace(assignedSquad))
            return Fail(SquadRosterOperationFailure.WarriorAssignedElsewhere,
                $"Warrior '{reserveWarriorId}' is assigned to squad '{assignedSquad}'.");

        WarriorData incoming = repository.ReserveWarriors[reserveIndex];
        string rollback = repository.CaptureState();
        if (!squad.TryReplaceWarrior(assignedWarriorId, incoming, false,
                out WarriorData outgoing, out string error))
            return Fail(MapFailure(error), error);
        repository.ReplaceReserveAt(reserveIndex, outgoing);
        return ValidateAfterMutation("Squad rotation completed.", rollback);
    }

    public SquadRosterOperationResult PreviewAdd(
        string squadId,
        string reserveWarriorId,
        out SquadCompositionStatPreview preview)
    {
        preview = default;
        SquadRosterOperationResult common = ValidateReadableSquad(squadId, out SquadData squad);
        if (!common.Success)
            return common;
        WarriorData warrior = repository.GetReserveWarrior(reserveWarriorId);
        if (warrior == null)
            return Fail(SquadRosterOperationFailure.WarriorNotInReserve,
                $"Warrior '{reserveWarriorId}' is not available in Reserve.");
        if (squad.Warriors.Count >= SquadData.MaximumWarriors)
            return Fail(SquadRosterOperationFailure.SquadFull, "Squad is full.");

        List<WarriorData> candidate = new List<WarriorData>(squad.Warriors) { warrior };
        preview = BuildPreview(squad, warrior.id, true, candidate);
        return SquadRosterOperationResult.Ok();
    }

    public SquadRosterOperationResult PreviewRemove(
        string squadId,
        string assignedWarriorId,
        out SquadCompositionStatPreview preview)
    {
        preview = default;
        SquadRosterOperationResult common = ValidateReadableSquad(squadId, out SquadData squad);
        if (!common.Success)
            return common;
        WarriorData warrior = squad.GetWarrior(assignedWarriorId);
        if (warrior == null)
            return Fail(SquadRosterOperationFailure.WarriorNotAssigned,
                $"Warrior '{assignedWarriorId}' is not assigned to squad '{squadId}'.");

        List<WarriorData> candidate = new List<WarriorData>();
        for (int i = 0; i < squad.Warriors.Count; i++)
            if (!string.Equals(squad.Warriors[i]?.id, assignedWarriorId,
                    StringComparison.Ordinal))
                candidate.Add(squad.Warriors[i]);
        preview = BuildPreview(squad, warrior.id, false, candidate);
        return SquadRosterOperationResult.Ok();
    }

    private SquadCompositionStatPreview BuildPreview(
        SquadData squad,
        string warriorId,
        bool adding,
        IReadOnlyList<WarriorData> candidate)
    {
        return new SquadCompositionStatPreview(
            warriorId,
            adding,
            SquadStatsCalculator.Calculate(squad, equipmentCatalog),
            SquadStatsCalculator.CalculateComposition(squad, candidate, equipmentCatalog));
    }

    private SquadRosterOperationResult ValidateMutableSquad(
        string squadId,
        out SquadData squad)
    {
        SquadRosterOperationResult result = ValidateMutableRepository(out squad, squadId);
        if (!result.Success)
            return result;
        if (squad.Commander == null || squad.Status == PersistentSquadStatus.CommanderLost)
            return Fail(SquadRosterOperationFailure.CommanderUnavailable,
                "Commander is unavailable; this squad cannot receive or rotate Warriors.");
        return SquadRosterOperationResult.Ok();
    }

    private SquadRosterOperationResult ValidateMutableRepository(
        out SquadData squad,
        string squadId)
    {
        squad = null;
        if (repository == null)
            return Fail(SquadRosterOperationFailure.MissingRepository,
                "Persistent squad repository is unavailable.");
        if (repository.IsCompositionLocked)
            return Fail(SquadRosterOperationFailure.BattleLocked,
                "Squad composition is locked for the active battle.");
        return ValidateReadableSquad(squadId, out squad);
    }

    private SquadRosterOperationResult ValidateReadableSquad(
        string squadId,
        out SquadData squad)
    {
        squad = repository?.GetSquad(squadId);
        if (squad == null)
            return Fail(SquadRosterOperationFailure.MissingSquad,
                $"Persistent squad '{squadId}' was not found.");
        return SquadRosterOperationResult.Ok();
    }

    private SquadRosterOperationResult ValidateAfterMutation(
        string reason,
        string rollback)
    {
        if (repository.ValidateRosterInvariants(out string error))
            return SquadRosterOperationResult.Ok(reason);
        repository.RestoreState(rollback);
        return Fail(SquadRosterOperationFailure.InvalidPersistentState,
            $"Roster invariant failed after mutation; transaction rolled back: {error}");
    }

    private static SquadRosterOperationFailure MapFailure(string error)
    {
        if (error != null && error.IndexOf("more than", StringComparison.OrdinalIgnoreCase) >= 0)
            return SquadRosterOperationFailure.SquadFull;
        if (error != null && error.IndexOf("Duplicate", StringComparison.OrdinalIgnoreCase) >= 0)
            return SquadRosterOperationFailure.DuplicateId;
        return SquadRosterOperationFailure.InvalidPersistentState;
    }

    private static SquadRosterOperationResult Fail(
        SquadRosterOperationFailure failure,
        string reason) => SquadRosterOperationResult.Fail(failure, reason);
}
