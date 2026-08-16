using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BattleAttackPipelineTests
{
    private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        BattleSquadSelectionContext.Clear();
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                UnityEngine.Object.DestroyImmediate(cleanup[i]);
        }
        cleanup.Clear();
    }

    [Test]
    public void HitCriticalStrengthAndArmorFormulasUseOneNormalizedRulesContract()
    {
        BattleCombatRules rules = CreateRules();
        AttackDefinition attack = CreateAttack();
        BattleAttackCalculator calculator = new BattleAttackCalculator(rules);
        SquadCalculatedStats attacker = Stats(
            strength: 10f,
            accuracy: 0.4f,
            criticalChance: 0.25f,
            criticalDamage: 1.5f);
        SquadCalculatedStats evasiveTarget = Stats(evasion: 2f, physicalArmor: 0.25f);
        SquadCalculatedStats exposedTarget = Stats(evasion: 0f, physicalArmor: 0f);

        Assert.That(calculator.CalculateHitChance(attacker, exposedTarget),
            Is.EqualTo(0.95f).Within(0.0001f));
        Assert.That(calculator.CalculateHitChance(Stats(), evasiveTarget),
            Is.EqualTo(0.05f).Within(0.0001f));
        Assert.That(calculator.CalculateCriticalChance(attacker, attack),
            Is.EqualTo(0.25f).Within(0.0001f));

        BattleDamageCalculation normal = calculator.CalculateDamage(
            attacker, evasiveTarget, attack, false);
        BattleDamageCalculation critical = calculator.CalculateDamage(
            attacker, evasiveTarget, attack, true);
        Assert.That(normal.RawDamage, Is.EqualTo(7f).Within(0.0001f));
        Assert.That(normal.ArmorReduction, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(normal.MitigatedDamage, Is.EqualTo(5.25f).Within(0.0001f));
        Assert.That(normal.AppliedDamage, Is.EqualTo(5),
            "Nearest integer with exact .5 upward is the only rounding rule.");
        Assert.That(critical.RawDamage, Is.EqualTo(10.5f).Within(0.0001f));
        Assert.That(critical.AppliedDamage, Is.EqualTo(8));
    }

    [Test]
    public void PhysicalArmorCapAndMagicalResistanceSeparationAreExplicit()
    {
        BattleCombatRules rules = CreateRules();
        AttackDefinition attack = CreateAttack(baseDamage: 10, scaling: 0f);
        BattleAttackCalculator calculator = new BattleAttackCalculator(rules);
        SquadCalculatedStats target = Stats(
            physicalArmor: 5f,
            magicalResistance: 0.99f);

        BattleDamageCalculation physical = calculator.CalculateDamage(
            Stats(), target, attack, false);
        Assert.That(physical.ArmorReduction, Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(physical.AppliedDamage, Is.EqualTo(2));

        SerializedObject serialized = new SerializedObject(attack);
        serialized.FindProperty("damageType").enumValueIndex =
            (int)BattleDamageType.Magical;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        BattleDamageCalculation extensionPoint = calculator.CalculateDamage(
            Stats(), target, attack, false);
        Assert.That(extensionPoint.ArmorReduction, Is.Zero);
        Assert.That(extensionPoint.AppliedDamage, Is.EqualTo(10),
            "Magical resistance must not leak into the physical attack formula.");
    }

    [Test]
    public void TargetingUsesEightNeighborGridDistanceAndRejectsSelfFriendlyDefeatedAndFar()
    {
        AttackSetup setup = CreateSetup(adjacent: true);
        BattleTargetingService targeting = setup.AttackService.TargetingService;

        Assert.That(targeting.ValidateTarget(
            setup.Player, setup.Enemy, setup.Definition).IsValid, Is.True);
        Assert.That(targeting.ValidateTarget(
            setup.Player, setup.Player, setup.Definition).FailureReason,
            Is.EqualTo(BattleAttackFailureReason.SelfTarget));

        SquadBattleController friendly = CreatePlacedController(
            setup,
            "friendly",
            setup.Enemy.GridAnchor.CurrentCell,
            BattleSide.Player,
            8);
        Assert.That(targeting.ValidateTarget(
            setup.Player, friendly, setup.Definition).FailureReason,
            Is.EqualTo(BattleAttackFailureReason.FriendlyTarget));

        setup.Enemy.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);
        Assert.That(targeting.ValidateTarget(
            setup.Player, setup.Enemy, setup.Definition).FailureReason,
            Is.EqualTo(BattleAttackFailureReason.TargetDefeated));

        Assert.That(BattleTargetingService.GetGridDistance(
            new Vector2Int(2, 2), new Vector2Int(3, 3), true), Is.EqualTo(1));
        Assert.That(BattleTargetingService.GetGridDistance(
            new Vector2Int(2, 2), new Vector2Int(3, 3), false), Is.EqualTo(2));

        AttackSetup far = CreateSetup(adjacent: false);
        Assert.That(far.AttackService.TargetingService.ValidateTarget(
            far.Player, far.Enemy, far.Definition).FailureReason,
            Is.EqualTo(BattleAttackFailureReason.TargetOutOfRange));
    }

    [Test]
    public void ValidationRequiresActiveSelectedLivingHumanPlayerWithApAndNoMovement()
    {
        AttackSetup setup = CreateSetup(adjacent: true);
        Assert.That(setup.AttackService.ValidateCommand(
            setup.Player, setup.Enemy).IsValid, Is.True);

        setup.Selection.ClearSelection();
        Assert.That(setup.AttackService.ValidateCommand(
            setup.Player, setup.Enemy).FailureReason,
            Is.EqualTo(BattleAttackFailureReason.AttackerNotSelected));
        Assert.That(setup.Selection.TrySelect(setup.Player), Is.True);

        int allAp = setup.Player.Runtime.State.currentActionPoints;
        Assert.That(setup.Player.Runtime.TrySpendActionPoints(allAp), Is.True);
        int targetHp = setup.Enemy.Runtime.State.CurrentSquadHP;
        Assert.That(setup.AttackService.TryExecuteAttack(
            setup.Player, setup.Enemy, out BattleAttackResult insufficient), Is.False);
        Assert.That(insufficient.FailureReason,
            Is.EqualTo(BattleAttackFailureReason.InsufficientActionPoints));
        Assert.That(setup.Player.Runtime.State.currentActionPoints, Is.Zero);
        Assert.That(setup.Enemy.Runtime.State.CurrentSquadHP, Is.EqualTo(targetHp));

        AttackSetup moving = CreateSetup(adjacent: true, movementInProgress: () => true);
        Assert.That(moving.AttackService.ValidateCommand(
            moving.Player, moving.Enemy).FailureReason,
            Is.EqualTo(BattleAttackFailureReason.MovementInProgress));

        Assert.That(setup.Turns.EndCurrentTurn(), Is.True);
        Assert.That(setup.AttackService.ValidateAvailability(
            setup.Player, requireSelected: false, requireTargetInRange: false).FailureReason,
            Is.EqualTo(BattleAttackFailureReason.AttackerNotActive));
    }

    [Test]
    public void DeterministicMissSpendsApOnceDoesNotRollCriticalOrMutateTarget()
    {
        SequenceRandomSource random = new SequenceRandomSource(0.99f, 0f);
        AttackSetup setup = CreateSetup(adjacent: true, random: random);
        int apBefore = setup.Player.Runtime.State.currentActionPoints;
        int hpBefore = setup.Enemy.Runtime.State.CurrentSquadHP;

        Assert.That(setup.AttackService.TryExecuteAttack(
            setup.Player, setup.Enemy, out BattleAttackResult result), Is.True);
        Assert.That(result.Hit, Is.False);
        Assert.That(result.Critical, Is.False);
        Assert.That(result.ActionPointsSpent, Is.EqualTo(setup.Definition.ActionPointCost));
        Assert.That(setup.Player.Runtime.State.currentActionPoints,
            Is.EqualTo(apBefore - setup.Definition.ActionPointCost));
        Assert.That(setup.Enemy.Runtime.State.CurrentSquadHP, Is.EqualTo(hpBefore));
        Assert.That(random.CallCount, Is.EqualTo(1),
            "A miss must not consume a critical roll.");
    }

    [Test]
    public void DeterministicHitAndCriticalUseExpectedRollCountAndMultiplier()
    {
        SequenceRandomSource random = new SequenceRandomSource(0f, 0f);
        AttackSetup setup = CreateSetup(
            adjacent: true,
            random: random,
            playerCriticalChance: 1f,
            playerCriticalDamage: 1.5f,
            enemyWarriorHp: 30,
            enemyArmor: 0f);

        Assert.That(setup.AttackService.TryExecuteAttack(
            setup.Player, setup.Enemy, out BattleAttackResult result), Is.True);
        Assert.That(result.Hit, Is.True);
        Assert.That(result.Critical, Is.True);
        Assert.That(result.RawDamage, Is.EqualTo(12f).Within(0.0001f),
            "(base 2 + calculated Strength 12 x 0.5) x critical 1.5");
        Assert.That(result.AppliedDamage, Is.EqualTo(12));
        Assert.That(random.CallCount, Is.EqualTo(2));

        SequenceRandomSource noCritRandom = new SequenceRandomSource(0f, 0f);
        AttackSetup noCrit = CreateSetup(
            adjacent: true,
            random: noCritRandom,
            playerCriticalChance: 0f,
            enemyWarriorHp: 30,
            enemyArmor: 0f);
        Assert.That(noCrit.AttackService.TryExecuteAttack(
            noCrit.Player, noCrit.Enemy, out BattleAttackResult noCritResult), Is.True);
        Assert.That(noCritResult.Hit, Is.True);
        Assert.That(noCritResult.Critical, Is.False);
        Assert.That(noCritRandom.CallCount, Is.EqualTo(1),
            "A guaranteed-zero critical chance does not consume a meaningless roll.");
    }

    [Test]
    public void PreviewIsReadOnlyAndExecutionUsesTheSameDamageFormula()
    {
        SequenceRandomSource random = new SequenceRandomSource(0f, 0.99f);
        AttackSetup setup = CreateSetup(
            adjacent: true,
            random: random,
            enemyWarriorHp: 30,
            enemyArmor: 0.15f);
        int apBefore = setup.Player.Runtime.State.currentActionPoints;
        int hpBefore = setup.Enemy.Runtime.State.CurrentSquadHP;

        BattleAttackPreview first = setup.AttackService.PreviewAttack(
            setup.Player, setup.Enemy);
        BattleAttackPreview second = setup.AttackService.PreviewAttack(
            setup.Player, setup.Enemy);
        Assert.That(first.IsValid, Is.True);
        Assert.That(second.PredictedDamage, Is.EqualTo(first.PredictedDamage));
        Assert.That(setup.Player.Runtime.State.currentActionPoints, Is.EqualTo(apBefore));
        Assert.That(setup.Enemy.Runtime.State.CurrentSquadHP, Is.EqualTo(hpBefore));
        Assert.That(random.CallCount, Is.Zero);

        Assert.That(setup.AttackService.TryExecuteAttack(
            setup.Player, setup.Enemy, out BattleAttackResult result), Is.True);
        Assert.That(result.Critical, Is.False);
        Assert.That(result.AppliedDamage, Is.EqualTo(first.PredictedDamage));
    }

    [Test]
    public void SingleTargetCasualtiesDoNotOverflowAndCommanderDefeatUpdatesBattleServices()
    {
        SequenceRandomSource random = new SequenceRandomSource(
            0f, 0.99f,
            0f, 0.99f,
            0f, 0.99f);
        AttackSetup setup = CreateSetup(
            adjacent: true,
            random: random,
            enemyWarriorCount: 2,
            enemyWarriorHp: 8,
            enemyArmor: 0f);
        WarriorBattleState first = setup.Enemy.Runtime.State.warriors[0];
        WarriorBattleState second = setup.Enemy.Runtime.State.warriors[1];
        float strengthBefore = setup.Enemy.Runtime.Stats.Strength;

        Assert.That(setup.AttackService.TryExecuteAttack(
            setup.Player, setup.Enemy, out BattleAttackResult firstHit), Is.True);
        Assert.That(first.defeated, Is.True);
        Assert.That(second.currentHP, Is.EqualTo(8),
            "SingleTarget overkill must not transfer to the next warrior.");
        Assert.That(firstHit.DefeatedWarriorIds, Does.Contain(first.warriorId));
        Assert.That(setup.Enemy.Runtime.Stats.Strength, Is.LessThan(strengthBefore));

        setup.Player.Runtime.BeginTurn();
        Assert.That(setup.AttackService.TryExecuteAttack(
            setup.Player, setup.Enemy, out BattleAttackResult secondHit), Is.True);
        Assert.That(second.defeated, Is.True);
        Assert.That(secondHit.CommanderDamaged, Is.False);

        setup.Definition.ConfigureDevelopment(
            "dev-basic-physical-melee",
            "Basic Physical Attack",
            30,
            2,
            0f,
            null,
            null);
        setup.Player.Runtime.BeginTurn();
        Assert.That(setup.AttackService.TryExecuteAttack(
            setup.Player, setup.Enemy, out BattleAttackResult commanderHit), Is.True);
        Assert.That(commanderHit.CommanderDamaged, Is.True);
        Assert.That(commanderHit.CommanderDefeated, Is.True);
        Assert.That(commanderHit.SquadDefeated, Is.True);
        Assert.That(setup.Enemy.CanAct, Is.False);
        Assert.That(setup.Bootstrap.InitiativeOrder.Entries.Contains(setup.Enemy),
            Is.False);
        Assert.That(setup.Occupancy.OccupiedCellCount, Is.EqualTo(1),
            "The current tactical contract releases defeated occupancy.");
    }

    [Test]
    public void SharedCommandModeNeverAllowsMoveAndAttackSimultaneously()
    {
        BattleCommandModeController modes = Track(new GameObject("CommandModes"))
            .AddComponent<BattleCommandModeController>();
        List<BattleCommandMode> changes = new List<BattleCommandMode>();
        modes.OnModeChanged += changes.Add;

        Assert.That(modes.TryEnter(BattleCommandMode.Move), Is.True);
        Assert.That(modes.ActiveMode, Is.EqualTo(BattleCommandMode.Move));
        Assert.That(modes.TryEnter(BattleCommandMode.Attack), Is.True);
        Assert.That(modes.ActiveMode, Is.EqualTo(BattleCommandMode.Attack));
        Assert.That(modes.Cancel(), Is.True);
        Assert.That(modes.ActiveMode, Is.EqualTo(BattleCommandMode.None));
        Assert.That(changes, Is.EqualTo(new[]
        {
            BattleCommandMode.Move,
            BattleCommandMode.Attack,
            BattleCommandMode.None
        }));
    }

    [Test]
    public void DevelopmentAttackAssetsUseStablePhysicalSingleTargetContractAndSwordPreview()
    {
        AttackDefinition attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(
            "Assets/GameData/Combat/DEV_BasicPhysicalMeleeAttack.asset");
        BattleCombatRules rules = AssetDatabase.LoadAssetAtPath<BattleCombatRules>(
            "Assets/GameData/Combat/DEV_BattleCombatRules.asset");

        Assert.That(attack, Is.Not.Null);
        Assert.That(attack.Validate(out string reason), Is.True, reason);
        Assert.That(attack.StableId, Is.EqualTo("dev-basic-physical-melee"));
        Assert.That(attack.DamageType, Is.EqualTo(BattleDamageType.Physical));
        Assert.That(attack.Distribution, Is.EqualTo(SquadDamageDistribution.SingleTarget));
        Assert.That(attack.MinimumRange, Is.EqualTo(1));
        Assert.That(attack.MaximumRange, Is.EqualTo(1));
        Assert.That(attack.ActionPointCost, Is.EqualTo(2));
        Assert.That(attack.PreviewSprite, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(attack.PreviewSprite),
            Is.EqualTo("Assets/3DModel/Test/WP_Sword_01_Preview.png"));
        Assert.That(attack.ModelPrefab, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(attack.ModelPrefab),
            Is.EqualTo("Assets/3DModel/Test/WP_Sword_01.fbx"));
        Assert.That(rules, Is.Not.Null);
        Assert.That(rules.BaseHitChance, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(rules.MaximumPhysicalArmorReduction,
            Is.EqualTo(0.8f).Within(0.0001f));
    }

    [Test]
    public void RangedPipelineUsesPreviewLosCoverApAndExistingDamageResolver()
    {
        SequenceRandomSource random = new SequenceRandomSource(0f, 0.99f);
        AttackSetup setup = CreateSetup(
            adjacent: false,
            random: random,
            enemyWarriorHp: 30,
            enemyArmor: 0.10f,
            includeRanged: true);
        AttackDefinition ranged = setup.RangedDefinition;
        int apBefore = setup.Player.Runtime.State.currentActionPoints;
        int hpBefore = setup.Enemy.Runtime.State.CurrentSquadHP;
        IReadOnlyList<Vector2Int> line = GridLineOfSightService.BuildSupercoverLine(
            setup.Player.GridAnchor.CurrentCell,
            setup.Enemy.GridAnchor.CurrentCell);
        Vector2Int blockingCell = line.Skip(1).First(cell =>
            cell != setup.Enemy.GridAnchor.CurrentCell);

        Assert.That(setup.Terrain.SetRuntimeCellsForTests(new[]
        {
            new GridTacticalTerrainCellDefinition(
                blockingCell, true, true, CoverType.Full)
        }), Is.True);
        BattleAttackPreview blocked = setup.AttackService.PreviewAttack(
            setup.Player, setup.Enemy, ranged);
        Assert.That(blocked.Validation.FailureReason,
            Is.EqualTo(BattleAttackFailureReason.LineOfSightBlocked));
        Assert.That(setup.AttackService.TryExecuteAttack(
            setup.Player, setup.Enemy, out _, ranged), Is.False);
        Assert.That(setup.Player.Runtime.State.currentActionPoints, Is.EqualTo(apBefore));
        Assert.That(setup.Enemy.Runtime.State.CurrentSquadHP, Is.EqualTo(hpBefore));

        GridCoverResult coverCells = new GridCoverService(setup.Terrain).Evaluate(
            setup.Player.GridAnchor.CurrentCell,
            setup.Enemy.GridAnchor.CurrentCell);
        Assert.That(coverCells.EvaluatedCells, Is.Not.Empty);
        Assert.That(setup.Terrain.SetRuntimeCellsForTests(new[]
        {
            new GridTacticalTerrainCellDefinition(
                coverCells.EvaluatedCells[0], true, false, CoverType.Half)
        }), Is.True);
        BattleAttackPreview preview = setup.AttackService.PreviewAttack(
            setup.Player, setup.Enemy, ranged);
        Assert.That(preview.IsValid, Is.True);
        Assert.That(preview.LineOfSightStatus, Is.EqualTo(LineOfSightStatus.Clear));
        Assert.That(preview.CoverType, Is.EqualTo(CoverType.Half));
        Assert.That(preview.GridDistance,
            Is.InRange(ranged.MinimumRange, ranged.MaximumRange));
        Assert.That(preview.CoverHitModifier, Is.EqualTo(-0.20f).Within(0.0001f));
        Assert.That(random.CallCount, Is.Zero, "Preview cannot consume combat RNG.");

        Assert.That(setup.AttackService.TryExecuteAttack(
            setup.Player, setup.Enemy, out BattleAttackResult result, ranged), Is.True);
        Assert.That(result.WasExecuted, Is.True);
        Assert.That(result.Hit, Is.True);
        Assert.That(result.CoverType, Is.EqualTo(CoverType.Half));
        Assert.That(result.LineOfSightStatus, Is.EqualTo(LineOfSightStatus.Clear));
        Assert.That(result.ActionPointsSpent, Is.EqualTo(3));
        Assert.That(setup.Player.Runtime.State.currentActionPoints,
            Is.EqualTo(apBefore - ranged.ActionPointCost));
        Assert.That(result.AppliedDamage, Is.EqualTo(preview.PredictedDamage));
        Assert.That(setup.Enemy.Runtime.State.CurrentSquadHP,
            Is.EqualTo(hpBefore - result.AppliedDamage));
    }

    private AttackSetup CreateSetup(
        bool adjacent,
        SequenceRandomSource random = null,
        Func<bool> movementInProgress = null,
        float playerCriticalChance = 0.15f,
        float playerCriticalDamage = 1.5f,
        int enemyWarriorCount = 2,
        int enemyWarriorHp = 8,
        float enemyArmor = 0.15f,
        bool includeRanged = false)
    {
        GameObject root = Track(new GameObject("AttackTestRoot"));
        GameObject mapObject = NewChild(root.transform, "Map").gameObject;
        MapGenerator generator = mapObject.AddComponent<MapGenerator>();
        generator.autoGenerate = false;
        generator.width = 7;
        generator.height = 7;
        generator.playableCount = 40;
        generator.seed = 913;
        generator.Generate();
        MapRenderer renderer = mapObject.AddComponent<MapRenderer>();
        renderer.autoRender = false;
        renderer.mapGenerator = generator;
        FindCellPair(generator, adjacent, out Vector2Int playerCell, out Vector2Int enemyCell);

        GameObject template = NewChild(root.transform, "SquadTemplate").gameObject;
        SquadGridAnchor templateAnchor = template.AddComponent<SquadGridAnchor>();
        SquadBattleController templateController = template.AddComponent<SquadBattleController>();
        templateController.Configure(templateAnchor, null);

        GameObject bootstrapObject = NewChild(root.transform, "Bootstrap").gameObject;
        Transform container = NewChild(bootstrapObject.transform, "Spawned");
        SquadSaveParticipant repository = bootstrapObject.AddComponent<SquadSaveParticipant>();
        SquadBattleBootstrap bootstrap = bootstrapObject.AddComponent<SquadBattleBootstrap>();
        bootstrap.Configure(templateController, container, repository, false, null, null, false);
        BattleSquadSelectionContext.SetSelection(
            new[]
            {
                CreateSquad(
                    "player",
                    20f,
                    4,
                    2,
                    8,
                    0.10f,
                    0.05f,
                    playerCriticalChance,
                    playerCriticalDamage,
                    0.10f)
            },
            new[]
            {
                CreateSquad(
                    "enemy",
                    10f,
                    4,
                    enemyWarriorCount,
                    enemyWarriorHp,
                    0.05f,
                    0.05f,
                    0.05f,
                    1.5f,
                    enemyArmor)
            });
        Assert.That(bootstrap.InitializeSquads(
            generator, renderer, playerCell, enemyCell), Is.True);
        SquadBattleController player = bootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Player);
        SquadBattleController enemy = bootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Enemy);

        GridOccupancyService occupancy =
            NewChild(root.transform, "Occupancy").gameObject.AddComponent<GridOccupancyService>();
        Assert.That(occupancy.Initialize(bootstrap.SpawnedControllers), Is.True);
        BattleSquadSelectionController selection =
            NewChild(root.transform, "Selection").gameObject
                .AddComponent<BattleSquadSelectionController>();
        selection.Configure(bootstrap, null);
        Assert.That(selection.Initialize(), Is.True);
        Assert.That(selection.TrySelect(player), Is.True);
        BattleTurnController turns =
            NewChild(root.transform, "Turns").gameObject.AddComponent<BattleTurnController>();
        turns.Configure(bootstrap, false, 0f);
        Assert.That(turns.StartBattle(), Is.True);
        Assert.That(turns.ActiveSquad, Is.SameAs(player));
        GridTacticalTerrainService terrain = includeRanged
            ? NewChild(root.transform, "TacticalTerrain").gameObject
                .AddComponent<GridTacticalTerrainService>()
            : null;
        terrain?.Configure(generator, Array.Empty<GridTacticalTerrainCellDefinition>());
        SquadMovementService movement =
            NewChild(root.transform, "Movement").gameObject.AddComponent<SquadMovementService>();
        movement.Configure(generator, renderer, occupancy, turns, terrain, true, 0.02f);
        Assert.That(movement.Initialize(), Is.True);

        AttackDefinition definition = CreateAttack();
        AttackDefinition rangedDefinition = includeRanged ? CreateRangedAttack() : null;
        BattleCombatRules rules = CreateRules();
        BattleAttackService attackService =
            NewChild(root.transform, "AttackService").gameObject.AddComponent<BattleAttackService>();
        attackService.Configure(
            bootstrap,
            turns,
            selection,
            movement,
            definition,
            rangedDefinition,
            rules,
            terrain,
            true,
            42,
            random ?? new SequenceRandomSource(0f, 0.99f),
            movementInProgress);
        Assert.That(attackService.Initialize(), Is.True);

        return new AttackSetup
        {
            Root = root,
            Generator = generator,
            Renderer = renderer,
            Bootstrap = bootstrap,
            Player = player,
            Enemy = enemy,
            Occupancy = occupancy,
            Selection = selection,
            Turns = turns,
            Movement = movement,
            Definition = definition,
            RangedDefinition = rangedDefinition,
            Rules = rules,
            Terrain = terrain,
            AttackService = attackService
        };
    }

    private SquadBattleController CreatePlacedController(
        AttackSetup setup,
        string id,
        Vector2Int cell,
        BattleSide side,
        int sequence)
    {
        GameObject root = Track(new GameObject(id));
        SquadGridAnchor anchor = root.AddComponent<SquadGridAnchor>();
        SquadBattleController controller = root.AddComponent<SquadBattleController>();
        controller.Configure(anchor, null);
        Assert.That(controller.InitializeAtCell(
            CreateSquad(id, 5f, 4, 1, 8, 0f, 0f, 0f, 1.5f, 0f),
            null,
            setup.Generator,
            setup.Renderer,
            cell,
            side,
            SquadControlType.AI,
            sequence), Is.True);
        return controller;
    }

    private AttackDefinition CreateAttack(
        int baseDamage = 2,
        float scaling = 0.5f)
    {
        AttackDefinition attack = Track(ScriptableObject.CreateInstance<AttackDefinition>());
        attack.ConfigureDevelopment(
            "test-basic-physical",
            "Test Basic Physical",
            baseDamage,
            2,
            scaling,
            null,
            null);
        return attack;
    }

    private AttackDefinition CreateRangedAttack()
    {
        AttackDefinition attack = Track(ScriptableObject.CreateInstance<AttackDefinition>());
        attack.ConfigureDevelopmentRanged(
            "test-basic-ranged",
            "Test Basic Ranged",
            2,
            3,
            2,
            8,
            0.5f,
            null,
            null);
        return attack;
    }

    private BattleCombatRules CreateRules()
    {
        BattleCombatRules rules = Track(ScriptableObject.CreateInstance<BattleCombatRules>());
        rules.ConfigureDevelopment(0.75f, 0.05f, 0.95f, 0.8f, 1);
        return rules;
    }

    private static SquadCalculatedStats Stats(
        float strength = 0f,
        float accuracy = 0f,
        float evasion = 0f,
        float criticalChance = 0f,
        float criticalDamage = 1f,
        float physicalArmor = 0f,
        float magicalResistance = 0f)
    {
        return new SquadCalculatedStats(
            new SquadBaseStats
            {
                hp = 1,
                actionPoints = 4,
                strength = strength,
                accuracy = accuracy,
                evasion = evasion,
                criticalChance = criticalChance,
                criticalDamage = criticalDamage,
                physicalArmor = physicalArmor,
                magicalResistance = magicalResistance
            },
            0,
            0f,
            0f,
            null);
    }

    private static SquadData CreateSquad(
        string id,
        float initiative,
        int actionPoints,
        int warriorCount,
        int warriorHp,
        float accuracy,
        float evasion,
        float criticalChance,
        float criticalDamage,
        float physicalArmor)
    {
        List<WarriorData> warriors = new List<WarriorData>();
        for (int i = 0; i < warriorCount; i++)
        {
            warriors.Add(new WarriorData
            {
                id = $"{id}-warrior-{i}",
                maxHP = warriorHp,
                strength = 2f,
                dexterity = 1f
            });
        }
        return new SquadData(
            id,
            new CommanderData
            {
                id = $"{id}-commander",
                baseStats = new SquadBaseStats
                {
                    hp = 20,
                    actionPoints = actionPoints,
                    initiative = initiative,
                    strength = 8f,
                    dexterity = 7f,
                    accuracy = accuracy,
                    evasion = evasion,
                    criticalChance = criticalChance,
                    criticalDamage = criticalDamage,
                    physicalArmor = physicalArmor,
                    morale = 20f
                }
            },
            warriors);
    }

    private static void FindCellPair(
        MapGenerator generator,
        bool adjacent,
        out Vector2Int first,
        out Vector2Int second)
    {
        List<Vector2Int> playable = new List<Vector2Int>();
        for (int x = 0; x < generator.width; x++)
        {
            for (int y = 0; y < generator.height; y++)
            {
                if (generator.GetIsPlayable(x, y))
                    playable.Add(new Vector2Int(x, y));
            }
        }
        for (int i = 0; i < playable.Count; i++)
        {
            for (int j = i + 1; j < playable.Count; j++)
            {
                int distance = BattleTargetingService.GetGridDistance(
                    playable[i], playable[j], true);
                if ((adjacent && distance == 1) || (!adjacent && distance > 2))
                {
                    first = playable[i];
                    second = playable[j];
                    return;
                }
            }
        }
        Assert.Fail(adjacent
            ? "No adjacent playable cells were generated."
            : "No separated playable cells were generated.");
        first = default;
        second = default;
    }

    private static Transform NewChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private T Track<T>(T target) where T : UnityEngine.Object
    {
        cleanup.Add(target);
        return target;
    }

    private sealed class SequenceRandomSource : IBattleRandomSource
    {
        private readonly Queue<float> values;

        public SequenceRandomSource(params float[] sequence)
        {
            values = new Queue<float>(sequence ?? Array.Empty<float>());
        }

        public int CallCount { get; private set; }

        public float Next01()
        {
            CallCount++;
            return values.Count > 0 ? Mathf.Clamp01(values.Dequeue()) : 0f;
        }
    }

    private sealed class AttackSetup
    {
        public GameObject Root;
        public MapGenerator Generator;
        public MapRenderer Renderer;
        public SquadBattleBootstrap Bootstrap;
        public SquadBattleController Player;
        public SquadBattleController Enemy;
        public GridOccupancyService Occupancy;
        public BattleSquadSelectionController Selection;
        public BattleTurnController Turns;
        public SquadMovementService Movement;
        public AttackDefinition Definition;
        public AttackDefinition RangedDefinition;
        public BattleCombatRules Rules;
        public GridTacticalTerrainService Terrain;
        public BattleAttackService AttackService;
    }
}
