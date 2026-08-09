using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class EnemyTacticalAITests
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
    public void AdjacentAISelectsAndExecutesBasicAttackBeforeAbilitiesOrRally()
    {
        AISetup setup = CreateSetup(true);
        setup.Enemy.Runtime.ApplyMoraleLoss(100f);
        int beforeAP = setup.Enemy.Runtime.State.currentActionPoints;
        int beforeHP = setup.Player.Runtime.State.CurrentSquadHP;

        EnemyTacticalDecision first = setup.Decisions.Decide(setup.Enemy);
        EnemyTacticalDecision second = setup.Decisions.Decide(setup.Enemy);

        Assert.That(first.ActionType, Is.EqualTo(EnemyTacticalActionType.BasicAttack));
        Assert.That(first.Target, Is.SameAs(setup.Player));
        Assert.That(second.ActionType, Is.EqualTo(first.ActionType));
        Assert.That(second.Target.SquadId, Is.EqualTo(first.Target.SquadId));
        Assert.That(setup.Attacks.TryExecuteAttack(
            setup.Enemy,
            setup.Player,
            out BattleAttackResult result,
            setup.BasicAttack,
            BattleCommandAuthority.TacticalAI), Is.True);
        Assert.That(result.WasExecuted, Is.True);
        Assert.That(setup.Enemy.Runtime.State.currentActionPoints,
            Is.EqualTo(beforeAP - setup.BasicAttack.ActionPointCost));
        Assert.That(setup.Player.Runtime.State.CurrentSquadHP, Is.LessThan(beforeHP));
    }

    [Test]
    public void DistantAIUsesExistingPathfinderForReachableUnoccupiedAttackCellOn32Map()
    {
        AISetup setup = CreateSetup(false, 32, 2, false, 3, 6);

        EnemyTacticalDecision decision = setup.Decisions.Decide(setup.Enemy);

        Assert.That(decision.ActionType, Is.EqualTo(EnemyTacticalActionType.MoveToAttack));
        Assert.That(decision.MovementPlan, Is.Not.Null);
        Assert.That(decision.Destination, Is.Not.EqualTo(setup.Player.GridAnchor.CurrentCell));
        Assert.That(setup.Generator.GetIsPlayable(
            decision.Destination.x, decision.Destination.y), Is.True);
        Assert.That(setup.Occupancy.CanEnter(setup.Enemy, decision.Destination), Is.True);
        Assert.That(decision.PathCost, Is.GreaterThan(0).And
            .LessThanOrEqualTo(setup.Enemy.Runtime.State.currentActionPoints));
        Assert.That(decision.MovementPlan.Path.Skip(1).All(cell =>
            cell == decision.Destination || setup.Occupancy.CanEnter(setup.Enemy, cell)), Is.True);
        Assert.That(BattleTargetingService.GetGridDistance(
            decision.Destination,
            setup.Player.GridAnchor.CurrentCell,
            setup.Movement.AllowDiagonalMovement), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator ControllerMovesAttacksThroughProductionServicesAndReturnsTurnToPlayer()
    {
        yield return new EnterPlayMode();
        AISetup setup = CreateSetup(false, 12, 2, true, 3, 4);
        Vector2Int start = setup.Enemy.GridAnchor.CurrentCell;
        int beforeAP = setup.Enemy.Runtime.State.currentActionPoints;
        int beforeHP = setup.Player.Runtime.State.CurrentSquadHP;

        yield return WaitForAI(setup.AI, 400);

        Assert.That(setup.AI.MovementActionCount, Is.EqualTo(1));
        Assert.That(setup.AI.BasicAttackActionCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(setup.Enemy.GridAnchor.CurrentCell, Is.Not.EqualTo(start));
        Assert.That(setup.Occupancy.TryGetOccupiedCell(
            setup.Enemy, out Vector2Int occupied), Is.True);
        Assert.That(occupied, Is.EqualTo(setup.Enemy.GridAnchor.CurrentCell));
        Assert.That(setup.Enemy.Runtime.State.currentActionPoints, Is.LessThan(beforeAP));
        Assert.That(setup.Player.Runtime.State.CurrentSquadHP, Is.LessThan(beforeHP));
        Assert.That(setup.Turns.ActiveSquad, Is.SameAs(setup.Player));
        Assert.That(setup.AI.EndTurnRequestCount, Is.EqualTo(1));
        Assert.That(setup.AI.PeakConcurrentRoutineCount, Is.EqualTo(1));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ControllerPerformsMultipleActionsAndObeysSafetyLimit()
    {
        yield return new EnterPlayMode();
        AISetup multiple = CreateSetup(true, 8, 2, true);
        yield return WaitForAI(multiple.AI, 300);
        Assert.That(multiple.AI.BasicAttackActionCount, Is.GreaterThanOrEqualTo(2));
        Assert.That(multiple.Enemy.Runtime.State.currentActionPoints,
            Is.GreaterThanOrEqualTo(0));

        AISetup limited = CreateSetup(true, 8, 2, true, actionLimit: 1);
        yield return WaitForAI(limited.AI, 100);
        Assert.That(limited.AI.LastTurnSummary.actionCount, Is.EqualTo(1));
        Assert.That(limited.AI.LastTurnSummary.endReason, Does.Contain("Safety action limit"));
        Assert.That(limited.AI.BasicAttackActionCount, Is.EqualTo(1));
        Assert.That(limited.AI.EndTurnRequestCount, Is.EqualTo(1));
        yield return new ExitPlayMode();
    }

    [Test]
    public void TargetPriorityIsDistanceThenHpThenStableId()
    {
        AISetup setup = CreateSetup(false, 10, 2, false, 3, 5);
        Vector2Int sharedCell = FindPlayableAtDistance(
            setup.Generator,
            setup.Enemy.GridAnchor.CurrentCell,
            2);
        SquadBattleController alpha = CreateDetachedController(
            setup, "alpha", sharedCell, 100);
        SquadBattleController beta = CreateDetachedController(
            setup, "beta", sharedCell, 100);

        Assert.That(setup.Decisions.CompareTargetPriority(
            setup.Enemy, alpha, beta), Is.LessThan(0),
            "Stable SquadId is the final deterministic tie-break.");

        beta.Runtime.ApplyDamage(1, SquadDamageDistribution.SingleTarget);
        Assert.That(setup.Decisions.CompareTargetPriority(
            setup.Enemy, beta, alpha), Is.LessThan(0),
            "Lower current squad HP wins after equal distance.");

        Vector2Int nearerCell = FindPlayableAtDistance(
            setup.Generator,
            setup.Enemy.GridAnchor.CurrentCell,
            1);
        SquadBattleController nearer = CreateDetachedController(
            setup, "z-nearer", nearerCell, 100);
        Assert.That(setup.Decisions.CompareTargetPriority(
            setup.Enemy, nearer, beta), Is.LessThan(0),
            "Grid distance has priority over HP and ID.");
    }

    [Test]
    public void DefeatedParticipantsAndWrongAuthorityCannotMutateAp()
    {
        AISetup setup = CreateSetup(true);
        int playerAP = setup.Player.Runtime.State.currentActionPoints;
        Assert.That(setup.Attacks.TryExecuteAttack(
            setup.Player,
            setup.Enemy,
            out _,
            setup.BasicAttack,
            BattleCommandAuthority.TacticalAI), Is.False);
        Assert.That(setup.Player.Runtime.State.currentActionPoints, Is.EqualTo(playerAP));

        int enemyAP = setup.Enemy.Runtime.State.currentActionPoints;
        setup.Enemy.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);
        EnemyTacticalDecision decision = setup.Decisions.Decide(setup.Enemy);
        Assert.That(decision.ActionType, Is.EqualTo(EnemyTacticalActionType.EndTurn));
        Assert.That(setup.Enemy.Runtime.State.currentActionPoints, Is.EqualTo(enemyAP));
    }

    [Test]
    public void LowMoraleRallyIsFallbackOnlyAndCooldownPreventsReuse()
    {
        AISetup setup = CreateSetup(false, 12, 2, false, 6, 9);
        setup.Enemy.Runtime.ApplyMoraleLoss(100f);
        SpendUntil(setup.Enemy, 2);

        EnemyTacticalDecision rallyDecision = setup.Decisions.Decide(setup.Enemy);
        Assert.That(rallyDecision.ActionType, Is.EqualTo(EnemyTacticalActionType.Rally));
        Assert.That(setup.Abilities.TryExecuteAbility(
            setup.Enemy,
            setup.Enemy,
            setup.Rally,
            out BattleAbilityResult rallyResult,
            BattleCommandAuthority.TacticalAI), Is.True);
        Assert.That(rallyResult.MoraleRestored, Is.GreaterThan(0f));
        Assert.That(setup.Abilities.GetRuntimeState(
            setup.Enemy.SquadId, setup.Rally.StableId).remainingCooldown, Is.EqualTo(2));

        setup.Enemy.Runtime.BeginTurn();
        setup.Enemy.Runtime.ApplyMoraleLoss(100f);
        SpendUntil(setup.Enemy, 2);
        EnemyTacticalDecision blocked = setup.Decisions.Decide(setup.Enemy);
        Assert.That(blocked.ActionType, Is.EqualTo(EnemyTacticalActionType.MoveToAttack));
        Assert.That(blocked.ActionType, Is.Not.EqualTo(EnemyTacticalActionType.Rally));
        Assert.That(setup.Enemy.Runtime.State.currentActionPoints, Is.EqualTo(2));
    }

    [Test]
    public void ExistingPowerAndSweepAbilitiesAreSelectedWhenHigherPriorityBasicIsUnavailable()
    {
        AISetup setup = CreateSetup(true, 8, 5);
        SpendUntil(setup.Enemy, 3);
        EnemyTacticalDecision power = setup.Decisions.Decide(setup.Enemy);
        Assert.That(power.ActionType, Is.EqualTo(EnemyTacticalActionType.PowerStrike));
        Assert.That(power.Ability, Is.SameAs(setup.Power));

        setup.Enemy.Runtime.BeginTurn();
        Assert.That(setup.Abilities.TryExecuteAbility(
            setup.Enemy,
            setup.Player,
            setup.Power,
            out _,
            BattleCommandAuthority.TacticalAI), Is.True);
        setup.Enemy.Runtime.BeginTurn();
        SpendUntil(setup.Enemy, 4);
        EnemyTacticalDecision sweep = setup.Decisions.Decide(setup.Enemy);
        Assert.That(sweep.ActionType, Is.EqualTo(EnemyTacticalActionType.SweepingBlow));
        Assert.That(sweep.Ability, Is.SameAs(setup.Sweep));
    }

    [UnityTest]
    public IEnumerator CompletionStopsAIWithoutEndingTurnAndDuplicateBeginIsRejected()
    {
        AISetup setup = CreateSetup(true, 8, 2, true);
        Assert.That(setup.AI.BeginTurn(setup.Enemy), Is.False);
        Assert.That(setup.AI.DuplicateBeginRejectedCount, Is.EqualTo(1));

        setup.Player.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);
        yield return null;
        yield return null;

        Assert.That(setup.Completion.State, Is.EqualTo(BattleCompletionState.Completed));
        Assert.That(setup.AI.EndTurnRequestCount, Is.Zero);
        Assert.That(setup.AI.BasicAttackActionCount, Is.Zero);
        Assert.That(setup.Turns.IsBattleLocked, Is.True);
    }

    [UnityTest]
    public IEnumerator DisabledControllerUnsubscribesAndDoesNotStartAnotherRoutine()
    {
        yield return new EnterPlayMode();
        AISetup setup = CreateSetup(true, 8, 2, true, actionLimit: 1);
        yield return WaitForAI(setup.AI, 100);
        int begun = setup.AI.BegunTurnCount;
        setup.AI.enabled = false;

        Assert.That(setup.Turns.ActiveSquad, Is.SameAs(setup.Player));
        Assert.That(setup.Turns.EndCurrentTurn(), Is.True);
        yield return null;

        Assert.That(setup.Turns.ActiveSquad, Is.SameAs(setup.Enemy));
        Assert.That(setup.AI.BegunTurnCount, Is.EqualTo(begun));
        Assert.That(setup.AI.IsExecutingTurn, Is.False);
        yield return new ExitPlayMode();
    }

    [Test]
    public void ProductionSceneOwnsOneExplicitAIControllerAndDisablesAutoSkip()
    {
        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/Raw_Alpha_BattleMode.unity",
            OpenSceneMode.Single);
        EnemyTacticalAIController[] ai = FindInScene<EnemyTacticalAIController>(scene);
        BattleTurnController turns = FindInScene<BattleTurnController>(scene).Single();
        SquadBattleTacticalBootstrap tactical =
            FindInScene<SquadBattleTacticalBootstrap>(scene).Single();
        Assert.That(ai.Length, Is.EqualTo(1));

        SerializedObject turnSerialized = new SerializedObject(turns);
        Assert.That(turnSerialized.FindProperty("autoSkipAITurns").boolValue, Is.False);
        SerializedObject tacticalSerialized = new SerializedObject(tactical);
        Assert.That(tacticalSerialized.FindProperty("enemyAI").objectReferenceValue,
            Is.SameAs(ai[0]));
        SerializedObject aiSerialized = new SerializedObject(ai[0]);
        foreach (string propertyName in new[]
                 {
                     "squadBootstrap", "mapGenerator", "turnController", "occupancy",
                     "movementService", "attackService", "abilityService",
                     "completionController"
                 })
        {
            Assert.That(aiSerialized.FindProperty(propertyName).objectReferenceValue,
                Is.Not.Null, $"AI serialized reference '{propertyName}' is missing.");
        }

        string source = System.IO.File.ReadAllText(
            "Assets/Scripts/Squads/Tactical/EnemyTacticalAIController.cs");
        Assert.That(source, Does.Not.Contain("FindObjectOfType"));
        Assert.That(source, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(source, Does.Not.Contain("void Update("));
    }

    private AISetup CreateSetup(
        bool adjacent,
        int mapSize = 8,
        int basicActionPointCost = 2,
        bool createAI = false,
        int minimumPathCost = 3,
        int maximumPathCost = 8,
        int actionLimit = 8)
    {
        GameObject root = Track(new GameObject("EnemyAITestRoot"));
        GameObject mapObject = NewChild(root.transform, "Map").gameObject;
        MapGenerator generator = mapObject.AddComponent<MapGenerator>();
        generator.autoGenerate = false;
        generator.width = mapSize;
        generator.height = mapSize;
        generator.playableCount = Mathf.Max(4, mapSize * mapSize * 3 / 4);
        generator.seed = 12091;
        generator.Generate();
        MapRenderer renderer = mapObject.AddComponent<MapRenderer>();
        renderer.autoRender = false;
        renderer.mapGenerator = generator;
        FindSpawnCells(
            generator,
            adjacent,
            minimumPathCost,
            maximumPathCost,
            out Vector2Int enemyCell,
            out Vector2Int playerCell);

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
            new[] { CreateSquad("player", 10f) },
            new[] { CreateSquad("enemy", 20f) });
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
        BattleTurnController turns = NewChild(root.transform, "Turns").gameObject
            .AddComponent<BattleTurnController>();
        turns.Configure(bootstrap, false, 0f);
        Assert.That(turns.StartBattle(), Is.True);
        Assert.That(turns.ActiveSquad, Is.SameAs(enemy));
        SquadMovementService movement = NewChild(root.transform, "Movement").gameObject
            .AddComponent<SquadMovementService>();
        movement.Configure(generator, renderer, occupancy, turns, true, 0.02f);
        Assert.That(movement.Initialize(), Is.True);
        BattleCommandModeController modes = NewChild(root.transform, "Modes").gameObject
            .AddComponent<BattleCommandModeController>();

        AttackDefinition basic = Track(ScriptableObject.CreateInstance<AttackDefinition>());
        basic.ConfigureDevelopment(
            "ai-basic", "AI Basic", 1, basicActionPointCost, 0.1f, null, null);
        BattleCombatRules rules = Track(ScriptableObject.CreateInstance<BattleCombatRules>());
        rules.ConfigureDevelopment(1f, 1f, 1f, 0.8f, 1);
        BattleAttackService attacks = NewChild(root.transform, "Attacks").gameObject
            .AddComponent<BattleAttackService>();
        attacks.Configure(
            bootstrap, turns, selection, movement, basic, rules, true, 42,
            new ConstantBattleRandom());
        Assert.That(attacks.Initialize(), Is.True);

        GameObject moveCommandsObject = NewChild(root.transform, "MoveCommands").gameObject;
        moveCommandsObject.SetActive(false);
        MovementCommandController moveCommands =
            moveCommandsObject.AddComponent<MovementCommandController>();
        GameObject attackCommandsObject = NewChild(root.transform, "AttackCommands").gameObject;
        attackCommandsObject.SetActive(false);
        AttackCommandController attackCommands =
            attackCommandsObject.AddComponent<AttackCommandController>();
        GameObject hudObject = NewChild(root.transform, "HUD").gameObject;
        hudObject.SetActive(false);
        BattleHUDController hud = hudObject.AddComponent<BattleHUDController>();
        GameObject panelObject = NewChild(root.transform, "ResultPanel").gameObject;
        panelObject.SetActive(false);
        BattleResultPanelView panel = panelObject.AddComponent<BattleResultPanelView>();
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
            "battle-ai-test-" + Guid.NewGuid().ToString("N"),
            "2026-08-09T00:00:00Z"), Is.True);

        AttackDefinition powerAttack = Track(ScriptableObject.CreateInstance<AttackDefinition>());
        powerAttack.ConfigureDevelopmentAbilityEffect(
            "ai-power-effect", "Power", 2, 3, 0.2f,
            SquadDamageDistribution.SingleTarget, null, null);
        AttackDefinition sweepAttack = Track(ScriptableObject.CreateInstance<AttackDefinition>());
        sweepAttack.ConfigureDevelopmentAbilityEffect(
            "ai-sweep-effect", "Sweep", 2, 4, 0.2f,
            SquadDamageDistribution.Area, null, null);
        AbilityDefinition power = Track(ScriptableObject.CreateInstance<AbilityDefinition>());
        power.ConfigureDevelopmentAttack(
            EnemyTacticalDecisionService.PowerStrikeId,
            "Power Strike", "Heavy", 3, 1, Key.Digit1, powerAttack, null);
        AbilityDefinition sweep = Track(ScriptableObject.CreateInstance<AbilityDefinition>());
        sweep.ConfigureDevelopmentAttack(
            EnemyTacticalDecisionService.SweepingBlowId,
            "Sweeping Blow", "Area", 4, 2, Key.Digit2, sweepAttack, null);
        AbilityDefinition rally = Track(ScriptableObject.CreateInstance<AbilityDefinition>());
        rally.ConfigureDevelopmentRally(
            EnemyTacticalDecisionService.RallyId,
            "Rally", "Morale", 2, 2, 20f, Key.Digit3, null);
        BattleAbilityService abilities = NewChild(root.transform, "Abilities").gameObject
            .AddComponent<BattleAbilityService>();
        abilities.Configure(
            bootstrap, turns, selection, movement, attacks, completion,
            new[] { power, sweep, rally });
        Assert.That(abilities.Initialize(), Is.True);
        completion.ConfigureAbilities(abilities, null);

        EnemyTacticalDecisionService decisions = new EnemyTacticalDecisionService(
            bootstrap, generator, occupancy, movement, attacks, abilities, completion);
        EnemyTacticalAIController ai = null;
        if (createAI)
        {
            ai = NewChild(root.transform, "EnemyAI").gameObject
                .AddComponent<EnemyTacticalAIController>();
            ai.Configure(
                bootstrap, generator, turns, occupancy, movement, attacks,
                abilities, completion, null, actionLimit, false);
            Assert.That(ai.Initialize(), Is.True);
        }

        return new AISetup
        {
            Root = root,
            Bootstrap = bootstrap,
            Generator = generator,
            Renderer = renderer,
            Player = player,
            Enemy = enemy,
            Occupancy = occupancy,
            Turns = turns,
            Movement = movement,
            BasicAttack = basic,
            Attacks = attacks,
            Completion = completion,
            Power = power,
            Sweep = sweep,
            Rally = rally,
            Abilities = abilities,
            Decisions = decisions,
            AI = ai
        };
    }

    private SquadBattleController CreateDetachedController(
        AISetup setup,
        string id,
        Vector2Int cell,
        int hp)
    {
        GameObject root = Track(new GameObject(id));
        SquadGridAnchor anchor = root.AddComponent<SquadGridAnchor>();
        SquadBattleController controller = root.AddComponent<SquadBattleController>();
        controller.Configure(anchor, null);
        SquadData data = CreateSquad(id, 1f, hp);
        Assert.That(controller.InitializeAtCell(
            data, null, setup.Generator, setup.Renderer, cell,
            BattleSide.Player, SquadControlType.Human, 100 + cleanup.Count), Is.True);
        return controller;
    }

    private static SquadData CreateSquad(
        string id,
        float initiative,
        int commanderHP = 100)
    {
        return new SquadData(
            id,
            new CommanderData
            {
                id = id + "-commander",
                baseStats = new SquadBaseStats
                {
                    hp = commanderHP,
                    actionPoints = 12,
                    initiative = initiative,
                    strength = 5,
                    dexterity = 5,
                    accuracy = 1f,
                    evasion = 0f,
                    criticalChance = 0f,
                    criticalDamage = 1.5f,
                    physicalArmor = 0f,
                    morale = 50,
                    resolve = 0
                }
            },
            new[]
            {
                new WarriorData
                {
                    id = id + "-warrior",
                    maxHP = 30,
                    strength = 1,
                    dexterity = 1
                }
            });
    }

    private static void FindSpawnCells(
        MapGenerator generator,
        bool adjacent,
        int minimumPathCost,
        int maximumPathCost,
        out Vector2Int enemy,
        out Vector2Int player)
    {
        List<Vector2Int> playable = GetPlayable(generator);
        for (int i = 0; i < playable.Count; i++)
        {
            for (int j = playable.Count - 1; j >= 0; j--)
            {
                if (i == j || !GridPathfinder.TryBuildPath(
                        generator, playable[i], playable[j], true, null,
                        out List<Vector2Int> path))
                {
                    continue;
                }
                int cost = path.Count - 1;
                if ((adjacent && cost == 1) ||
                    (!adjacent && cost >= minimumPathCost && cost <= maximumPathCost))
                {
                    enemy = playable[i];
                    player = playable[j];
                    return;
                }
            }
        }
        throw new AssertionException("No test spawn pair satisfies the requested path distance.");
    }

    private static Vector2Int FindPlayableAtDistance(
        MapGenerator generator,
        Vector2Int origin,
        int distance)
    {
        foreach (Vector2Int cell in GetPlayable(generator))
        {
            if (BattleTargetingService.GetGridDistance(origin, cell, true) == distance)
                return cell;
        }
        throw new AssertionException($"No playable cell at distance {distance} was found.");
    }

    private static List<Vector2Int> GetPlayable(MapGenerator generator)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = 0; x < generator.Width; x++)
        for (int y = 0; y < generator.Height; y++)
        {
            if (generator.GetIsPlayable(x, y))
                cells.Add(new Vector2Int(x, y));
        }
        return cells;
    }

    private static void SpendUntil(SquadBattleController controller, int remaining)
    {
        int spend = controller.Runtime.State.currentActionPoints - remaining;
        if (spend > 0)
            Assert.That(controller.Runtime.TrySpendActionPoints(spend), Is.True);
    }

    private static IEnumerator WaitForAI(EnemyTacticalAIController ai, int frameLimit)
    {
        int frames = 0;
        while (ai != null && ai.IsExecutingTurn && frames++ < frameLimit)
            yield return null;
        Assert.That(frames, Is.LessThan(frameLimit), "AI turn exceeded the bounded test frame limit.");
        Assert.That(ai.IsExecutingTurn, Is.False);
        Assert.That(ai.CompletedTurnCount, Is.EqualTo(1));
    }

    private static Transform NewChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        List<T> values = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            values.AddRange(root.GetComponentsInChildren<T>(true));
        return values.ToArray();
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        cleanup.Add(value);
        return value;
    }

    private sealed class ConstantBattleRandom : IBattleRandomSource
    {
        public float Next01() => 0f;
    }

    private sealed class AISetup
    {
        public GameObject Root;
        public SquadBattleBootstrap Bootstrap;
        public MapGenerator Generator;
        public MapRenderer Renderer;
        public SquadBattleController Player;
        public SquadBattleController Enemy;
        public GridOccupancyService Occupancy;
        public BattleTurnController Turns;
        public SquadMovementService Movement;
        public AttackDefinition BasicAttack;
        public BattleAttackService Attacks;
        public BattleCompletionController Completion;
        public AbilityDefinition Power;
        public AbilityDefinition Sweep;
        public AbilityDefinition Rally;
        public BattleAbilityService Abilities;
        public EnemyTacticalDecisionService Decisions;
        public EnemyTacticalAIController AI;
    }
}
