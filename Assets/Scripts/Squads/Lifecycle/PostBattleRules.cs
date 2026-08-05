using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PersistentDebuffDefinition",
    menuName = "Game/Battle/Persistent Debuff")]
public sealed class PersistentDebuffDefinition : ScriptableObject
{
    [SerializeField] private string stableId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private float resolveModifier = -1f;
    [SerializeField] private bool persistent = true;
    [SerializeField] private bool stackable;

    public string StableId => stableId;
    public string DisplayName => displayName;
    public string Description => description;
    public float ResolveModifier => resolveModifier;
    public bool Persistent => persistent;
    public bool Stackable => stackable;

    public bool Validate(out string error)
    {
        if (string.IsNullOrWhiteSpace(stableId))
        {
            error = "Persistent debuff stable ID is missing.";
            return false;
        }
        if (!persistent)
        {
            error = "Post-battle debuff must be persistent.";
            return false;
        }
        error = null;
        return true;
    }

#if UNITY_EDITOR
    public void ConfigureDevelopment(
        string id,
        string label,
        string details,
        float configuredResolveModifier)
    {
        stableId = id;
        displayName = label;
        description = details;
        resolveModifier = configuredResolveModifier;
        persistent = true;
        stackable = false;
    }
#endif
}

[CreateAssetMenu(
    fileName = "PostBattleRules",
    menuName = "Game/Battle/Post-Battle Rules")]
public sealed class PostBattleRules : ScriptableObject
{
    [SerializeField, Range(0f, 1f)] private float defeatedCommanderSurvivalChance = 0.2f;
    [SerializeField] private PersistentDebuffDefinition survivorDebuff;
    [SerializeField] private bool restoreSurvivorsToMaximumHP = true;

    public float DefeatedCommanderSurvivalChance =>
        Mathf.Clamp01(defeatedCommanderSurvivalChance);
    public PersistentDebuffDefinition SurvivorDebuff => survivorDebuff;
    public bool RestoreSurvivorsToMaximumHP => restoreSurvivorsToMaximumHP;

    public bool Validate(out string error)
    {
        if (survivorDebuff == null)
        {
            error = "Post-battle survivor debuff is missing.";
            return false;
        }
        if (!survivorDebuff.Validate(out error))
            return false;
        error = null;
        return true;
    }

#if UNITY_EDITOR
    public void ConfigureDevelopment(
        float survivalChance,
        PersistentDebuffDefinition debuff)
    {
        defeatedCommanderSurvivalChance = Mathf.Clamp01(survivalChance);
        survivorDebuff = debuff;
        restoreSurvivorsToMaximumHP = true;
    }
#endif
}

public interface IPostBattleRandomSource
{
    float Next01();
}

public sealed class SeededPostBattleRandomSource : IPostBattleRandomSource
{
    private readonly System.Random random;

    public SeededPostBattleRandomSource(int seed)
    {
        random = new System.Random(seed);
    }

    public float Next01() => (float)random.NextDouble();
}

public sealed class DevelopmentCommanderPostBattleResolver : ICommanderPostBattleResolver
{
    private readonly PostBattleRules rules;
    private readonly IPostBattleRandomSource random;
    private readonly string battleId;

    public DevelopmentCommanderPostBattleResolver(
        PostBattleRules configuredRules,
        IPostBattleRandomSource randomSource,
        string sourceBattleId)
    {
        rules = configuredRules ?? throw new ArgumentNullException(nameof(configuredRules));
        random = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        battleId = sourceBattleId ?? string.Empty;
    }

    public CommanderPostBattleResult Resolve(
        CommanderData commander,
        SquadBattleState battleState)
    {
        if (commander == null)
            throw new ArgumentNullException(nameof(commander));
        bool defeated = battleState?.commander == null ||
                        battleState.commander.defeated ||
                        battleState.commander.currentHP <= 0;
        if (!defeated)
        {
            return new CommanderPostBattleResult
            {
                commanderId = commander.id,
                survived = true,
                outcomeType = CommanderPostBattleOutcomeType.SurvivedNormally,
                sourceBattleId = battleId
            };
        }

        bool survived = random.Next01() < rules.DefeatedCommanderSurvivalChance;
        return new CommanderPostBattleResult
        {
            commanderId = commander.id,
            survived = survived,
            permanentlyDead = !survived,
            permanentDebuffId = survived ? rules.SurvivorDebuff.StableId : null,
            outcomeType = survived
                ? CommanderPostBattleOutcomeType.SurvivedWithPermanentDebuff
                : CommanderPostBattleOutcomeType.Killed,
            sourceBattleId = battleId
        };
    }
}
