using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BattleLifecycleTests
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
    public void VictoryCompletesOnceAndLocksTurnTargetingAndCommands()
    {
        LifecycleSetup setup = CreateLifecycleSetup();

        setup.Enemy.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);

        Assert.That(setup.Completion.State, Is.EqualTo(BattleCompletionState.Completed));
        Assert.That(setup.Completion.Outcome.resultType, Is.EqualTo(BattleResultType.Victory));
        Assert.That(setup.Completion.Outcome.winningSide, Is.EqualTo(BattleSide.Player));
        Assert.That(setup.Completion.CompletionCount, Is.EqualTo(1));
        Assert.That(setup.Completion.AutosaveAttemptCount, Is.EqualTo(1));
        Assert.That(setup.Turns.IsBattleLocked, Is.True);
        Assert.That(setup.Turns.ActiveSquad, Is.Null);
        Assert.That(setup.Modes.IsLocked, Is.True);
        Assert.That(setup.Movement.CommandsEnabled, Is.False);
        Assert.That(setup.Attacks.CommandsEnabled, Is.False);

        Assert.That(setup.Completion.EvaluateCompletion(), Is.False);
        setup.Completion.Outcome.participantResults[1].commanderDefeatedInBattle = true;
        Assert.That(setup.Completion.EvaluateCompletion(), Is.False);
        Assert.That(setup.Completion.CompletionCount, Is.EqualTo(1));
        Assert.That(setup.Completion.AutosaveAttemptCount, Is.EqualTo(1));
    }

    [Test]
    public void DefeatAndAtomicDrawUseFinalCommittedState()
    {
        LifecycleSetup defeat = CreateLifecycleSetup();
        defeat.Player.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);
        Assert.That(defeat.Completion.Outcome.resultType, Is.EqualTo(BattleResultType.Defeat));
        Assert.That(defeat.Completion.Outcome.winningSide, Is.EqualTo(BattleSide.Enemy));

        LifecycleSetup draw = CreateLifecycleSetup();
        Assert.That(draw.Completion.BeginCommittedResolution(), Is.True);
        draw.Enemy.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);
        draw.Player.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);
        Assert.That(draw.Completion.State, Is.EqualTo(BattleCompletionState.Running),
            "Completion must not run in the middle of one atomic resolution.");
        Assert.That(draw.Completion.EndCommittedResolution(), Is.True);
        Assert.That(draw.Completion.Outcome.resultType, Is.EqualTo(BattleResultType.Draw));
        Assert.That(draw.Completion.CompletionCount, Is.EqualTo(1));
    }

    [Test]
    public void BuilderUsesStableIdsIsRepeatableAndDoesNotMutateRuntime()
    {
        LifecycleSetup setup = CreateLifecycleSetup(initializeCompletion: false);
        BattleResultBuilder builder = new BattleResultBuilder();
        Assert.That(builder.Initialize(
            setup.Bootstrap.SpawnedControllers,
            "battle-stable",
            "encounter-stable",
            77,
            "2026-08-04T00:00:00.0000000Z"), Is.True);
        int hpBefore = setup.Player.Runtime.State.CurrentSquadHP;
        int apBefore = setup.Player.Runtime.State.currentActionPoints;

        BattleOutcome first = builder.Build(
            BattleResultType.Victory,
            BattleSide.Player,
            BattleSide.Enemy,
            2,
            5,
            "2026-08-04T00:01:00.0000000Z").Outcome;
        BattleOutcome second = builder.Build(
            BattleResultType.Victory,
            BattleSide.Player,
            BattleSide.Enemy,
            2,
            5,
            "2026-08-04T00:01:00.0000000Z").Outcome;

        Assert.That(JsonUtility.ToJson(second), Is.EqualTo(JsonUtility.ToJson(first)));
        Assert.That(first.participantResults.Select(result => result.squadId).Distinct().Count(),
            Is.EqualTo(2));
        Assert.That(first.participantResults.SelectMany(result => result.initialWarriorIds)
            .Distinct().Count(), Is.EqualTo(4));
        Assert.That(first.participantResults.All(result =>
            !string.IsNullOrWhiteSpace(result.commanderId)), Is.True);
        Assert.That(setup.Player.Runtime.State.CurrentSquadHP, Is.EqualTo(hpBefore));
        Assert.That(setup.Player.Runtime.State.currentActionPoints, Is.EqualTo(apBefore));
    }

    [Test]
    public void BuilderRejectsDuplicateStableMemberIds()
    {
        LifecycleSetup setup = CreateLifecycleSetup(initializeCompletion: false);
        SquadData duplicate = CreateSquad("third", 5f);
        duplicate.Warriors[0].id = setup.Player.Runtime.Data.Warriors[0].id;
        GameObject root = Track(new GameObject("DuplicateController"));
        SquadGridAnchor anchor = root.AddComponent<SquadGridAnchor>();
        SquadBattleController controller = root.AddComponent<SquadBattleController>();
        controller.Configure(anchor, null);
        Assert.That(controller.InitializeAtCell(
            duplicate,
            null,
            setup.Generator,
            setup.Renderer,
            FindThirdPlayableCell(setup.Generator,
                setup.Player.GridAnchor.CurrentCell,
                setup.Enemy.GridAnchor.CurrentCell),
            BattleSide.Player,
            SquadControlType.AI,
            3), Is.True);

        BattleResultBuilder builder = new BattleResultBuilder();
        Assert.That(builder.Initialize(
            new[] { setup.Player, setup.Enemy, controller },
            "battle-duplicate",
            string.Empty,
            1), Is.False);
    }

    [Test]
    public void ResultApplierUsesWarriorIdsIsAtomicAndIdempotent()
    {
        LifecycleSetup setup = CreateLifecycleSetup(initializeCompletion: false);
        BattleResultBuilder builder = CreateBuilder(setup, "battle-casualty");
        string defeatedId = setup.Player.Runtime.State.warriors[0].warriorId;
        setup.Player.Runtime.ApplyDamage(
            setup.Player.Runtime.State.warriors[0].currentHP,
            SquadDamageDistribution.SingleTarget);
        BattleOutcome outcome = builder.Build(
            BattleResultType.Victory,
            BattleSide.Player,
            BattleSide.Enemy,
            1,
            1,
            "2026-08-04T00:02:00Z").Outcome;
        string portrait = setup.Player.Runtime.Data.CommanderPortraitId;
        BattleResultApplier applier = new BattleResultApplier(
            setup.Repository,
            setup.PostBattleRules,
            _ => new FixedPostBattleRandom(0f));

        BattleResultApplicationResult first = applier.Apply(outcome);
        Assert.That(first.Success, Is.True);
        SquadData persistent = setup.Repository.GetSquad("player");
        Assert.That(persistent.Warriors.Select(warrior => warrior.id),
            Does.Not.Contain(defeatedId));
        Assert.That(persistent.Warriors.Count, Is.EqualTo(1));
        Assert.That(persistent.CommanderPortraitId, Is.EqualTo(portrait));
        Assert.That(setup.Repository.HasAppliedBattle(outcome.battleId), Is.True);

        BattleResultApplicationResult second = applier.Apply(outcome);
        Assert.That(second.Success, Is.True);
        Assert.That(second.AlreadyApplied, Is.True);
        Assert.That(persistent.Warriors.Count, Is.EqualTo(1));

        string saved = setup.Repository.CaptureState();
        GameObject restoredRoot = Track(new GameObject("RestoredRepository"));
        SquadSaveParticipant restored = restoredRoot.AddComponent<SquadSaveParticipant>();
        restored.RestoreState(saved);
        Assert.That(restored.GetSquad("player").Warriors.Count, Is.EqualTo(1));
        Assert.That(restored.HasAppliedBattle(outcome.battleId), Is.True);
        Assert.That(restored.ActiveRuntimeCount, Is.Zero,
            "Battle runtime is not part of the post-battle persistent autosave.");
    }

    [Test]
    public void InvalidApplicationDoesNotPartiallyMutatePersistentSquad()
    {
        LifecycleSetup setup = CreateLifecycleSetup(initializeCompletion: false);
        BattleResultBuilder builder = CreateBuilder(setup, "battle-invalid");
        BattleOutcome outcome = builder.Build(
            BattleResultType.Victory,
            BattleSide.Player,
            BattleSide.Enemy,
            1,
            0,
            "2026-08-04T00:02:00Z").Outcome;
        SquadBattleResult player = outcome.participantResults.Single(
            result => result.side == BattleSide.Player);
        player.commanderId = "wrong-commander";
        string before = setup.Repository.CaptureState();

        BattleResultApplicationResult result = new BattleResultApplier(
            setup.Repository,
            setup.PostBattleRules,
            _ => new FixedPostBattleRandom(0f)).Apply(outcome);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("Commander ID mismatch"));
        Assert.That(setup.Repository.CaptureState(), Is.EqualTo(before));
        Assert.That(outcome.persistentMutationsApplied, Is.False);
    }

    [Test]
    public void CommanderOutcomeIsDeterministicAndDebuffDoesNotDuplicate()
    {
        LifecycleSetup survivor = CreateLifecycleSetup(initializeCompletion: false);
        BattleResultBuilder survivorBuilder = CreateBuilder(survivor, "battle-survival");
        survivor.Player.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);
        BattleOutcome survivorOutcome = survivorBuilder.Build(
            BattleResultType.Defeat,
            BattleSide.Enemy,
            BattleSide.Player,
            1,
            0).Outcome;
        BattleResultApplier survivorApplier = new BattleResultApplier(
            survivor.Repository,
            survivor.PostBattleRules,
            _ => new FixedPostBattleRandom(0f));
        Assert.That(survivorApplier.Apply(survivorOutcome).Success, Is.True);
        SquadData survived = survivor.Repository.GetSquad("player");
        Assert.That(survived.Status, Is.EqualTo(PersistentSquadStatus.InactiveNoWarriors));
        Assert.That(survived.Commander.permanentDebuffIds,
            Is.EqualTo(new[] { "DEV_BattleScar" }));
        Assert.That(survived.Commander.permanentDebuffs.Single().sourceBattleId,
            Is.EqualTo("battle-survival"));
        Assert.That(survived.PermanentModifiers.resolve, Is.EqualTo(-1f));
        Assert.That(survivorApplier.Apply(survivorOutcome).AlreadyApplied, Is.True);
        Assert.That(survived.Commander.permanentDebuffIds.Count, Is.EqualTo(1));

        LifecycleSetup killed = CreateLifecycleSetup(initializeCompletion: false);
        BattleResultBuilder killedBuilder = CreateBuilder(killed, "battle-death");
        killed.Player.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);
        BattleOutcome killedOutcome = killedBuilder.Build(
            BattleResultType.Defeat,
            BattleSide.Enemy,
            BattleSide.Player,
            1,
            0).Outcome;
        BattleResultApplier killedApplier = new BattleResultApplier(
            killed.Repository,
            killed.PostBattleRules,
            _ => new FixedPostBattleRandom(1f));
        Assert.That(killedApplier.Apply(killedOutcome).Success, Is.True);
        SquadData dead = killed.Repository.GetSquad("player");
        Assert.That(dead.Status, Is.EqualTo(PersistentSquadStatus.CommanderLost));
        Assert.That(dead.IsBattleEligible, Is.False);
        Assert.That(dead.Commander.permanentDebuffIds, Is.Empty);
    }

    [Test]
    public void UndefeatedCommanderSurvivesWithoutConsumingPostBattleRandom()
    {
        LifecycleSetup setup = CreateLifecycleSetup(initializeCompletion: false);
        BattleResultBuilder builder = CreateBuilder(setup, "battle-healthy");
        BattleOutcome outcome = builder.Build(
            BattleResultType.Victory,
            BattleSide.Player,
            BattleSide.Enemy,
            1,
            0).Outcome;
        FixedPostBattleRandom random = new FixedPostBattleRandom(1f);
        Assert.That(new BattleResultApplier(
            setup.Repository,
            setup.PostBattleRules,
            _ => random).Apply(outcome).Success, Is.True);
        SquadBattleResult player = outcome.participantResults.Single(
            result => result.side == BattleSide.Player);
        Assert.That(player.commanderOutcome,
            Is.EqualTo(CommanderPostBattleOutcomeType.SurvivedNormally));
        Assert.That(random.CallCount, Is.Zero);
        Assert.That(setup.Repository.GetSquad("player").Status,
            Is.EqualTo(PersistentSquadStatus.Active));
    }

    [Test]
    public void SaveFailureKeepsContinueBlockedAndRetryRunsOneAdditionalAutosave()
    {
        int saveCalls = 0;
        LifecycleSetup setup = CreateLifecycleSetup(
            autosave: (_, _) =>
            {
                saveCalls++;
                return saveCalls == 1
                    ? SaveOperationResult.Fail("disk unavailable")
                    : SaveOperationResult.Ok();
            });
        setup.Enemy.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);

        Assert.That(setup.Completion.State, Is.EqualTo(BattleCompletionState.Completed));
        Assert.That(setup.Completion.CanContinue, Is.False);
        Assert.That(setup.Completion.ContinueToOverworld(), Is.False);
        Assert.That(setup.Completion.RetryAutosave(), Is.True);
        Assert.That(setup.Completion.CanContinue, Is.True);
        Assert.That(saveCalls, Is.EqualTo(2));
        Assert.That(setup.Completion.AutosaveAttemptCount, Is.EqualTo(2));
    }

    [Test]
    public void ContinueAndReturnContextsAreOneShot()
    {
        string loadedScene = null;
        LifecycleSetup setup = CreateLifecycleSetup(sceneLoader: scene => loadedScene = scene);
        setup.Enemy.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);

        Assert.That(setup.Completion.ContinueToOverworld(), Is.True);
        Assert.That(setup.Completion.ContinueToOverworld(), Is.False);
        Assert.That(setup.Completion.TransitionRequestCount, Is.EqualTo(1));
        Assert.That(loadedScene, Is.EqualTo("first_try"));
        Assert.That(BattleReturnContext.TryPeek(out BattleReturnData peek), Is.True);
        Assert.That(peek.outcome.battleId, Is.EqualTo(setup.Completion.Outcome.battleId));
        Assert.That(BattleReturnContext.TryConsume(out BattleReturnData consumed), Is.True);
        Assert.That(consumed.targetScene, Is.EqualTo("first_try"));
        Assert.That(BattleReturnContext.TryConsume(out _), Is.False);
        Assert.That(BattleEncounterContext.HasEncounterData, Is.False);
        Assert.That(BattleSquadSelectionContext.HasSelection, Is.False);
    }

    [Test]
    public void DevelopmentPostBattleAssetsHaveValidPersistentDebuffScriptBinding()
    {
        PersistentDebuffDefinition debuff =
            AssetDatabase.LoadAssetAtPath<PersistentDebuffDefinition>(
                "Assets/GameData/BattleLifecycle/DEV_BattleScar.asset");
        PostBattleRules rules =
            AssetDatabase.LoadAssetAtPath<PostBattleRules>(
                "Assets/GameData/BattleLifecycle/DEV_PostBattleRules.asset");

        Assert.That(debuff, Is.Not.Null);
        Assert.That(debuff.Validate(out string debuffError), Is.True, debuffError);
        Assert.That(debuff.StableId, Is.EqualTo("DEV_BattleScar"));
        Assert.That(debuff.ResolveModifier, Is.EqualTo(-1f));
        Assert.That(debuff.Persistent, Is.True);
        Assert.That(debuff.Stackable, Is.False);

        Assert.That(rules, Is.Not.Null);
        Assert.That(rules.Validate(out string rulesError), Is.True, rulesError);
        Assert.That(rules.DefeatedCommanderSurvivalChance, Is.EqualTo(0.2f));
        Assert.That(rules.SurvivorDebuff, Is.SameAs(debuff));
    }

    [Test]
    public void SceneContractsContainOneLifecycleOwnerOneModalAndOverworldPersistenceComposition()
    {
        Scene battle = EditorSceneManager.OpenScene(
            "Assets/Scenes/Raw_Alpha_BattleMode.unity",
            OpenSceneMode.Single);
        Assert.That(FindInScene<BattleCompletionController>(battle).Length, Is.EqualTo(1));
        Assert.That(FindInScene<BattleResultPanelView>(battle).Length, Is.EqualTo(1));
        BattleResultPanelView resultPanel = FindInScene<BattleResultPanelView>(battle)[0];
        Assert.That(resultPanel.GetComponentsInParent<Canvas>(true).Length, Is.EqualTo(1));
        GameObject modalLayer = resultPanel.transform.parent.gameObject;
        modalLayer.SetActive(false);
        resultPanel.Show(new BattleOutcome
        {
            battleId = "battle-modal-contract",
            resultType = BattleResultType.Victory,
            participantResults = new List<SquadBattleResult>()
        }, SaveOperationResult.Ok());
        Assert.That(modalLayer.activeInHierarchy, Is.True,
            "Showing a result must activate the serialized ModalLayer parent.");
        Assert.That(resultPanel.IsVisible, Is.True);
        Assert.That(resultPanel.ContinueButton.interactable, Is.True);
        resultPanel.Hide();
        Assert.That(FindInScene<UnityEngine.EventSystems.EventSystem>(battle).Length,
            Is.EqualTo(1));

        Scene overworld = EditorSceneManager.OpenScene(
            "Assets/Scenes/first_try.unity",
            OpenSceneMode.Single);
        Assert.That(FindInScene<SaveSystemBehaviour>(overworld).Length, Is.EqualTo(1));
        Assert.That(FindInScene<SquadSaveParticipant>(overworld).Length, Is.EqualTo(1));
        Assert.That(FindInScene<OverworldSaveParticipant>(overworld).Length, Is.EqualTo(1));
        Assert.That(FindInScene<OverworldBattleResultReceiver>(overworld).Length, Is.EqualTo(1));
        Assert.That(FindInScene<UnityEngine.EventSystems.EventSystem>(overworld).Length,
            Is.EqualTo(1));
    }

    private LifecycleSetup CreateLifecycleSetup(
        bool initializeCompletion = true,
        Func<string, string, SaveOperationResult> autosave = null,
        Action<string> sceneLoader = null)
    {
        GameObject root = Track(new GameObject("LifecycleTestRoot"));
        GameObject mapObject = NewChild(root.transform, "Map").gameObject;
        MapGenerator generator = mapObject.AddComponent<MapGenerator>();
        generator.autoGenerate = false;
        generator.width = 7;
        generator.height = 7;
        generator.playableCount = 40;
        generator.seed = 817;
        generator.Generate();
        MapRenderer renderer = mapObject.AddComponent<MapRenderer>();
        renderer.autoRender = false;
        renderer.mapGenerator = generator;
        FindTwoPlayableCells(generator, out Vector2Int playerCell, out Vector2Int enemyCell);

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
            new[] { CreateSquad("player", 20f) },
            new[] { CreateSquad("enemy", 10f) });
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
        movement.Configure(generator, renderer, occupancy, turns, true, 0.02f);
        Assert.That(movement.Initialize(), Is.True);
        BattleCommandModeController modes = NewChild(root.transform, "Modes").gameObject
            .AddComponent<BattleCommandModeController>();
        AttackDefinition attack = Track(ScriptableObject.CreateInstance<AttackDefinition>());
        attack.ConfigureDevelopment("test-basic", "Test Basic", 2, 2, 0.5f, null, null);
        BattleCombatRules combatRules = Track(ScriptableObject.CreateInstance<BattleCombatRules>());
        combatRules.ConfigureDevelopment(0.75f, 0.05f, 0.95f, 0.8f, 1);
        BattleAttackService attacks = NewChild(root.transform, "Attacks").gameObject
            .AddComponent<BattleAttackService>();
        attacks.Configure(
            bootstrap,
            turns,
            selection,
            movement,
            attack,
            combatRules,
            true,
            42,
            new FixedBattleRandom());
        Assert.That(attacks.Initialize(), Is.True);

        MovementCommandController movementCommands = NewChild(root.transform, "MoveCommands")
            .gameObject.AddComponent<MovementCommandController>();
        AttackCommandController attackCommands = NewChild(root.transform, "AttackCommands")
            .gameObject.AddComponent<AttackCommandController>();
        BattleHUDController hud = NewChild(root.transform, "HUD").gameObject
            .AddComponent<BattleHUDController>();
        BattleResultPanelView panel = NewChild(root.transform, "ResultPanel").gameObject
            .AddComponent<BattleResultPanelView>();
        PersistentDebuffDefinition debuff = Track(
            ScriptableObject.CreateInstance<PersistentDebuffDefinition>());
        debuff.ConfigureDevelopment(
            "DEV_BattleScar", "Battle Scar", "Resolve -1", -1f);
        PostBattleRules postBattleRules = Track(ScriptableObject.CreateInstance<PostBattleRules>());
        postBattleRules.ConfigureDevelopment(0.2f, debuff);
        BattleCompletionController completion = NewChild(root.transform, "Completion").gameObject
            .AddComponent<BattleCompletionController>();
        completion.Configure(
            bootstrap,
            turns,
            modes,
            movement,
            movementCommands,
            attacks,
            attackCommands,
            hud,
            repository,
            null,
            postBattleRules,
            panel,
            "first_try");
        completion.ConfigureTestSeams(
            outcome =>
            {
                outcome.persistentMutationsApplied = true;
                return BattleResultApplicationResult.Ok();
            },
            autosave ?? ((_, _) => SaveOperationResult.Ok()),
            sceneLoader);
        if (initializeCompletion)
        {
            Assert.That(completion.Initialize(
                "battle-controller-test-" + Guid.NewGuid().ToString("N"),
                "2026-08-04T00:00:00Z"), Is.True);
        }

        return new LifecycleSetup
        {
            Generator = generator,
            Renderer = renderer,
            Bootstrap = bootstrap,
            Repository = repository,
            Player = player,
            Enemy = enemy,
            Turns = turns,
            Modes = modes,
            Movement = movement,
            Attacks = attacks,
            Completion = completion,
            PostBattleRules = postBattleRules
        };
    }

    private static BattleResultBuilder CreateBuilder(LifecycleSetup setup, string battleId)
    {
        BattleResultBuilder builder = new BattleResultBuilder();
        Assert.That(builder.Initialize(
            setup.Bootstrap.SpawnedControllers,
            battleId,
            "encounter-test",
            19,
            "2026-08-04T00:00:00Z"), Is.True);
        return builder;
    }

    private static SquadData CreateSquad(string id, float initiative)
    {
        return new SquadData(
            id,
            new CommanderData
            {
                id = id + "-commander",
                commanderPortraitId = id + "-portrait",
                baseStats = new SquadBaseStats
                {
                    hp = 20,
                    actionPoints = 5,
                    initiative = initiative,
                    strength = 6,
                    dexterity = 5,
                    morale = 20,
                    resolve = 2
                }
            },
            new[]
            {
                new WarriorData
                {
                    id = id + "-warrior-0",
                    maxHP = 7,
                    strength = 2,
                    dexterity = 1
                },
                new WarriorData
                {
                    id = id + "-warrior-1",
                    maxHP = 8,
                    strength = 2,
                    dexterity = 1
                }
            });
    }

    private static void FindTwoPlayableCells(
        MapGenerator generator,
        out Vector2Int first,
        out Vector2Int second)
    {
        List<Vector2Int> playable = new List<Vector2Int>();
        for (int x = 0; x < generator.width; x++)
        for (int y = 0; y < generator.height; y++)
        {
            if (generator.GetIsPlayable(x, y))
                playable.Add(new Vector2Int(x, y));
        }
        Assert.That(playable.Count, Is.GreaterThan(2));
        first = playable[0];
        second = playable[playable.Count - 1];
    }

    private static Vector2Int FindThirdPlayableCell(
        MapGenerator generator,
        Vector2Int first,
        Vector2Int second)
    {
        for (int x = 0; x < generator.width; x++)
        for (int y = 0; y < generator.height; y++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            if (cell != first && cell != second && generator.GetIsPlayable(x, y))
                return cell;
        }
        throw new AssertionException("No third playable cell was found.");
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

    private sealed class FixedBattleRandom : IBattleRandomSource
    {
        public float Next01() => 0f;
    }

    private sealed class FixedPostBattleRandom : IPostBattleRandomSource
    {
        private readonly float value;
        public int CallCount { get; private set; }

        public FixedPostBattleRandom(float configuredValue)
        {
            value = configuredValue;
        }

        public float Next01()
        {
            CallCount++;
            return value;
        }
    }

    private sealed class LifecycleSetup
    {
        public MapGenerator Generator;
        public MapRenderer Renderer;
        public SquadBattleBootstrap Bootstrap;
        public SquadSaveParticipant Repository;
        public SquadBattleController Player;
        public SquadBattleController Enemy;
        public BattleTurnController Turns;
        public BattleCommandModeController Modes;
        public SquadMovementService Movement;
        public BattleAttackService Attacks;
        public BattleCompletionController Completion;
        public PostBattleRules PostBattleRules;
    }
}
