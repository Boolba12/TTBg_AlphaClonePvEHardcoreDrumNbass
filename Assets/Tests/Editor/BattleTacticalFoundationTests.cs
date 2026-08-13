using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BattleTacticalFoundationTests
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
    public void SharedPathfinderProducesRepeatableShortestPath()
    {
        TacticalSetup setup = CreateSetup();
        FindReachableDestination(setup, setup.Player, 2, out Vector2Int destination);

        Assert.That(GridPathfinder.TryBuildPath(
            setup.Generator,
            setup.Player.GridAnchor.CurrentCell,
            destination,
            true,
            null,
            out List<Vector2Int> first), Is.True);
        Assert.That(GridPathfinder.TryBuildPath(
            setup.Generator,
            setup.Player.GridAnchor.CurrentCell,
            destination,
            true,
            null,
            out List<Vector2Int> second), Is.True);

        Assert.That(second, Is.EqualTo(first));
        Assert.That(first.First(), Is.EqualTo(setup.Player.GridAnchor.CurrentCell));
        Assert.That(first.Last(), Is.EqualTo(destination));
        Assert.That(first.Count - 1, Is.EqualTo(2));
    }

    [Test]
    public void OccupancyReservationCommitCancelAndDefeatLifecycleAreAtomic()
    {
        TacticalSetup setup = CreateSetup();
        GridOccupancyService occupancy = Track(new GameObject("Occupancy"))
            .AddComponent<GridOccupancyService>();
        Assert.That(occupancy.Initialize(setup.Bootstrap.SpawnedControllers), Is.True);
        Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(2));
        Assert.That(occupancy.ReservationCount, Is.Zero);

        FindReachableDestination(setup, setup.Player, 1, out Vector2Int destination);
        Assert.That(occupancy.TryReserve(setup.Player, destination), Is.True);
        Assert.That(occupancy.ReservationCount, Is.EqualTo(1));
        Assert.That(occupancy.TryReserve(setup.Player, destination), Is.False);
        occupancy.CancelReservation(setup.Player);
        Assert.That(occupancy.ReservationCount, Is.Zero);

        Assert.That(occupancy.TryReserve(setup.Player, destination), Is.True);
        Assert.That(occupancy.CanCommitMove(setup.Player, destination), Is.True);
        Assert.That(occupancy.TryCommitMove(setup.Player, destination), Is.True);
        Assert.That(setup.Player.GridAnchor.CommitVisualArrival(destination), Is.True);
        Assert.That(occupancy.TryGetOccupant(
            destination, out SquadBattleController occupant), Is.True);
        Assert.That(occupant, Is.SameAs(setup.Player));
        Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(2));
        Assert.That(occupancy.ReservationCount, Is.Zero);

        setup.Player.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);
        Assert.That(setup.Player.CanAct, Is.False);
        Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(1));
        occupancy.Clear();
        Assert.That(occupancy.IsInitialized, Is.False);
        Assert.That(occupancy.OccupiedCellCount, Is.Zero);
    }

    [Test]
    public void OccupancyRejectsDuplicateInitialCellsWithoutPartialRegistration()
    {
        TacticalSetup setup = CreateSetup();
        SquadBattleController duplicate = CreatePlacedController(
            setup,
            "duplicate",
            setup.Player.GridAnchor.CurrentCell,
            BattleSide.Player,
            SquadControlType.AI,
            2,
            5);
        GridOccupancyService occupancy = Track(new GameObject("Occupancy"))
            .AddComponent<GridOccupancyService>();

        Assert.That(occupancy.Initialize(
            new[] { setup.Player, setup.Enemy, duplicate }), Is.False);
        Assert.That(occupancy.IsInitialized, Is.False);
        Assert.That(occupancy.OccupiedCellCount, Is.Zero);
        Assert.That(occupancy.ReservationCount, Is.Zero);
    }

    [Test]
    public void SelectionAcceptsLivingPlayerTargetRejectsEnemyAndClearsOnDefeat()
    {
        TacticalSetup setup = CreateSetup();
        BattleSquadSelectionController selection = Track(new GameObject("Selection"))
            .AddComponent<BattleSquadSelectionController>();
        selection.Configure(setup.Bootstrap, null);
        Assert.That(selection.Initialize(), Is.True);
        int changes = 0;
        selection.OnSelectedSquadChanged += _ => changes++;

        Assert.That(selection.TrySelectTarget(setup.Enemy.SelectionTarget), Is.False);
        Assert.That(selection.SelectedSquad, Is.Null);
        Assert.That(selection.TrySelectTarget(setup.Player.SelectionTarget), Is.True);
        Assert.That(selection.SelectedSquad, Is.SameAs(setup.Player));
        Assert.That(setup.Player.SelectionTarget.SelectionView.IsSelected, Is.True);
        Assert.That(selection.TrySelectTarget(setup.Player.SelectionTarget), Is.True);
        Assert.That(changes, Is.EqualTo(1));

        setup.Player.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);
        Assert.That(selection.SelectedSquad, Is.Null);
        Assert.That(setup.Player.SelectionTarget.SelectionView.IsSelected, Is.False);
        Assert.That(changes, Is.EqualTo(2));
    }

    [Test]
    public void TurnControllerUsesInitiativeRefreshesApAndAdvancesRoundOnce()
    {
        TacticalSetup setup = CreateSetup();
        BattleTurnController turns = Track(new GameObject("Turns"))
            .AddComponent<BattleTurnController>();
        turns.Configure(setup.Bootstrap, false, 0f);
        Assert.That(setup.Player.Runtime.TrySpendActionPoints(2), Is.True);

        Assert.That(turns.StartBattle(), Is.True);
        Assert.That(turns.StartBattle(), Is.False);
        Assert.That(turns.CurrentRound, Is.EqualTo(1));
        Assert.That(turns.ActiveSquad, Is.SameAs(setup.Player));
        Assert.That(setup.Player.Runtime.State.currentActionPoints,
            Is.EqualTo(setup.Player.Runtime.Stats.ActionPoints));
        Assert.That(setup.Player.Runtime.State.turnCompleted, Is.False);

        Assert.That(turns.EndCurrentTurn(), Is.True);
        Assert.That(setup.Player.Runtime.State.turnCompleted, Is.True);
        Assert.That(turns.ActiveSquad, Is.SameAs(setup.Enemy));
        Assert.That(turns.CurrentRound, Is.EqualTo(1));
        Assert.That(turns.EndCurrentTurn(), Is.True);
        Assert.That(turns.ActiveSquad, Is.SameAs(setup.Player));
        Assert.That(turns.CurrentRound, Is.EqualTo(2));
    }

    [Test]
    public void TacticalCameraFocusesFirstActiveSquadAndEachSubsequentTurnOnce()
    {
        TacticalSetup setup = CreateSetup();
        BattleTurnController turns = Track(new GameObject("Turns"))
            .AddComponent<BattleTurnController>();
        turns.Configure(setup.Bootstrap, false, 0f);
        Assert.That(setup.Renderer.TryGetGeneratedWorldBounds(
            out Bounds bounds, true), Is.True);

        GameObject cameraObject = Track(new GameObject("TacticalCamera"));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.aspect = 1f;
        camera.fieldOfView = 32f;
        camera.transform.position = bounds.center + Vector3.up * 5f;
        camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        TacticalCameraController tacticalCamera =
            cameraObject.AddComponent<TacticalCameraController>();
        tacticalCamera.Configure(
            camera,
            setup.Generator,
            setup.Renderer,
            turns);
        Assert.That(tacticalCamera.Initialize(), Is.True);
        Assert.That(tacticalCamera.TurnFocusCount, Is.Zero);

        Assert.That(turns.StartBattle(), Is.True);
        Assert.That(turns.ActiveSquad, Is.SameAs(setup.Player));
        Assert.That(tacticalCamera.LastTurnFocusSquadId, Is.EqualTo(setup.Player.SquadId));
        Assert.That(tacticalCamera.TurnFocusCount, Is.EqualTo(1));

        Vector3 focusedPlayerPosition = camera.transform.position;
        Assert.That(tacticalCamera.PanFromKeyboard(Vector2.right, 0.2f), Is.True);
        Assert.That(camera.transform.position, Is.Not.EqualTo(focusedPlayerPosition));
        Assert.That(tacticalCamera.TurnFocusCount, Is.EqualTo(1),
            "Manual pan must not start a continuous auto-follow routine.");

        Assert.That(turns.EndCurrentTurn(), Is.True);
        Assert.That(turns.ActiveSquad, Is.SameAs(setup.Enemy));
        Assert.That(tacticalCamera.LastTurnFocusSquadId, Is.EqualTo(setup.Enemy.SquadId));
        Assert.That(tacticalCamera.TurnFocusCount, Is.EqualTo(2));
    }

    [Test]
    public void DefeatedActiveEntryIsRemovedAndSkippedByTurnController()
    {
        TacticalSetup setup = CreateSetup();
        BattleTurnController turns = Track(new GameObject("Turns"))
            .AddComponent<BattleTurnController>();
        turns.Configure(setup.Bootstrap, false, 0f);
        Assert.That(turns.StartBattle(), Is.True);
        Assert.That(turns.ActiveSquad, Is.SameAs(setup.Player));

        setup.Player.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);

        Assert.That(setup.Bootstrap.InitiativeOrder.Entries.Count, Is.EqualTo(1));
        Assert.That(setup.Bootstrap.InitiativeOrder.Entries[0], Is.SameAs(setup.Enemy));
        Assert.That(turns.ActiveSquad, Is.SameAs(setup.Enemy));
    }

    [Test]
    public void MovementPlanningRequiresActiveHumanAndChargesOneApPerCell()
    {
        TacticalSetup setup = CreateSetup();
        GridOccupancyService occupancy = Track(new GameObject("Occupancy"))
            .AddComponent<GridOccupancyService>();
        Assert.That(occupancy.Initialize(setup.Bootstrap.SpawnedControllers), Is.True);
        BattleTurnController turns = Track(new GameObject("Turns"))
            .AddComponent<BattleTurnController>();
        turns.Configure(setup.Bootstrap, false, 0f);
        Assert.That(turns.StartBattle(), Is.True);
        SquadMovementService movement = Track(new GameObject("Movement"))
            .AddComponent<SquadMovementService>();
        movement.Configure(
            setup.Generator,
            setup.Renderer,
            occupancy,
            turns,
            true,
            0.02f);
        Assert.That(movement.Initialize(), Is.True);

        FindReachableDestination(setup, setup.Player, 2, out Vector2Int destination);
        Vector3 beforePreview = setup.Player.transform.position;
        Assert.That(movement.TryBuildPlan(
            setup.Player, destination, out SquadMovementPlan plan), Is.True);
        Assert.That(plan.ActionPointCost, Is.EqualTo(plan.Path.Count - 1));
        Assert.That(plan.ActionPointCost, Is.EqualTo(2));
        Assert.That(setup.Player.transform.position, Is.EqualTo(beforePreview),
            "Preview planning must not move the squad root.");

        FindReachableDestination(setup, setup.Player, 1, out Vector2Int oneStep);
        Assert.That(movement.TryBuildPlan(
            setup.Player, oneStep, out SquadMovementPlan oneStepPlan), Is.True);
        Assert.That(oneStepPlan.ActionPointCost, Is.EqualTo(1));

        Assert.That(movement.TryBuildPlan(
            setup.Player,
            setup.Enemy.GridAnchor.CurrentCell,
            out SquadMovementPlan occupiedPlan), Is.False);
        Assert.That(occupiedPlan.IsValid, Is.False);
        Assert.That(movement.TryBuildPlan(
            setup.Enemy,
            setup.Player.GridAnchor.CurrentCell,
            out SquadMovementPlan aiPlan), Is.False);
        Assert.That(aiPlan.UnavailableReason, Does.Contain("Human"));

        FindNonPlayableCell(setup.Generator, out Vector2Int blockedCell);
        Assert.That(movement.TryBuildPlan(
            setup.Player, blockedCell, out SquadMovementPlan blockedPlan), Is.False);
        Assert.That(blockedPlan.IsValid, Is.False);

        int pointsToSpend = setup.Player.Runtime.State.currentActionPoints - 1;
        Assert.That(setup.Player.Runtime.TrySpendActionPoints(pointsToSpend), Is.True);
        Assert.That(movement.TryBuildPlan(
            setup.Player, destination, out SquadMovementPlan expensivePlan), Is.False);
        Assert.That(expensivePlan.UnavailableReason, Does.Contain("needs"));
        setup.Player.Runtime.BeginTurn();

        Assert.That(turns.EndCurrentTurn(), Is.True);
        Assert.That(movement.TryBuildPlan(
            setup.Player, destination, out SquadMovementPlan inactivePlan), Is.False);
        Assert.That(inactivePlan.UnavailableReason, Does.Contain("active"));
    }

    [Test]
    public void InitiativePresentationKeepsSelectedAndActiveAsSeparateFlags()
    {
        TacticalSetup setup = CreateSetup();
        List<InitiativeEntryModel> models = InitiativeQueuePresenter.BuildModels(
            setup.Bootstrap.InitiativeOrder,
            null,
            setup.Player.SquadId,
            setup.Enemy.SquadId);

        InitiativeEntryModel player = models.Single(
            model => model.SquadId == setup.Player.SquadId);
        InitiativeEntryModel enemy = models.Single(
            model => model.SquadId == setup.Enemy.SquadId);
        Assert.That(player.IsSelected, Is.True);
        Assert.That(player.IsActive, Is.False);
        Assert.That(player.ControlType, Is.EqualTo(SquadControlType.Human));
        Assert.That(enemy.IsSelected, Is.False);
        Assert.That(enemy.IsActive, Is.True);
        Assert.That(enemy.ControlType, Is.EqualTo(SquadControlType.AI));
    }

    [Test]
    public void RawSceneOwnsOneExplicitTacticalCompositionAndPrefabSelectionTarget()
    {
        const string scenePath = "Assets/Scenes/Raw_Alpha_BattleMode.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            Assert.That(FindAllInScene<SquadBattleTacticalBootstrap>(scene).Length,
                Is.EqualTo(1));
            Assert.That(FindAllInScene<GridOccupancyService>(scene).Length, Is.EqualTo(1));
            Assert.That(FindAllInScene<BattleSquadSelectionController>(scene).Length,
                Is.EqualTo(1));
            Assert.That(FindAllInScene<BattleTurnController>(scene).Length, Is.EqualTo(1));
            Assert.That(FindAllInScene<SquadMovementService>(scene).Length, Is.EqualTo(1));
            Assert.That(FindAllInScene<MovementCommandController>(scene).Length,
                Is.EqualTo(1));
            Assert.That(FindAllInScene<BattleCommandModeController>(scene).Length,
                Is.EqualTo(1));
            Assert.That(FindAllInScene<BattleAttackService>(scene).Length,
                Is.EqualTo(1));
            Assert.That(FindAllInScene<AttackCommandController>(scene).Length,
                Is.EqualTo(1));

            SquadBattleTacticalBootstrap tactical =
                FindAllInScene<SquadBattleTacticalBootstrap>(scene).Single();
            SerializedObject serialized = new SerializedObject(tactical);
            Assert.That(serialized.FindProperty("squadBootstrap").objectReferenceValue,
                Is.Not.Null);
            Assert.That(serialized.FindProperty("occupancy").objectReferenceValue,
                Is.Not.Null);
            Assert.That(serialized.FindProperty("selection").objectReferenceValue,
                Is.Not.Null);
            Assert.That(serialized.FindProperty("turns").objectReferenceValue,
                Is.Not.Null);
            Assert.That(serialized.FindProperty("movement").objectReferenceValue,
                Is.Not.Null);
            Assert.That(serialized.FindProperty("commands").objectReferenceValue,
                Is.Not.Null);
            Assert.That(serialized.FindProperty("commandMode").objectReferenceValue,
                Is.Not.Null);
            Assert.That(serialized.FindProperty("attackService").objectReferenceValue,
                Is.Not.Null);
            Assert.That(serialized.FindProperty("attackCommands").objectReferenceValue,
                Is.Not.Null);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    Assert.That(
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                            transform.gameObject),
                        Is.Zero,
                        $"Missing script on scene object '{transform.name}'.");
                }
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        GameObject squadPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Squads/SquadBattle.prefab");
        Assert.That(squadPrefab.GetComponent<BoxCollider>(), Is.Not.Null);
        Assert.That(squadPrefab.GetComponent<SquadSelectionTarget>(), Is.Not.Null);
        Assert.That(squadPrefab.GetComponent<SquadSelectionView>(), Is.Not.Null);
        Assert.That(squadPrefab.GetComponent<SquadBattleController>().SelectionTarget,
            Is.Not.Null);
        Assert.That(squadPrefab.GetComponent<SquadAttackTarget>(), Is.Not.Null);
        Assert.That(squadPrefab.GetComponent<SquadAttackTargetView>(), Is.Not.Null);
        Assert.That(squadPrefab.GetComponent<SquadBattleController>().AttackTarget,
            Is.Not.Null);
        foreach (Transform transform in squadPrefab.GetComponentsInChildren<Transform>(true))
        {
            Assert.That(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject),
                Is.Zero,
                $"Missing script on SquadBattle prefab object '{transform.name}'.");
        }
        GameObject squadInstance = Track(Object.Instantiate(squadPrefab));
        Transform visualChild = squadInstance.transform
            .GetComponentsInChildren<Transform>(true)
            .First(child => child != squadInstance.transform);
        Assert.That(visualChild.GetComponentInParent<SquadSelectionTarget>(),
            Is.SameAs(squadInstance.GetComponent<SquadSelectionTarget>()));
        Assert.That(visualChild.GetComponentInParent<SquadAttackTarget>(),
            Is.SameAs(squadInstance.GetComponent<SquadAttackTarget>()));

        string selectionSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Squads/Tactical/BattleSquadSelectionController.cs");
        string commandSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Squads/Tactical/MovementCommandController.cs");
        string attackCommandSource = System.IO.File.ReadAllText(
            "Assets/Scripts/Squads/Tactical/AttackCommandController.cs");
        Assert.That(selectionSource, Does.Contain("EventSystem.current.IsPointerOverGameObject()"));
        Assert.That(commandSource, Does.Contain("EventSystem.current.IsPointerOverGameObject()"));
        Assert.That(attackCommandSource,
            Does.Contain("EventSystem.current.IsPointerOverGameObject()"));
    }

    [Test]
    public void AuditedWeaponPreviewsAndModelsUsePresentationOnlyImportContract()
    {
        ItemPresentationCatalog catalog =
            AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(
                "Assets/UI/Presentation/DevelopmentItemPresentationCatalog.asset");
        ItemPresentationRecord[] weapons = catalog.Entries
            .Where(entry => entry.Category == ItemPresentationCategory.Weapon)
            .ToArray();
        Assert.That(weapons.Length, Is.EqualTo(12));
        foreach (ItemPresentationRecord weapon in weapons)
        {
            Assert.That(weapon.PreviewSprite, Is.Not.Null);
            Assert.That(weapon.ModelPrefab, Is.Not.Null);
            Assert.That(weapon.IsPlaceholder, Is.False);
            string previewPath = AssetDatabase.GetAssetPath(weapon.PreviewSprite);
            TextureImporter previewImporter = AssetImporter.GetAtPath(previewPath) as TextureImporter;
            Assert.That(previewImporter.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(previewImporter.mipmapEnabled, Is.False);
            string modelPath = AssetDatabase.GetAssetPath(weapon.ModelPrefab);
            ModelImporter modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            Assert.That(modelImporter.importAnimation, Is.False);
        }

        SquadFormationPresentation player =
            AssetDatabase.LoadAssetAtPath<SquadFormationPresentation>(
                "Assets/Prefabs/Squads/Presentation/DevelopmentPlayerFormation.asset");
        SquadFormationPresentation enemy =
            AssetDatabase.LoadAssetAtPath<SquadFormationPresentation>(
                "Assets/Prefabs/Squads/Presentation/DevelopmentEnemyFormation.asset");
        Assert.That(player, Is.Not.Null);
        Assert.That(enemy, Is.Not.Null);
        Assert.That(player.IsValid && enemy.IsValid, Is.True);
        Assert.That(player.CommanderPrefab.name,
            Is.EqualTo("DevelopmentSquadMemberPlaceholder"));
        Assert.That(enemy.WarriorPrefab.name,
            Is.EqualTo("DevelopmentSquadMemberPlaceholder"));
    }

    private TacticalSetup CreateSetup()
    {
        GameObject root = Track(new GameObject("TacticalTestRoot"));
        GameObject mapObject = NewChild(root.transform, "Map").gameObject;
        MapGenerator generator = mapObject.AddComponent<MapGenerator>();
        generator.autoGenerate = false;
        generator.width = 7;
        generator.height = 7;
        generator.playableCount = 40;
        generator.seed = 2718;
        generator.Generate();
        MapRenderer renderer = mapObject.AddComponent<MapRenderer>();
        renderer.autoRender = false;
        renderer.mapGenerator = generator;

        GameObject placeholder = NewChild(root.transform, "Placeholder").gameObject;
        GameObject template = NewChild(root.transform, "SquadTemplate").gameObject;
        SquadGridAnchor anchor = template.AddComponent<SquadGridAnchor>();
        SquadFormationView formation = template.AddComponent<SquadFormationView>();
        LineRenderer ring = template.AddComponent<LineRenderer>();
        SquadSelectionView selectionView = template.AddComponent<SquadSelectionView>();
        selectionView.Configure(null, ring);
        SquadSelectionTarget target = template.AddComponent<SquadSelectionTarget>();
        SquadBattleController controller = template.AddComponent<SquadBattleController>();
        target.Configure(controller, selectionView);
        Transform models = NewChild(template.transform, "Models");
        Transform commanderSlot = NewChild(models, "CommanderSlot");
        List<Transform> warriorSlots = new List<Transform>();
        for (int i = 0; i < SquadData.MaximumWarriors; i++)
            warriorSlots.Add(NewChild(models, $"WarriorSlot_{i}"));
        formation.Configure(models, commanderSlot, warriorSlots, placeholder, placeholder);
        controller.Configure(anchor, formation, target);

        GameObject bootstrapObject = NewChild(root.transform, "Bootstrap").gameObject;
        Transform container = NewChild(bootstrapObject.transform, "Spawned");
        SquadSaveParticipant repository =
            bootstrapObject.AddComponent<SquadSaveParticipant>();
        SquadBattleBootstrap bootstrap =
            bootstrapObject.AddComponent<SquadBattleBootstrap>();
        bootstrap.Configure(controller, container, repository, false, null, null, false);
        FindTwoPlayableCells(generator, out Vector2Int playerCell, out Vector2Int enemyCell);
        BattleSquadSelectionContext.SetSelection(
            new[] { CreateSquad("player", 20) },
            new[] { CreateSquad("enemy", 10) });
        Assert.That(bootstrap.InitializeSquads(
            generator, renderer, playerCell, enemyCell), Is.True);

        return new TacticalSetup
        {
            Bootstrap = bootstrap,
            Generator = generator,
            Renderer = renderer,
            Player = bootstrap.SpawnedControllers.Single(
                candidate => candidate.Side == BattleSide.Player),
            Enemy = bootstrap.SpawnedControllers.Single(
                candidate => candidate.Side == BattleSide.Enemy)
        };
    }

    private SquadBattleController CreatePlacedController(
        TacticalSetup setup,
        string id,
        Vector2Int cell,
        BattleSide side,
        SquadControlType control,
        int sequence,
        float initiative)
    {
        GameObject root = Track(new GameObject(id));
        SquadGridAnchor anchor = root.AddComponent<SquadGridAnchor>();
        SquadBattleController controller = root.AddComponent<SquadBattleController>();
        controller.Configure(anchor, null);
        Assert.That(controller.InitializeAtCell(
            CreateSquad(id, initiative),
            null,
            setup.Generator,
            setup.Renderer,
            cell,
            side,
            control,
            sequence), Is.True);
        return controller;
    }

    private static void FindReachableDestination(
        TacticalSetup setup,
        SquadBattleController controller,
        int requiredCost,
        out Vector2Int destination)
    {
        for (int x = 0; x < setup.Generator.width; x++)
        {
            for (int y = 0; y < setup.Generator.height; y++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                if (candidate == setup.Enemy.GridAnchor.CurrentCell)
                    continue;
                if (GridPathfinder.TryBuildPath(
                        setup.Generator,
                        controller.GridAnchor.CurrentCell,
                        candidate,
                        true,
                        null,
                        out List<Vector2Int> path) &&
                    path.Count - 1 == requiredCost)
                {
                    destination = candidate;
                    return;
                }
            }
        }
        Assert.Fail($"No reachable destination with cost {requiredCost} was found.");
        destination = default;
    }

    private static void FindTwoPlayableCells(
        MapGenerator generator,
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
        Assert.That(playable.Count, Is.GreaterThan(2));
        first = playable[0];
        second = playable[playable.Count - 1];
    }

    private static void FindNonPlayableCell(
        MapGenerator generator,
        out Vector2Int blocked)
    {
        for (int x = 0; x < generator.width; x++)
        {
            for (int y = 0; y < generator.height; y++)
            {
                if (!generator.GetIsPlayable(x, y))
                {
                    blocked = new Vector2Int(x, y);
                    return;
                }
            }
        }
        Assert.Fail("Generated tactical test map did not contain a blocked cell.");
        blocked = default;
    }

    private static SquadData CreateSquad(string id, float initiative)
    {
        return new SquadData(
            id,
            new CommanderData
            {
                id = id + "-commander",
                baseStats = new SquadBaseStats
                {
                    hp = 12,
                    actionPoints = 5,
                    initiative = initiative,
                    strength = 4,
                    dexterity = 4,
                    morale = 10
                }
            },
            new[]
            {
                new WarriorData
                {
                    id = id + "-warrior",
                    maxHP = 8,
                    strength = 2,
                    dexterity = 2
                }
            });
    }

    private GameObject Track(GameObject target)
    {
        cleanup.Add(target);
        return target;
    }

    private static Transform NewChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        List<T> found = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            found.AddRange(root.GetComponentsInChildren<T>(true));
        return found.ToArray();
    }

    private sealed class TacticalSetup
    {
        public SquadBattleBootstrap Bootstrap;
        public MapGenerator Generator;
        public MapRenderer Renderer;
        public SquadBattleController Player;
        public SquadBattleController Enemy;
    }
}
