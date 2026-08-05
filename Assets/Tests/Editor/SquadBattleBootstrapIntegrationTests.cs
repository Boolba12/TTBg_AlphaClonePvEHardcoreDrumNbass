using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class SquadBattleBootstrapIntegrationTests
{
    private readonly List<GameObject> cleanup = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        BattleSquadSelectionContext.Clear();
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        }
        cleanup.Clear();
    }

    [Test]
    public void ValidBootstrapCreatesTwoRuntimesFormationsAndInitiativeEntries()
    {
        TestSetup setup = CreateSetup();
        SquadData player = CreateSquad("player", 3, 12);
        SquadData enemy = CreateSquad("enemy", 5, 8);
        BattleSquadSelectionContext.SetSelection(
            new[] { player },
            new[] { enemy });

        bool initialized = setup.Bootstrap.InitializeSquads(
            setup.MapGenerator,
            setup.MapRenderer,
            setup.PlayerCell,
            setup.EnemyCell);

        Assert.That(initialized, Is.True);
        Assert.That(setup.Bootstrap.State, Is.EqualTo(SquadBootstrapState.Initialized));
        Assert.That(setup.Bootstrap.HasBootstrapped, Is.True);
        Assert.That(setup.Bootstrap.SpawnedControllers.Count, Is.EqualTo(2));
        Assert.That(setup.Bootstrap.InitiativeOrder.Entries.Count, Is.EqualTo(2));
        Assert.That(setup.Repository.ActiveRuntimeCount, Is.EqualTo(2));
        Assert.That(BattleSquadSelectionContext.HasSelection, Is.False);
        SquadBattleController playerController =
            setup.Bootstrap.SpawnedControllers.Single(
                controller => controller.Side == BattleSide.Player);
        SquadBattleController enemyController =
            setup.Bootstrap.SpawnedControllers.Single(
                controller => controller.Side == BattleSide.Enemy);
        Assert.That(playerController.ControlType, Is.EqualTo(SquadControlType.Human));
        Assert.That(enemyController.ControlType, Is.EqualTo(SquadControlType.AI));
        Assert.That(
            setup.Bootstrap.SpawnedControllers[0].RegistrationSequence,
            Is.Not.EqualTo(setup.Bootstrap.SpawnedControllers[1].RegistrationSequence));
        Assert.That(
            setup.Bootstrap.SpawnedControllers[0].Runtime,
            Is.Not.SameAs(setup.Bootstrap.SpawnedControllers[1].Runtime));
        Assert.That(
            setup.Bootstrap.SpawnedControllers[0].SquadId,
            Is.Not.EqualTo(setup.Bootstrap.SpawnedControllers[1].SquadId));
        Assert.That(
            setup.Bootstrap.SpawnedControllers[0].GridAnchor.CurrentCell,
            Is.Not.EqualTo(setup.Bootstrap.SpawnedControllers[1].GridAnchor.CurrentCell));
        Assert.That(
            setup.Bootstrap.SpawnedControllers[0].FormationView.CommanderModelCount,
            Is.EqualTo(1));
        Assert.That(
            setup.Bootstrap.SpawnedControllers[0].FormationView.WarriorModelCount,
            Is.EqualTo(3));
        Assert.That(
            setup.Bootstrap.SpawnedControllers[1].FormationView.CommanderModelCount,
            Is.EqualTo(1));
        Assert.That(
            setup.Bootstrap.SpawnedControllers[1].FormationView.WarriorModelCount,
            Is.EqualTo(5));
    }

    [Test]
    public void BootstrapRejectsSquadWithoutCommander()
    {
        SquadData invalid = new SquadData(
            "invalid-player",
            null,
            new[] { Warrior("warrior") });
        AssertInvalidPlayerSquadIsRejected(invalid);
    }

    [Test]
    public void BootstrapRejectsSquadWithoutWarriors()
    {
        SquadData invalid = new SquadData(
            "invalid-player",
            Commander("commander", 10),
            new WarriorData[0]);
        AssertInvalidPlayerSquadIsRejected(invalid);
    }

    [Test]
    public void BootstrapRejectsSquadWithMoreThanEightWarriors()
    {
        SquadData invalid = CreateSquad("invalid-player", 9, 10);
        AssertInvalidPlayerSquadIsRejected(invalid);
    }

    [Test]
    public void RepeatedBootstrapDoesNotCreateDuplicates()
    {
        TestSetup setup = CreateSetup();
        BattleSquadSelectionContext.SetSelection(
            new[] { CreateSquad("player", 2, 12) },
            new[] { CreateSquad("enemy", 2, 8) });

        Assert.That(
            setup.Bootstrap.InitializeSquads(
                setup.MapGenerator,
                setup.MapRenderer,
                setup.PlayerCell,
                setup.EnemyCell),
            Is.True);
        LogAssert.Expect(
            LogType.Warning,
            "SquadBattleBootstrap: ignored repeated initialization while state is Initialized.");

        Assert.That(
            setup.Bootstrap.InitializeSquads(
                setup.MapGenerator,
                setup.MapRenderer,
                setup.PlayerCell,
                setup.EnemyCell),
            Is.False);
        Assert.That(setup.Bootstrap.SpawnedControllers.Count, Is.EqualTo(2));
        Assert.That(setup.Bootstrap.InitiativeOrder.Entries.Count, Is.EqualTo(2));
    }

    [Test]
    public void InitiativeRejectsSecondRegistrationOfSameSquad()
    {
        GameObject controllerObject = Track(new GameObject("Controller"));
        SquadBattleController controller =
            controllerObject.AddComponent<SquadBattleController>();
        Assert.That(controller.Initialize(CreateSquad("single", 1, 5)), Is.True);

        SquadInitiativeOrder order = new SquadInitiativeOrder();
        Assert.That(order.Register(controller), Is.True);
        Assert.That(order.Register(controller), Is.False);
        Assert.That(order.Entries.Count, Is.EqualTo(1));
        order.Clear();
    }

    [Test]
    public void EqualInitiativeUsesRegistrationSequenceAndRepeatedSortIsStable()
    {
        SquadBattleController registeredSecond = CreateInitializedController(
            "registered-second",
            10,
            BattleSide.Enemy,
            SquadControlType.AI,
            1);
        SquadBattleController registeredFirst = CreateInitializedController(
            "registered-first",
            10,
            BattleSide.Player,
            SquadControlType.Human,
            0);

        SquadInitiativeOrder order = new SquadInitiativeOrder();
        Assert.That(order.Register(registeredSecond), Is.True);
        Assert.That(order.Register(registeredFirst), Is.True);
        Assert.That(order.Register(registeredFirst), Is.False);
        Assert.That(order.Entries.Count, Is.EqualTo(2));
        Assert.That(order.Entries[0], Is.SameAs(registeredFirst));

        for (int i = 0; i < 5; i++)
        {
            order.Resort();
            Assert.That(order.Entries[0], Is.SameAs(registeredFirst));
            Assert.That(order.Entries[1], Is.SameAs(registeredSecond));
        }
        order.Clear();
    }

    [Test]
    public void EqualInitiativeWithoutSequenceFallsBackToOrdinalSquadId()
    {
        GameObject zetaObject = Track(new GameObject("same-name"));
        SquadBattleController zeta = zetaObject.AddComponent<SquadBattleController>();
        Assert.That(zeta.Initialize(CreateSquad("zeta", 1, 10)), Is.True);

        GameObject alphaObject = Track(new GameObject("same-name"));
        SquadBattleController alpha = alphaObject.AddComponent<SquadBattleController>();
        Assert.That(alpha.Initialize(CreateSquad("alpha", 1, 10)), Is.True);

        SquadInitiativeOrder order = new SquadInitiativeOrder();
        Assert.That(order.Register(zeta), Is.True);
        Assert.That(order.Register(alpha), Is.True);
        order.Resort();

        Assert.That(order.Entries[0].SquadId, Is.EqualTo("alpha"));
        Assert.That(order.Entries[1].SquadId, Is.EqualTo("zeta"));
        order.Clear();
    }

    [Test]
    public void FailedBootstrapCanRetryAfterSourceRepairWithoutPartialState()
    {
        TestSetup setup = CreateSetup();
        SquadData invalid = new SquadData(
            "invalid-player",
            null,
            new[] { Warrior("invalid-warrior") });
        BattleSquadSelectionContext.SetSelection(
            new[] { invalid },
            new[] { CreateSquad("enemy", 2, 8) });
        LogAssert.Expect(
            LogType.Error,
            new System.Text.RegularExpressions.Regex(
                "SquadBattleBootstrap: bootstrap failed: " +
                "BattleSquadSelectionContext was provided.*"));

        Assert.That(
            setup.Bootstrap.InitializeSquads(
                setup.MapGenerator,
                setup.MapRenderer,
                setup.PlayerCell,
                setup.EnemyCell),
            Is.False);
        Assert.That(setup.Bootstrap.State, Is.EqualTo(SquadBootstrapState.Failed));
        Assert.That(setup.Bootstrap.FailureReason, Is.Not.Empty);
        Assert.That(setup.Bootstrap.SpawnedControllers, Is.Empty);
        Assert.That(setup.Bootstrap.InitiativeOrder.Entries, Is.Empty);
        Assert.That(setup.Repository.ActiveRuntimeCount, Is.Zero);
        Assert.That(setup.Container.childCount, Is.Zero);
        Assert.That(BattleSquadSelectionContext.HasSelection, Is.True);

        BattleSquadSelectionContext.SetSelection(
            new[] { CreateSquad("repaired-player", 2, 12) },
            new[] { CreateSquad("repaired-enemy", 2, 8) });
        Assert.That(setup.Bootstrap.ResetFailedStateForRetry(), Is.True);
        Assert.That(setup.Bootstrap.State, Is.EqualTo(SquadBootstrapState.NotInitialized));
        Assert.That(setup.Bootstrap.FailureReason, Is.Null);
        Assert.That(
            setup.Bootstrap.InitializeSquads(
                setup.MapGenerator,
                setup.MapRenderer,
                setup.PlayerCell,
                setup.EnemyCell),
            Is.True);

        Assert.That(setup.Bootstrap.SpawnedControllers.Count, Is.EqualTo(2));
        Assert.That(setup.Bootstrap.InitiativeOrder.Entries.Count, Is.EqualTo(2));
        Assert.That(setup.Repository.ActiveRuntimeCount, Is.EqualTo(2));
        Assert.That(setup.Container.childCount, Is.EqualTo(2));
        Assert.That(BattleSquadSelectionContext.HasSelection, Is.False);
        Assert.That(setup.Bootstrap.ResetFailedStateForRetry(), Is.False);
    }

    [Test]
    public void OwnershipAndControlDoNotDependOnSquadOrGameObjectNames()
    {
        TestSetup setup = CreateSetup();
        BattleSquadSelectionContext.SetSelection(
            new[] { CreateSquad("enemy-looking-id", 2, 10) },
            new[] { CreateSquad("player-looking-id", 2, 10) });

        Assert.That(
            setup.Bootstrap.InitializeSquads(
                setup.MapGenerator,
                setup.MapRenderer,
                setup.PlayerCell,
                setup.EnemyCell),
            Is.True);

        SquadBattleController player = setup.Bootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Player);
        SquadBattleController enemy = setup.Bootstrap.SpawnedControllers.Single(
            controller => controller.Side == BattleSide.Enemy);
        player.gameObject.name = "Enemy";
        enemy.gameObject.name = "Player";

        Assert.That(player.Side, Is.EqualTo(BattleSide.Player));
        Assert.That(player.ControlType, Is.EqualTo(SquadControlType.Human));
        Assert.That(enemy.Side, Is.EqualTo(BattleSide.Enemy));
        Assert.That(enemy.ControlType, Is.EqualTo(SquadControlType.AI));
        Assert.That(player.Side, Is.Not.EqualTo(enemy.Side));
        LogAssert.Expect(
            LogType.Warning,
            "SquadBattleController: battle context can only be assigned once before initialization.");
        Assert.That(
            player.AssignBattleContext(
                BattleSide.Enemy,
                SquadControlType.AI,
                99),
            Is.False);
        Assert.That(player.Side, Is.EqualTo(BattleSide.Player));
        Assert.That(player.ControlType, Is.EqualTo(SquadControlType.Human));
    }

    [Test]
    public void CombatModeActivatesOnlyCanonicalLegacyPair()
    {
        GameObject mapObject = Track(new GameObject("BattleMapBootstrap"));
        BattleMapBootstrap mapBootstrap = mapObject.AddComponent<BattleMapBootstrap>();
        GameObject canonicalPlayer = Track(new GameObject("CanonicalPlayer"));
        GameObject canonicalEnemy = Track(new GameObject("CanonicalEnemy"));
        GameObject obsoletePlayer = Track(new GameObject("ObsoletePlayer"));
        GameObject obsoleteEnemy = Track(new GameObject("ObsoleteEnemy"));
        mapBootstrap.playerController = canonicalPlayer.AddComponent<PlayerController>();
        mapBootstrap.enemyController = canonicalEnemy.AddComponent<EnemyController>();
        obsoletePlayer.AddComponent<PlayerController>();
        obsoleteEnemy.AddComponent<EnemyController>();
        mapBootstrap.ConfigureLegacyRoots(
            canonicalPlayer,
            canonicalEnemy,
            new[] { obsoletePlayer, obsoleteEnemy });

        SerializedObject serialized = new SerializedObject(mapBootstrap);
        serialized.FindProperty("combatMode").enumValueIndex =
            (int)BattleCombatMode.Squads;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        mapBootstrap.ApplyConfiguredCombatMode();
        Assert.That(canonicalPlayer.activeSelf, Is.False);
        Assert.That(canonicalEnemy.activeSelf, Is.False);
        Assert.That(obsoletePlayer.activeSelf, Is.False);
        Assert.That(obsoleteEnemy.activeSelf, Is.False);

        serialized.Update();
        serialized.FindProperty("combatMode").enumValueIndex =
            (int)BattleCombatMode.LegacyUnits;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        mapBootstrap.ApplyConfiguredCombatMode();
        Assert.That(canonicalPlayer.activeSelf, Is.True);
        Assert.That(canonicalEnemy.activeSelf, Is.True);
        Assert.That(obsoletePlayer.activeSelf, Is.False);
        Assert.That(obsoleteEnemy.activeSelf, Is.False);
    }

    [Test]
    public void RawAlphaSceneHasExplicitSquadCompositionAndSaveWiring()
    {
        const string scenePath = "Assets/Scenes/Raw_Alpha_BattleMode.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedByTest = !scene.IsValid() || !scene.isLoaded;
        if (openedByTest)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            BattleMapBootstrap mapBootstrap = FindInScene<BattleMapBootstrap>(scene);
            SquadBattleBootstrap squadBootstrap = FindInScene<SquadBattleBootstrap>(scene);
            BattleContextMenuUI setupUI = FindInScene<BattleContextMenuUI>(scene);
            SquadSaveParticipant squadSave = FindInScene<SquadSaveParticipant>(scene);
            CommanderPortraitSaveParticipant portraits =
                FindInScene<CommanderPortraitSaveParticipant>(scene);
            SaveSystemBehaviour saveSystem = FindInScene<SaveSystemBehaviour>(scene);

            Assert.That(mapBootstrap, Is.Not.Null);
            Assert.That(mapBootstrap.CombatMode, Is.EqualTo(BattleCombatMode.Squads));
            Assert.That(squadBootstrap, Is.Not.Null);
            Assert.That(setupUI, Is.Not.Null);
            Assert.That(squadSave, Is.Not.Null);
            Assert.That(portraits, Is.Not.Null);
            Assert.That(saveSystem, Is.Not.Null);

            SerializedObject serializedMap = new SerializedObject(mapBootstrap);
            Assert.That(
                serializedMap.FindProperty("squadBattleBootstrap").objectReferenceValue,
                Is.SameAs(squadBootstrap));
            Assert.That(
                serializedMap.FindProperty("battleSetupUI").objectReferenceValue,
                Is.SameAs(setupUI));
            Assert.That(
                serializedMap.FindProperty("enableDevelopmentSquadAutoConfirm").boolValue,
                Is.True);
            Assert.That(
                serializedMap.FindProperty("legacyPlayerRoot").objectReferenceValue,
                Is.SameAs(mapBootstrap.playerController.gameObject));
            Assert.That(
                serializedMap.FindProperty("legacyEnemyRoot").objectReferenceValue,
                Is.SameAs(mapBootstrap.enemyController.gameObject));
            Assert.That(
                serializedMap.FindProperty("obsoleteLegacyCombatRoots").arraySize,
                Is.EqualTo(2));

            SerializedObject serializedSquads = new SerializedObject(squadBootstrap);
            SquadBattleController prefab =
                serializedSquads.FindProperty("squadBattlePrefab").objectReferenceValue
                    as SquadBattleController;
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<SquadGridAnchor>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<SquadFormationView>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<PlayerController>(), Is.Null);
            Assert.That(prefab.GetComponent<EnemyController>(), Is.Null);
            Assert.That(prefab.GetComponent<UnitStats>(), Is.Null);

            SerializedObject serializedSave = new SerializedObject(saveSystem);
            SerializedProperty participants = serializedSave.FindProperty("participants");
            bool hasSquads = false;
            bool hasPortraits = false;
            for (int i = 0; i < participants.arraySize; i++)
            {
                Object participant =
                    participants.GetArrayElementAtIndex(i).objectReferenceValue;
                hasSquads |= participant == squadSave;
                hasPortraits |= participant == portraits;
            }

            Assert.That(hasSquads, Is.True);
            Assert.That(hasPortraits, Is.True);
        }
        finally
        {
            if (openedByTest)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void SceneSmokeDoesNotCallInternalConfirmationOrSquadInitialization()
    {
        string source = File.ReadAllText(
            "Assets/Scripts/Squads/Editor/SquadBattleSceneSmokeRunner.cs");
        Assert.That(source, Does.Not.Contain(".ConfirmBattleSetup("));
        Assert.That(source, Does.Not.Contain("BattleSetupContext.Confirm("));
        Assert.That(source, Does.Not.Contain("BattleSetupContext.IsConfirmed ="));
        Assert.That(source, Does.Not.Contain(".InitializeSquads("));
    }

    private void AssertInvalidPlayerSquadIsRejected(SquadData invalid)
    {
        TestSetup setup = CreateSetup();
        BattleSquadSelectionContext.SetSelection(
            new[] { invalid },
            new[] { CreateSquad("enemy", 2, 8) });
        LogAssert.Expect(
            LogType.Error,
            new System.Text.RegularExpressions.Regex(
                "SquadBattleBootstrap: bootstrap failed: " +
                "BattleSquadSelectionContext was provided.*"));

        bool initialized = setup.Bootstrap.InitializeSquads(
            setup.MapGenerator,
            setup.MapRenderer,
            setup.PlayerCell,
            setup.EnemyCell);

        Assert.That(initialized, Is.False);
        Assert.That(setup.Bootstrap.State, Is.EqualTo(SquadBootstrapState.Failed));
        Assert.That(setup.Bootstrap.FailureReason, Is.Not.Empty);
        Assert.That(setup.Bootstrap.SpawnedControllers, Is.Empty);
        Assert.That(setup.Bootstrap.InitiativeOrder.Entries, Is.Empty);
        Assert.That(setup.Repository.ActiveRuntimeCount, Is.Zero);
        Assert.That(BattleSquadSelectionContext.HasSelection, Is.True);
    }

    private TestSetup CreateSetup()
    {
        GameObject root = Track(new GameObject("TestRoot"));

        GameObject mapObject = new GameObject("Map");
        mapObject.transform.SetParent(root.transform);
        MapGenerator generator = mapObject.AddComponent<MapGenerator>();
        generator.autoGenerate = false;
        generator.width = 6;
        generator.height = 6;
        generator.playableCount = 30;
        generator.seed = 42;
        generator.Generate();
        MapRenderer renderer = mapObject.AddComponent<MapRenderer>();
        renderer.autoRender = false;
        renderer.mapGenerator = generator;

        GameObject placeholder = Track(new GameObject("Placeholder"));
        GameObject template = Track(new GameObject("SquadTemplate"));
        SquadGridAnchor anchor = template.AddComponent<SquadGridAnchor>();
        SquadFormationView formation = template.AddComponent<SquadFormationView>();
        SquadBattleController controller = template.AddComponent<SquadBattleController>();
        Transform models = NewChild(template.transform, "Models");
        Transform commanderSlot = NewChild(models, "CommanderSlot");
        List<Transform> warriorSlots = new List<Transform>();
        for (int i = 0; i < SquadData.MaximumWarriors; i++)
            warriorSlots.Add(NewChild(models, $"WarriorSlot_{i}"));
        formation.Configure(
            models,
            commanderSlot,
            warriorSlots,
            placeholder,
            placeholder);
        controller.Configure(anchor, formation);

        GameObject bootstrapObject = new GameObject("Bootstrap");
        bootstrapObject.transform.SetParent(root.transform);
        Transform container = NewChild(bootstrapObject.transform, "Spawned");
        SquadSaveParticipant repository =
            bootstrapObject.AddComponent<SquadSaveParticipant>();
        SquadBattleBootstrap bootstrap =
            bootstrapObject.AddComponent<SquadBattleBootstrap>();
        bootstrap.Configure(controller, container, repository, false, null, null, false);

        FindTwoPlayableCells(generator, out Vector2Int playerCell, out Vector2Int enemyCell);
        return new TestSetup
        {
            Bootstrap = bootstrap,
            Repository = repository,
            Container = container,
            MapGenerator = generator,
            MapRenderer = renderer,
            PlayerCell = playerCell,
            EnemyCell = enemyCell
        };
    }

    private SquadBattleController CreateInitializedController(
        string squadId,
        float initiative,
        BattleSide side,
        SquadControlType controlType,
        int sequence)
    {
        GameObject controllerObject = Track(new GameObject("same-name"));
        SquadBattleController controller =
            controllerObject.AddComponent<SquadBattleController>();
        Assert.That(
            controller.AssignBattleContext(side, controlType, sequence),
            Is.True);
        Assert.That(
            controller.Initialize(CreateSquad(squadId, 1, initiative)),
            Is.True);
        return controller;
    }

    private static void FindTwoPlayableCells(
        MapGenerator generator,
        out Vector2Int first,
        out Vector2Int second)
    {
        first = default;
        second = default;
        bool foundFirst = false;
        for (int x = 0; x < generator.width; x++)
        {
            for (int y = 0; y < generator.height; y++)
            {
                if (!generator.GetIsPlayable(x, y))
                    continue;

                if (!foundFirst)
                {
                    first = new Vector2Int(x, y);
                    foundFirst = true;
                }
                else
                {
                    second = new Vector2Int(x, y);
                    return;
                }
            }
        }

        Assert.Fail("Generated test map did not contain two playable cells.");
    }

    private static SquadData CreateSquad(
        string id,
        int warriorCount,
        float initiative)
    {
        List<WarriorData> warriors = new List<WarriorData>();
        for (int i = 0; i < warriorCount; i++)
            warriors.Add(Warrior($"{id}-warrior-{i}"));
        return new SquadData(
            id,
            Commander($"{id}-commander", initiative),
            warriors);
    }

    private static CommanderData Commander(string id, float initiative)
    {
        return new CommanderData
        {
            id = id,
            baseStats = new SquadBaseStats
            {
                hp = 10,
                actionPoints = 4,
                initiative = initiative,
                strength = 5,
                dexterity = 5,
                morale = 10
            }
        };
    }

    private static WarriorData Warrior(string id)
    {
        return new WarriorData
        {
            id = id,
            maxHP = 5,
            strength = 1,
            dexterity = 1
        };
    }

    private GameObject Track(GameObject gameObject)
    {
        cleanup.Add(gameObject);
        return gameObject;
    }

    private static Transform NewChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private sealed class TestSetup
    {
        public SquadBattleBootstrap Bootstrap;
        public SquadSaveParticipant Repository;
        public Transform Container;
        public MapGenerator MapGenerator;
        public MapRenderer MapRenderer;
        public Vector2Int PlayerCell;
        public Vector2Int EnemyCell;
    }
}
