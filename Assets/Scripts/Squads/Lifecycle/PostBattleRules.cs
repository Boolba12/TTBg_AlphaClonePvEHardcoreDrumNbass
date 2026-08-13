using System;
using UnityEngine;

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
