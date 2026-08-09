using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class BattleAbilityFrameworkTests
{
    private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        BattleSquadSelectionContext.Clear();
        BattleReturnContext.Clear();
        BattleEncounterContext.Clear();
        BattleSetupContext.ClearConfirmation();
        PendingSaveLoadContext.Clear();
        ResolvedEncounterRegistry.Clear();
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                UnityEngine.Object.DestroyImmediate(cleanup[i]);
        }
        cleanup.Clear();
    }

    [Test]
    public void DefinitionsAreStableValidatedAndDoNotStoreRuntimeCooldown()
    {
        AbilitySetup setup = CreateSetup();

        Assert.That(setup.Power.Validate(out _), Is.True);
        Assert.That(setup.Sweep.Validate(out _), Is.True);
        Assert.That(setup.Rally.Validate(out _), Is.True);
        Assert.That(new[] { setup.Power.StableId, setup.Sweep.StableId, setup.Rally.StableId }
            .Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(3));
        Assert.That(setup.Power.ActionPointCost, Is.EqualTo(3));
        Assert.That(setup.Power.CooldownRounds, Is.EqualTo(1));
        Assert.That(setup.Power.AttackEffect.Distribution,
            Is.EqualTo(SquadDamageDistribution.SingleTarget));
        Assert.That(setup.Sweep.ActionPointCost, Is.EqualTo(4));
        Assert.That(setup.Sweep.CooldownRounds, Is.EqualTo(2));
        Assert.That(setup.Sweep.AttackEffect.Distribution,
            Is.EqualTo(SquadDamageDistribution.Area));
        Assert.That(setup.Rally.ActionPointCost, Is.EqualTo(2));
        Assert.That(setup.Rally.CooldownRounds, Is.EqualTo(2));
        Assert.That(setup.Rally.TargetType, Is.EqualTo(BattleAbilityTargetType.Self));
        Assert.That(setup.Service.GetRuntimeState("player", setup.Power.StableId)
            .remainingCooldown, Is.Zero);
        Assert.That(setup.Power.CooldownRounds, Is.EqualTo(1),
            "Runtime cooldown must not mutate its ScriptableObject definition.");
    }

    [Test]
    public void PowerStrikeUsesExistingHitCritArmorResolverApAndProgression()
    {
        AbilitySetup setup = CreateSetup();
        setup.Player.Runtime.Data.Commander.baseStats.criticalChance = 1f;
        setup.Player.Runtime.RecalculateStats();
        SequenceBattleRandom random = new SequenceBattleRandom(0f, 0f);
        setup.Attacks.SetRandomSourceForTests(random);
        int beforeAP = setup.Player.Runtime.State.currentActionPoints;
        int beforeHP = setup.Enemy.Runtime.State.CurrentSquadHP;
        int progressionEvents = 0;
        setup.Player.Runtime.OnPrimaryStatIncreased += _ => progressionEvents++;

        BattleAbilityPreview preview = setup.Service.PreviewAbility(
            setup.Player, setup.Enemy, setup.Power);
        Assert.That(preview.IsValid, Is.True);
        Assert.That(setup.Player.Runtime.State.currentActionPoints, Is.EqualTo(beforeAP));
        Assert.That(setup.Enemy.Runtime.State.CurrentSquadHP, Is.EqualTo(beforeHP));
        Assert.That(preview.AttackPreview.PredictedDamage,
            Is.GreaterThan(setup.Attacks.PreviewAttack(setup.Player, setup.Enemy)
                .PredictedDamage));

        Assert.That(setup.Service.TryExecuteAbility(
            setup.Player, setup.Enemy, setup.Power, out BattleAbilityResult result), Is.True);
        Assert.That(result.WasExecuted && result.Hit && result.Critical, Is.True);
        Assert.That(result.ActionPointsSpent, Is.EqualTo(3));
        Assert.That(setup.Player.Runtime.State.currentActionPoints, Is.EqualTo(beforeAP - 3));
        Assert.That(setup.Enemy.Runtime.State.CurrentSquadHP,
            Is.EqualTo(beforeHP - result.Damage));
        Assert.That(result.Damage, Is.GreaterThan(0).And
            .LessThanOrEqualTo(preview.AttackPreview.PredictedCriticalDamage));
        Assert.That(result.CooldownApplied, Is.EqualTo(1));
        Assert.That(setup.Service.GetRuntimeState("player", setup.Power.StableId)
            .remainingCooldown, Is.EqualTo(1));
        Assert.That(progressionEvents, Is.EqualTo(1));
        Assert.That(random.CallCount, Is.EqualTo(2));
    }

    [Test]
    public void CommittedMissSpendsApStartsCooldownAndCooldownAdvancesOncePerOwnerTurn()
    {
        AbilitySetup setup = CreateSetup();
        SequenceBattleRandom random = new SequenceBattleRandom(0.99f);
        setup.Attacks.SetRandomSourceForTests(random);
        int beforeAP = setup.Player.Runtime.State.currentActionPoints;
        int beforeHP = setup.Enemy.Runtime.State.CurrentSquadHP;

        Assert.That(setup.Service.TryExecuteAbility(
            setup.Player, setup.Enemy, setup.Power, out BattleAbilityResult result), Is.True);
        Assert.That(result.Hit, Is.False);
        Assert.That(result.ActionPointsSpent, Is.EqualTo(3));
        Assert.That(setup.Player.Runtime.State.currentActionPoints, Is.EqualTo(beforeAP - 3));
        Assert.That(setup.Enemy.Runtime.State.CurrentSquadHP, Is.EqualTo(beforeHP));
        Assert.That(random.CallCount, Is.EqualTo(1), "Critical roll must not run after a miss.");
        Assert.That(setup.Service.GetRuntimeState("player", setup.Power.StableId)
            .remainingCooldown, Is.EqualTo(1));

        Assert.That(setup.Turns.EndCurrentTurn(), Is.True);
        Assert.That(setup.Service.GetRuntimeState("player", setup.Power.StableId)
            .remainingCooldown, Is.EqualTo(1));
        Assert.That(setup.Turns.EndCurrentTurn(), Is.True);
        Assert.That(setup.Service.GetRuntimeState("player", setup.Power.StableId)
            .remainingCooldown, Is.Zero);
        Assert.That(setup.Power.CooldownRounds, Is.EqualTo(1));
    }

    [Test]
    public void SweepingBlowUsesOneSquadTargetAndExistingAreaDistribution()
    {
        AbilitySetup setup = CreateSetup();
        setup.Attacks.SetRandomSourceForTests(new SequenceBattleRandom(0f));
        setup.Enemy.Runtime.State.warriors[0].currentHP = 1;
        setup.Enemy.Runtime.State.warriors[1].currentHP = 1;
        int commanderBefore = setup.Enemy.Runtime.State.commander.currentHP;
        int beforeAP = setup.Player.Runtime.State.currentActionPoints;

        Assert.That(setup.Service.TryExecuteAbility(
            setup.Player, setup.Enemy, setup.Sweep, out BattleAbilityResult result), Is.True);
        Assert.That(result.Hit, Is.True);
        Assert.That(result.ActionPointsSpent, Is.EqualTo(4));
        Assert.That(setup.Player.Runtime.State.currentActionPoints, Is.EqualTo(beforeAP - 4));
        Assert.That(result.DefeatedWarriorIds.Count, Is.EqualTo(2));
        Assert.That(setup.Enemy.Runtime.State.commander.currentHP, Is.LessThan(commanderBefore),
            "Area damage should reach Commander only after all Warriors are defeated.");
        Assert.That(result.TargetSquadId, Is.EqualTo(setup.Enemy.SquadId));
        Assert.That(setup.Service.GetRuntimeState("player", setup.Sweep.StableId)
            .remainingCooldown, Is.EqualTo(2));
    }

    [Test]
    public void RallyRestoresMoraleThroughRuntimeCapsItAndNeverTouchesHpOrProgression()
    {
        AbilitySetup setup = CreateSetup();
        float loss = setup.Player.Runtime.ApplyMoraleLoss(15f);
        Assert.That(loss, Is.GreaterThan(0f));
        int hpBefore = setup.Player.Runtime.State.CurrentSquadHP;
        int apBefore = setup.Player.Runtime.State.currentActionPoints;
        int progressionEvents = 0;
        setup.Player.Runtime.OnPrimaryStatIncreased += _ => progressionEvents++;

        BattleAbilityPreview preview = setup.Service.PreviewAbility(
            setup.Player, setup.Player, setup.Rally);
        Assert.That(preview.IsValid, Is.True);
        Assert.That(preview.PredictedMoraleRestore, Is.GreaterThan(0f));
        Assert.That(setup.Service.TryExecuteAbility(
            setup.Player, setup.Player, setup.Rally, out BattleAbilityResult result), Is.True);
        Assert.That(result.MoraleRestored, Is.EqualTo(preview.PredictedMoraleRestore)
            .Within(0.001f));
        Assert.That(setup.Player.Runtime.State.currentMorale,
            Is.LessThanOrEqualTo(setup.Player.Runtime.Stats.Morale));
        Assert.That(setup.Player.Runtime.State.CurrentSquadHP, Is.EqualTo(hpBefore));
        Assert.That(setup.Player.Runtime.State.currentActionPoints, Is.EqualTo(apBefore - 2));
        Assert.That(result.Damage, Is.Zero);
        Assert.That(progressionEvents, Is.Zero);
        Assert.That(setup.Service.GetRuntimeState("player", setup.Rally.StableId)
            .remainingCooldown, Is.EqualTo(2));
    }

    [Test]
    public void InvalidAbilityCommandsDoNotSpendApOrStartCooldown()
    {
        AbilitySetup setup = CreateSetup();
        int beforeAP = setup.Player.Runtime.State.currentActionPoints;

        Assert.That(setup.Service.TryExecuteAbility(
            setup.Player, setup.Player, setup.Power, out BattleAbilityResult friendly), Is.False);
        Assert.That(friendly.FailureReason, Is.EqualTo(BattleAbilityFailureReason.InvalidTarget));
        Assert.That(setup.Service.TryExecuteAbility(
            setup.Player, setup.Enemy, setup.Rally, out BattleAbilityResult wrongSelf), Is.False);
        Assert.That(wrongSelf.FailureReason, Is.EqualTo(BattleAbilityFailureReason.InvalidTarget));
        Assert.That(setup.Player.Runtime.State.currentActionPoints, Is.EqualTo(beforeAP));
        Assert.That(setup.Service.CreateUsageRecords(), Is.Empty);

        Assert.That(setup.Player.Runtime.TrySpendActionPoints(beforeAP - 1), Is.True);
        int lowAP = setup.Player.Runtime.State.currentActionPoints;
        Assert.That(setup.Service.TryExecuteAbility(
            setup.Player, setup.Enemy, setup.Power, out BattleAbilityResult insufficient), Is.False);
        Assert.That(insufficient.FailureReason,
            Is.EqualTo(BattleAbilityFailureReason.InsufficientActionPoints));
        Assert.That(setup.Player.Runtime.State.currentActionPoints, Is.EqualTo(lowAP));
        Assert.That(setup.Service.GetRuntimeState("player", setup.Power.StableId)
            .remainingCooldown, Is.Zero);
    }

    [Test]
    public void CompletionLocksAbilitiesAndIncludesDeterministicUsageSummary()
    {
        AbilitySetup setup = CreateSetup();
        setup.Attacks.SetRandomSourceForTests(new SequenceBattleRandom(0f));
        Assert.That(setup.Service.TryExecuteAbility(
            setup.Player, setup.Enemy, setup.Power, out _), Is.True);

        setup.Enemy.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);

        Assert.That(setup.Completion.State, Is.EqualTo(BattleCompletionState.Completed));
        Assert.That(setup.Service.CommandsEnabled, Is.False);
        Assert.That(setup.Completion.Outcome.abilityUsages.Count, Is.EqualTo(1));
        Assert.That(setup.Completion.Outcome.abilityUsages[0].squadId, Is.EqualTo("player"));
        Assert.That(setup.Completion.Outcome.abilityUsages[0].abilityId,
            Is.EqualTo(setup.Power.StableId));
        Assert.That(setup.Completion.Outcome.abilityUsages[0].uses, Is.EqualTo(1));
        int ap = setup.Player.Runtime.State.currentActionPoints;
        Assert.That(setup.Service.TryExecuteAbility(
            setup.Player, setup.Player, setup.Rally, out BattleAbilityResult blocked), Is.False);
        Assert.That(blocked.FailureReason, Is.EqualTo(BattleAbilityFailureReason.BattleCompleted));
        Assert.That(setup.Player.Runtime.State.currentActionPoints, Is.EqualTo(ap));
    }

    [Test]
    public void ProductionSceneHasOneAbilityOwnerThreeControlsAndPersistentDefinitions()
    {
        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/Raw_Alpha_BattleMode.unity",
            OpenSceneMode.Single);
        BattleAbilityService[] services = FindInScene<BattleAbilityService>(scene);
        AbilityCommandController[] commands = FindInScene<AbilityCommandController>(scene);
        Assert.That(services.Length, Is.EqualTo(1));
        Assert.That(commands.Length, Is.EqualTo(1));
        Assert.That(services[0].Abilities.Count, Is.EqualTo(3));
        Assert.That(services[0].Abilities.All(ability => ability != null &&
            UnityEditor.EditorUtility.IsPersistent(ability)), Is.True);
        Assert.That(commands[0].AbilityControls.Count, Is.EqualTo(3));
        Assert.That(commands[0].AbilityControls.Select(control => control.gameObject.name),
            Is.EquivalentTo(new[] { "PowerStrike", "SweepingBlow", "Rally" }));
        BattleHUDController hud = FindInScene<BattleHUDController>(scene).Single();
        Assert.That(hud.GetComponentsInChildren<BattleActionControlView>(true).Length,
            Is.EqualTo(8));
    }

    private AbilitySetup CreateSetup()
    {
        GameObject root = Track(new GameObject("AbilityTestRoot"));
        GameObject mapObject = NewChild(root.transform, "Map").gameObject;
        MapGenerator generator = mapObject.AddComponent<MapGenerator>();
        generator.autoGenerate = false;
        generator.width = 7;
        generator.height = 7;
        generator.playableCount = 40;
        generator.seed = 911;
        generator.Generate();
        MapRenderer renderer = mapObject.AddComponent<MapRenderer>();
        renderer.autoRender = false;
        renderer.mapGenerator = generator;
        FindAdjacentPlayableCells(generator, out Vector2Int playerCell, out Vector2Int enemyCell);

        GameObject template = NewChild(root.transform, "Template").gameObject;
        SquadGridAnchor templateAnchor = template.AddComponent<SquadGridAnchor>();
        SquadBattleController templateController = template.AddComponent<SquadBattleController>();
        templateController.Configure(templateAnchor, null);
        GameObject bootstrapObject = NewChild(root.transform, "Bootstrap").gameObject;
        Transform container = NewChild(bootstrapObject.transform, "Spawned");
        SquadSaveParticipant repository = bootstrapObject.AddComponent<SquadSaveParticipant>();
        SquadBattleBootstrap bootstrap = bootstrapObject.AddComponent<SquadBattleBootstrap>();
        bootstrap.Configure(templateController, container, repository, false, null, null, false);
        BattleSquadSelectionContext.SetSelection(
            new[] { CreateSquad("player", 20f, 1f) },
            new[] { CreateSquad("enemy", 10f, 0f) });
        Assert.That(bootstrap.InitializeSquads(
            generator, renderer, playerCell, enemyCell), Is.True);
        SquadBattleController player = bootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Player);
        SquadBattleController enemy = bootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Enemy);

        GridOccupancyService occupancy = NewChild(root.transform, "Occupancy").gameObject
            .AddComponent<GridOccupancyService>();
        Assert.That(occupancy.Initialize(bootstrap.SpawnedControllers), Is.True);
        BattleSquadSelectionController selection = NewChild(root.transform, "Selection").gameObject
            .AddComponent<BattleSquadSelectionController>();
        selection.Configure(bootstrap, null);
        Assert.That(selection.Initialize(), Is.True);
        Assert.That(selection.TrySelect(player), Is.True);
        BattleTurnController turns = NewChild(root.transform, "Turns").gameObject
            .AddComponent<BattleTurnController>();
        turns.Configure(bootstrap, false, 0f);
        Assert.That(turns.StartBattle(), Is.True);
        SquadMovementService movement = NewChild(root.transform, "Movement").gameObject
            .AddComponent<SquadMovementService>();
        movement.Configure(generator, renderer, occupancy, turns, true, 0.01f);
        Assert.That(movement.Initialize(), Is.True);
        BattleCommandModeController modes = NewChild(root.transform, "Modes").gameObject
            .AddComponent<BattleCommandModeController>();

        AttackDefinition basic = Track(ScriptableObject.CreateInstance<AttackDefinition>());
        basic.ConfigureDevelopment("test-basic", "Basic", 2, 2, 0.5f, null, null);
        BattleCombatRules rules = Track(ScriptableObject.CreateInstance<BattleCombatRules>());
        rules.ConfigureDevelopment(0.75f, 0.05f, 0.95f, 0.8f, 1);
        BattleAttackService attacks = NewChild(root.transform, "Attacks").gameObject
            .AddComponent<BattleAttackService>();
        attacks.Configure(bootstrap, turns, selection, movement, basic, rules, true, 42,
            new SequenceBattleRandom(0f));
        Assert.That(attacks.Initialize(), Is.True);

        MovementCommandController moveCommands = NewChild(root.transform, "MoveCommands")
            .gameObject.AddComponent<MovementCommandController>();
        AttackCommandController attackCommands = NewChild(root.transform, "AttackCommands")
            .gameObject.AddComponent<AttackCommandController>();
        BattleHUDController hud = NewChild(root.transform, "HUD").gameObject
            .AddComponent<BattleHUDController>();
        BattleResultPanelView panel = NewChild(root.transform, "ResultPanel").gameObject
            .AddComponent<BattleResultPanelView>();
        PersistentDebuffDefinition debuff = Track(
            ScriptableObject.CreateInstance<PersistentDebuffDefinition>());
        debuff.ConfigureDevelopment("DEV_BattleScar", "Battle Scar", "Resolve -1", -1f);
        PostBattleRules postRules = Track(ScriptableObject.CreateInstance<PostBattleRules>());
        postRules.ConfigureDevelopment(0.2f, debuff);
        BattleCompletionController completion = NewChild(root.transform, "Completion").gameObject
            .AddComponent<BattleCompletionController>();
        completion.Configure(
            bootstrap, turns, modes, movement, moveCommands, attacks, attackCommands,
            hud, repository, null, postRules, panel, "first_try");
        completion.ConfigureTestSeams(
            outcome =>
            {
                outcome.persistentMutationsApplied = true;
                return BattleResultApplicationResult.Ok();
            },
            (_, _) => SaveOperationResult.Ok(),
            _ => { });
        Assert.That(completion.Initialize(
            "battle-ability-test-" + Guid.NewGuid().ToString("N"),
            "2026-08-09T00:00:00Z"), Is.True);

        AttackDefinition powerAttack = Track(ScriptableObject.CreateInstance<AttackDefinition>());
        powerAttack.ConfigureDevelopmentAbilityEffect(
            "power-effect", "Power", 4, 3, 0.85f,
            SquadDamageDistribution.SingleTarget, null, null);
        AttackDefinition sweepAttack = Track(ScriptableObject.CreateInstance<AttackDefinition>());
        sweepAttack.ConfigureDevelopmentAbilityEffect(
            "sweep-effect", "Sweep", 10, 4, 0.75f,
            SquadDamageDistribution.Area, null, null);
        AbilityDefinition power = Track(ScriptableObject.CreateInstance<AbilityDefinition>());
        power.ConfigureDevelopmentAttack(
            "DEV_PowerStrike", "Power Strike", "Heavy", 3, 1, Key.Digit1,
            powerAttack, null);
        AbilityDefinition sweep = Track(ScriptableObject.CreateInstance<AbilityDefinition>());
        sweep.ConfigureDevelopmentAttack(
            "DEV_SweepingBlow", "Sweeping Blow", "Area", 4, 2, Key.Digit2,
            sweepAttack, null);
        AbilityDefinition rally = Track(ScriptableObject.CreateInstance<AbilityDefinition>());
        rally.ConfigureDevelopmentRally(
            "DEV_Rally", "Rally", "Morale", 2, 2, 20f, Key.Digit3, null);
        BattleAbilityService service = NewChild(root.transform, "Abilities").gameObject
            .AddComponent<BattleAbilityService>();
        service.Configure(
            bootstrap, turns, selection, movement, attacks, completion,
            new[] { power, sweep, rally });
        Assert.That(service.Initialize(), Is.True);
        completion.ConfigureAbilities(service, null);

        return new AbilitySetup
        {
            Bootstrap = bootstrap,
            Player = player,
            Enemy = enemy,
            Turns = turns,
            Attacks = attacks,
            Completion = completion,
            Service = service,
            Power = power,
            Sweep = sweep,
            Rally = rally
        };
    }

    private static SquadData CreateSquad(string id, float initiative, float experience)
    {
        return new SquadData(
            id,
            new CommanderData
            {
                id = id + "-commander",
                commanderPortraitId = id + "-portrait",
                baseStats = new SquadBaseStats
                {
                    hp = 50,
                    actionPoints = 12,
                    initiative = initiative,
                    strength = 10,
                    dexterity = 5,
                    accuracy = 0.2f,
                    evasion = 0f,
                    criticalChance = 0f,
                    criticalDamage = 1.5f,
                    physicalArmor = 0f,
                    morale = 50,
                    resolve = 0,
                    experienceMultiplier = experience
                }
            },
            new[]
            {
                new WarriorData { id = id + "-warrior-0", maxHP = 5, strength = 1, dexterity = 1 },
                new WarriorData { id = id + "-warrior-1", maxHP = 5, strength = 1, dexterity = 1 }
            });
    }

    private static void FindAdjacentPlayableCells(
        MapGenerator generator,
        out Vector2Int first,
        out Vector2Int second)
    {
        for (int x = 0; x < generator.width; x++)
        for (int y = 0; y < generator.height; y++)
        {
            if (!generator.GetIsPlayable(x, y))
                continue;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < generator.width && ny < generator.height &&
                    generator.GetIsPlayable(nx, ny))
                {
                    first = new Vector2Int(x, y);
                    second = new Vector2Int(nx, ny);
                    return;
                }
            }
        }
        throw new AssertionException("No adjacent playable cells were generated.");
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        List<T> values = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            values.AddRange(root.GetComponentsInChildren<T>(true));
        return values.ToArray();
    }

    private static Transform NewChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        cleanup.Add(value);
        return value;
    }

    private sealed class SequenceBattleRandom : IBattleRandomSource
    {
        private readonly float[] values;
        private int index;
        public int CallCount => index;
        public SequenceBattleRandom(params float[] configuredValues)
        {
            values = configuredValues ?? Array.Empty<float>();
        }
        public float Next01()
        {
            float value = values.Length == 0
                ? 0f
                : values[Math.Min(index, values.Length - 1)];
            index++;
            return Mathf.Clamp01(value);
        }
    }

    private sealed class AbilitySetup
    {
        public SquadBattleBootstrap Bootstrap;
        public SquadBattleController Player;
        public SquadBattleController Enemy;
        public BattleTurnController Turns;
        public BattleAttackService Attacks;
        public BattleCompletionController Completion;
        public BattleAbilityService Service;
        public AbilityDefinition Power;
        public AbilityDefinition Sweep;
        public AbilityDefinition Rally;
    }
}
