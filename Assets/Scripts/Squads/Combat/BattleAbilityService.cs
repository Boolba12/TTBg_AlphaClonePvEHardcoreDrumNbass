using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class BattleAbilityService : MonoBehaviour
{
    [SerializeField] private SquadBattleBootstrap squadBootstrap;
    [SerializeField] private BattleTurnController turnController;
    [SerializeField] private BattleSquadSelectionController selectionController;
    [SerializeField] private SquadMovementService movementService;
    [SerializeField] private BattleAttackService attackService;
    [SerializeField] private BattleCompletionController completionController;
    [SerializeField] private List<AbilityDefinition> abilities = new List<AbilityDefinition>();

    private readonly Dictionary<string, BattleAbilityRuntimeState> runtimeStates =
        new Dictionary<string, BattleAbilityRuntimeState>(StringComparer.Ordinal);
    private readonly Dictionary<string, AbilityDefinition> definitions =
        new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal);

    public bool IsInitialized { get; private set; }
    public bool IsExecuting { get; private set; }
    public bool CommandsEnabled { get; private set; } = true;
    public IReadOnlyList<AbilityDefinition> Abilities => abilities;

    public event Action<BattleAbilityResult> OnAbilityStarted;
    public event Action<BattleAbilityResult> OnAbilityResolved;
    public event Action<BattleAbilityRuntimeState> OnCooldownChanged;

    public void Configure(
        SquadBattleBootstrap bootstrap,
        BattleTurnController turns,
        BattleSquadSelectionController selection,
        SquadMovementService movement,
        BattleAttackService attacks,
        BattleCompletionController completion,
        IEnumerable<AbilityDefinition> configuredAbilities)
    {
        UnbindListeners();
        squadBootstrap = bootstrap;
        turnController = turns;
        selectionController = selection;
        movementService = movement;
        attackService = attacks;
        completionController = completion;
        abilities = configuredAbilities == null
            ? new List<AbilityDefinition>()
            : configuredAbilities.Where(ability => ability != null).ToList();
        definitions.Clear();
        runtimeStates.Clear();
        IsInitialized = false;
        IsExecuting = false;
    }

    public bool Initialize()
    {
        if (IsInitialized || squadBootstrap == null || !squadBootstrap.HasBootstrapped ||
            turnController == null || !turnController.HasStarted ||
            selectionController == null || !selectionController.IsInitialized ||
            movementService == null || !movementService.IsInitialized ||
            attackService == null || !attackService.IsInitialized ||
            completionController == null ||
            completionController.State != BattleCompletionState.Running ||
            abilities == null || abilities.Count == 0)
        {
            return false;
        }

        definitions.Clear();
        foreach (AbilityDefinition ability in abilities)
        {
            if (ability == null || !ability.Validate(out string _) ||
                !definitions.TryAdd(ability.StableId, ability))
            {
                definitions.Clear();
                return false;
            }
        }

        runtimeStates.Clear();
        foreach (SquadBattleController controller in squadBootstrap.SpawnedControllers)
        {
            if (controller == null || string.IsNullOrWhiteSpace(controller.SquadId))
                return false;
            foreach (AbilityDefinition ability in abilities)
            {
                string key = CreateKey(controller.SquadId, ability.StableId);
                runtimeStates.Add(key, new BattleAbilityRuntimeState
                {
                    squadId = controller.SquadId,
                    abilityId = ability.StableId
                });
            }
        }

        BindListeners();
        CommandsEnabled = true;
        IsInitialized = true;
        return true;
    }

    public void SetCommandsEnabled(bool enabled) => CommandsEnabled = enabled;

    public AbilityDefinition GetDefinition(string abilityId)
    {
        definitions.TryGetValue(abilityId ?? string.Empty, out AbilityDefinition definition);
        return definition;
    }

    public BattleAbilityRuntimeState GetRuntimeState(string squadId, string abilityId)
    {
        runtimeStates.TryGetValue(CreateKey(squadId, abilityId), out BattleAbilityRuntimeState state);
        return state;
    }

    public IReadOnlyList<BattleAbilityUsageRecord> CreateUsageRecords()
    {
        return runtimeStates.Values
            .Where(state => state.usesThisBattle > 0)
            .OrderBy(state => state.squadId, StringComparer.Ordinal)
            .ThenBy(state => state.abilityId, StringComparer.Ordinal)
            .Select(state => new BattleAbilityUsageRecord
            {
                squadId = state.squadId,
                abilityId = state.abilityId,
                uses = state.usesThisBattle
            })
            .ToList();
    }

    public BattleAbilityValidationResult ValidateAvailability(
        SquadBattleController caster,
        AbilityDefinition definition,
        bool requireSelected = false,
        BattleCommandAuthority authority = BattleCommandAuthority.HumanInput)
    {
        BattleAbilityValidationResult casterValidation = ValidateCaster(
            caster, definition, requireSelected, authority);
        if (!casterValidation.IsValid)
            return casterValidation;

        if (definition.EffectType == BattleAbilityEffectType.RestoreMorale)
        {
            if (caster.Runtime.State.currentMorale >= caster.Runtime.Stats.Morale)
            {
                return BattleAbilityValidationResult.Reject(
                    BattleAbilityFailureReason.MoraleAlreadyFull,
                    "Morale is already full.");
            }
            return BattleAbilityValidationResult.Accepted;
        }

        BattleAttackValidationResult attack = attackService.ValidateAvailability(
            caster,
            definition.AttackEffect,
            requireSelected,
            true,
            authority);
        return MapAttackValidation(attack);
    }

    public BattleAbilityValidationResult ValidateCommand(
        SquadBattleController caster,
        SquadBattleController target,
        AbilityDefinition definition,
        BattleCommandAuthority authority = BattleCommandAuthority.HumanInput)
    {
        BattleAbilityValidationResult casterValidation = ValidateCaster(
            caster, definition, true, authority);
        if (!casterValidation.IsValid)
            return casterValidation;

        if (definition.TargetType == BattleAbilityTargetType.Self)
        {
            if (target != caster)
            {
                return BattleAbilityValidationResult.Reject(
                    BattleAbilityFailureReason.InvalidTarget,
                    "This ability targets its own squad.");
            }
            return caster.Runtime.State.currentMorale < caster.Runtime.Stats.Morale
                ? BattleAbilityValidationResult.Accepted
                : BattleAbilityValidationResult.Reject(
                    BattleAbilityFailureReason.MoraleAlreadyFull,
                    "Morale is already full.");
        }

        return MapAttackValidation(attackService.ValidateCommand(
            caster,
            target,
            definition.AttackEffect,
            authority));
    }

    public BattleAbilityPreview PreviewAbility(
        SquadBattleController caster,
        SquadBattleController target,
        AbilityDefinition definition,
        BattleCommandAuthority authority = BattleCommandAuthority.HumanInput)
    {
        BattleAbilityValidationResult validation = definition != null &&
            definition.TargetType == BattleAbilityTargetType.Self
            ? ValidateCommand(caster, caster, definition, authority)
            : ValidateCommand(caster, target, definition, authority);
        BattleAbilityRuntimeState state = GetRuntimeState(
            caster?.SquadId,
            definition?.StableId);
        BattleAttackPreview attackPreview = default;
        float currentMorale = caster?.Runtime?.State?.currentMorale ?? 0f;
        float maximumMorale = caster?.Runtime?.Stats.Morale ?? 0f;
        float restored = 0f;
        if (definition?.EffectType == BattleAbilityEffectType.PhysicalAttack)
        {
            attackPreview = attackService.PreviewAttack(
                caster,
                target,
                definition.AttackEffect,
                authority);
        }
        else if (definition != null)
        {
            restored = Mathf.Min(
                definition.MoraleRestore,
                Mathf.Max(0f, maximumMorale - currentMorale));
        }

        return new BattleAbilityPreview(
            caster?.SquadId,
            target?.SquadId,
            definition?.StableId,
            validation,
            definition?.ActionPointCost ?? 0,
            state?.remainingCooldown ?? 0,
            attackPreview,
            currentMorale,
            maximumMorale,
            restored);
    }

    public bool TryExecuteAbility(
        SquadBattleController caster,
        SquadBattleController target,
        AbilityDefinition definition,
        out BattleAbilityResult result,
        BattleCommandAuthority authority = BattleCommandAuthority.HumanInput)
    {
        result = new BattleAbilityResult
        {
            CasterSquadId = caster?.SquadId ?? string.Empty,
            TargetSquadId = target?.SquadId ?? string.Empty,
            AbilityId = definition?.StableId ?? string.Empty
        };
        BattleAbilityValidationResult validation = ValidateCommand(
            caster,
            target,
            definition,
            authority);
        if (!validation.IsValid)
        {
            result.FailureReason = validation.FailureReason;
            result.FailureMessage = validation.Reason;
            return false;
        }
        if (!completionController.BeginCommittedResolution())
        {
            result.FailureReason = BattleAbilityFailureReason.BattleCompleted;
            result.FailureMessage = "Battle completion no longer accepts committed commands.";
            return false;
        }

        IsExecuting = true;
        bool committed = false;
        try
        {
            Raise(OnAbilityStarted, result);
            if (definition.EffectType == BattleAbilityEffectType.PhysicalAttack)
            {
                bool accepted = attackService.TryExecuteAttack(
                    caster,
                    target,
                    out BattleAttackResult attack,
                    definition.AttackEffect,
                    authority);
                result.CopyAttackResult(attack);
                if (!accepted || !attack.WasExecuted)
                {
                    BattleAbilityValidationResult mapped = MapAttackFailure(attack);
                    result.FailureReason = mapped.FailureReason;
                    result.FailureMessage = mapped.Reason;
                    return false;
                }
                committed = true;
            }
            else
            {
                if (!caster.Runtime.TrySpendActionPoints(definition.ActionPointCost))
                {
                    result.FailureReason = BattleAbilityFailureReason.InsufficientActionPoints;
                    result.FailureMessage = "Action points changed before Rally could commit.";
                    return false;
                }
                result.ActionPointsSpent = definition.ActionPointCost;
                result.MoraleRestored = caster.Runtime.TryRestoreMorale(
                    definition.MoraleRestore);
                if (result.MoraleRestored <= 0f)
                {
                    caster.Runtime.RestoreActionPointsAfterFailedCommit(
                        result.ActionPointsSpent);
                    result.ActionPointsSpent = 0;
                    result.FailureReason = BattleAbilityFailureReason.MoraleAlreadyFull;
                    result.FailureMessage = "Morale changed before Rally could commit.";
                    return false;
                }
                result.WasExecuted = true;
                result.Hit = true;
                committed = true;
            }

            BattleAbilityRuntimeState state = GetRuntimeState(
                caster.SquadId,
                definition.StableId);
            state.BeginCooldown(definition.CooldownRounds);
            result.CooldownApplied = definition.CooldownRounds;
            OnCooldownChanged?.Invoke(state);
            if (definition.HasProgressionStat)
            {
                result.ProgressionIncreased = caster.Runtime.TryIncreaseUsedPrimaryStat(
                    definition.ProgressionStat);
            }
            return true;
        }
        catch (Exception exception)
        {
            result.FailureReason = BattleAbilityFailureReason.RuntimeFailure;
            result.FailureMessage = exception.Message;
            Debug.LogException(exception, this);
            return false;
        }
        finally
        {
            IsExecuting = false;
            if (committed && result.FailureReason == BattleAbilityFailureReason.None)
                Raise(OnAbilityResolved, result);
            completionController.EndCommittedResolution();
        }
    }

    private BattleAbilityValidationResult ValidateCaster(
        SquadBattleController caster,
        AbilityDefinition definition,
        bool requireSelected,
        BattleCommandAuthority authority)
    {
        if (!IsInitialized)
            return Reject(BattleAbilityFailureReason.ServiceNotInitialized, "Ability service is not initialized.");
        if (!CommandsEnabled || completionController.State != BattleCompletionState.Running ||
            turnController.IsBattleLocked)
            return Reject(BattleAbilityFailureReason.BattleCompleted, "Battle commands are locked.");
        if (IsExecuting || attackService.IsExecuting)
            return Reject(BattleAbilityFailureReason.CommandInProgress, "Another command is resolving.");
        if (definition == null)
            return Reject(BattleAbilityFailureReason.MissingDefinition, "Ability definition is missing.");
        if (!definitions.ContainsKey(definition.StableId))
            return Reject(BattleAbilityFailureReason.InvalidDefinition, "Ability is not registered.");
        if (!definition.Validate(out string reason))
            return Reject(BattleAbilityFailureReason.InvalidDefinition, reason);
        if (caster == null || !caster.IsInitialized)
            return Reject(BattleAbilityFailureReason.InvalidCaster, "Caster is unavailable.");
        if (!caster.CanAct)
            return Reject(BattleAbilityFailureReason.CasterDefeated, "Defeated squads cannot use abilities.");
        if (!turnController.IsActive(caster))
            return Reject(BattleAbilityFailureReason.CasterNotActive, "Caster is not the active squad.");
        if (authority == BattleCommandAuthority.HumanInput &&
            caster.Side != BattleSide.Player)
            return Reject(BattleAbilityFailureReason.CasterNotPlayerSide, "Only Player-side abilities accept Human input.");
        if (authority == BattleCommandAuthority.HumanInput &&
            caster.ControlType != SquadControlType.Human)
            return Reject(BattleAbilityFailureReason.CasterNotHumanControlled, "AI-controlled squads do not accept Human abilities.");
        if (authority == BattleCommandAuthority.TacticalAI &&
            caster.Side != BattleSide.Enemy)
        {
            return Reject(
                BattleAbilityFailureReason.CasterNotPlayerSide,
                "Enemy Tactical AI only controls Enemy-side squads in AI v0.");
        }
        if (authority == BattleCommandAuthority.TacticalAI &&
            caster.ControlType != SquadControlType.AI)
        {
            return Reject(
                BattleAbilityFailureReason.CasterNotHumanControlled,
                "Only an AI-controlled squad accepts tactical AI ability commands.");
        }
        if (authority == BattleCommandAuthority.HumanInput && requireSelected &&
            selectionController.SelectedSquad != caster)
            return Reject(BattleAbilityFailureReason.CasterNotSelected, "Select the active squad before using an ability.");
        if (movementService.IsMoving)
            return Reject(BattleAbilityFailureReason.MovementInProgress, "Ability is unavailable during movement.");
        if (caster.Runtime.State.currentActionPoints < definition.ActionPointCost)
            return Reject(BattleAbilityFailureReason.InsufficientActionPoints,
                $"{definition.DisplayName} needs {definition.ActionPointCost} AP.");
        BattleAbilityRuntimeState state = GetRuntimeState(caster.SquadId, definition.StableId);
        if (state == null)
            return Reject(BattleAbilityFailureReason.InvalidDefinition, "Ability runtime state is missing.");
        if (state.remainingCooldown > 0)
            return Reject(BattleAbilityFailureReason.CooldownActive,
                $"Cooldown: {state.remainingCooldown} round(s).");
        return BattleAbilityValidationResult.Accepted;
    }

    private void HandleTurnStarted(SquadBattleController owner)
    {
        if (owner == null)
            return;
        foreach (AbilityDefinition ability in abilities)
        {
            BattleAbilityRuntimeState state = GetRuntimeState(owner.SquadId, ability.StableId);
            if (state != null && state.AdvanceOwnerTurn())
                OnCooldownChanged?.Invoke(state);
        }
    }

    private static BattleAbilityValidationResult MapAttackValidation(
        BattleAttackValidationResult attack)
    {
        if (attack.IsValid)
            return BattleAbilityValidationResult.Accepted;
        BattleAbilityFailureReason reason = attack.FailureReason switch
        {
            BattleAttackFailureReason.NoTargetsInRange => BattleAbilityFailureReason.NoTargetsInRange,
            BattleAttackFailureReason.InsufficientActionPoints => BattleAbilityFailureReason.InsufficientActionPoints,
            BattleAttackFailureReason.MovementInProgress => BattleAbilityFailureReason.MovementInProgress,
            BattleAttackFailureReason.AttackInProgress => BattleAbilityFailureReason.CommandInProgress,
            BattleAttackFailureReason.BattleCompleted => BattleAbilityFailureReason.BattleCompleted,
            _ => BattleAbilityFailureReason.InvalidTarget
        };
        return Reject(reason, attack.Reason);
    }

    private static BattleAbilityValidationResult MapAttackFailure(BattleAttackResult attack)
    {
        if (attack == null)
            return Reject(BattleAbilityFailureReason.RuntimeFailure, "Attack pipeline returned no result.");
        return MapAttackValidation(new BattleAttackValidationResult(
            attack.FailureReason,
            attack.FailureMessage));
    }

    private static BattleAbilityValidationResult Reject(
        BattleAbilityFailureReason reason,
        string message) => BattleAbilityValidationResult.Reject(reason, message);

    private static string CreateKey(string squadId, string abilityId) =>
        (squadId ?? string.Empty) + "\u001f" + (abilityId ?? string.Empty);

    private void BindListeners()
    {
        if (turnController != null)
            turnController.OnTurnStarted += HandleTurnStarted;
    }

    private void UnbindListeners()
    {
        if (turnController != null)
            turnController.OnTurnStarted -= HandleTurnStarted;
    }

    private static void Raise(
        Action<BattleAbilityResult> handler,
        BattleAbilityResult result)
    {
        if (handler == null)
            return;
        foreach (Action<BattleAbilityResult> subscriber in handler.GetInvocationList())
        {
            try
            {
                subscriber(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    private void OnDestroy() => UnbindListeners();
}
