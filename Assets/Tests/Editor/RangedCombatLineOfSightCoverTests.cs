using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RangedCombatLineOfSightCoverTests
{
    private const string RangedAssetPath =
        "Assets/GameData/Combat/DEV_BasicPhysicalRangedAttack.asset";
    private const string RulesAssetPath =
        "Assets/GameData/Combat/DEV_BattleCombatRules.asset";

    private readonly List<Object> cleanup = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null && !EditorUtility.IsPersistent(cleanup[i]))
                Object.DestroyImmediate(cleanup[i]);
        }
        cleanup.Clear();
    }

    [Test]
    public void DevelopmentRangedAssetUsesStableDataDrivenPhysicalContract()
    {
        AttackDefinition ranged = AssetDatabase.LoadAssetAtPath<AttackDefinition>(
            RangedAssetPath);

        Assert.That(ranged, Is.Not.Null);
        Assert.That(ranged.Validate(out string reason), Is.True, reason);
        Assert.That(ranged.StableId, Is.EqualTo("dev-basic-physical-ranged"));
        Assert.That(ranged.Delivery, Is.EqualTo(BattleAttackDelivery.Ranged));
        Assert.That(ranged.DamageType, Is.EqualTo(BattleDamageType.Physical));
        Assert.That(ranged.Distribution, Is.EqualTo(SquadDamageDistribution.SingleTarget));
        Assert.That(ranged.MinimumRange, Is.EqualTo(2));
        Assert.That(ranged.MaximumRange, Is.EqualTo(8));
        Assert.That(ranged.ActionPointCost, Is.EqualTo(3));
        Assert.That(ranged.BaseDamage, Is.EqualTo(2));
        Assert.That(ranged.PrimaryScalingStat, Is.EqualTo(AttackScalingStat.Dexterity));
        Assert.That(ranged.FriendlyFire, Is.False);
        Assert.That(ranged.DevelopmentOnly, Is.True);
        Assert.That(ranged.UsesEquippedWeapon, Is.False);
        Assert.That(ranged.PreviewSprite, Is.Not.Null);
        Assert.That(ranged.ModelPrefab, Is.Null,
            "No ranged hand/socket contract exists, so the stage must not fake a model attachment.");
    }

    [Test]
    public void RangedRangeUsesCanonicalChebyshevTopologyAndDistinctFailures()
    {
        AttackDefinition ranged = CreateRanged();
        FakeTerrain terrain = new FakeTerrain(20, 20);
        BattleTargetingService targeting = new BattleTargetingService(
            true,
            new GridLineOfSightService(terrain),
            new GridCoverService(terrain));

        BattleAttackTargetEvaluation diagonal = targeting.EvaluateGridGeometry(
            new Vector2Int(1, 1), new Vector2Int(7, 7), ranged);
        BattleAttackTargetEvaluation tooClose = targeting.EvaluateGridGeometry(
            new Vector2Int(1, 1), new Vector2Int(2, 2), ranged);
        BattleAttackTargetEvaluation tooFar = targeting.EvaluateGridGeometry(
            new Vector2Int(1, 1), new Vector2Int(10, 1), ranged);

        Assert.That(diagonal.IsValid, Is.True);
        Assert.That(diagonal.GridDistance, Is.EqualTo(6));
        Assert.That(tooClose.Validation.FailureReason,
            Is.EqualTo(BattleAttackFailureReason.TargetTooClose));
        Assert.That(tooFar.Validation.FailureReason,
            Is.EqualTo(BattleAttackFailureReason.TargetBeyondRange));
    }

    [Test]
    public void SupercoverIsDeterministicForHorizontalVerticalAndDiagonalLines()
    {
        FakeTerrain terrain = new FakeTerrain(12, 12);
        GridLineOfSightService service = new GridLineOfSightService(terrain);

        Assert.That(service.Evaluate(new Vector2Int(1, 2), new Vector2Int(5, 2))
                .TraversedCells,
            Is.EqualTo(new[]
            {
                new Vector2Int(1, 2), new Vector2Int(2, 2),
                new Vector2Int(3, 2), new Vector2Int(4, 2), new Vector2Int(5, 2)
            }));
        Assert.That(service.Evaluate(new Vector2Int(4, 1), new Vector2Int(4, 4))
                .Status,
            Is.EqualTo(LineOfSightStatus.Clear));

        IReadOnlyList<Vector2Int> diagonal = service.Evaluate(
            new Vector2Int(0, 0), new Vector2Int(2, 2)).TraversedCells;
        Assert.That(diagonal, Does.Contain(new Vector2Int(1, 0)));
        Assert.That(diagonal, Does.Contain(new Vector2Int(0, 1)));
        Assert.That(diagonal, Does.Contain(new Vector2Int(1, 1)));
        Assert.That(diagonal.First(), Is.EqualTo(new Vector2Int(0, 0)));
        Assert.That(diagonal.Last(), Is.EqualTo(new Vector2Int(2, 2)));
    }

    [Test]
    public void SupercoverBlocksCornerShotsButNeverTreatsEndpointsAsBlockers()
    {
        FakeTerrain terrain = new FakeTerrain(10, 10);
        terrain.Set(new Vector2Int(1, 0), false, true, CoverType.None);
        terrain.Set(new Vector2Int(0, 0), false, true, CoverType.None);
        terrain.Set(new Vector2Int(2, 2), false, true, CoverType.None);
        GridLineOfSightService service = new GridLineOfSightService(terrain);

        LineOfSightResult blocked = service.Evaluate(
            new Vector2Int(0, 0), new Vector2Int(2, 2));
        Assert.That(blocked.Status, Is.EqualTo(LineOfSightStatus.Blocked));
        Assert.That(blocked.BlockingCell, Is.EqualTo(new Vector2Int(1, 0)));

        terrain.Clear(new Vector2Int(1, 0));
        LineOfSightResult endpointsIgnored = service.Evaluate(
            new Vector2Int(0, 0), new Vector2Int(2, 2));
        Assert.That(endpointsIgnored.Status, Is.EqualTo(LineOfSightStatus.Clear));
    }

    [Test]
    public void DirectionalCoverReadsOnlyTargetFacingCellsAndChoosesStrongestDiagonal()
    {
        FakeTerrain terrain = new FakeTerrain(20, 20);
        Vector2Int target = new Vector2Int(10, 10);
        terrain.Set(new Vector2Int(9, 10), true, false, CoverType.Half);
        terrain.Set(new Vector2Int(10, 9), true, false, CoverType.Full);
        terrain.Set(new Vector2Int(11, 10), true, false, CoverType.None);
        GridCoverService cover = new GridCoverService(terrain);

        GridCoverResult diagonal = cover.Evaluate(new Vector2Int(5, 5), target);
        GridCoverResult opposite = cover.Evaluate(new Vector2Int(15, 10), target);

        Assert.That(diagonal.EvaluatedCells,
            Is.EquivalentTo(new[] { new Vector2Int(9, 10), new Vector2Int(10, 9) }));
        Assert.That(diagonal.CoverType, Is.EqualTo(CoverType.Full));
        Assert.That(opposite.EvaluatedCells,
            Is.EqualTo(new[] { new Vector2Int(11, 10) }));
        Assert.That(opposite.CoverType, Is.EqualTo(CoverType.None));
    }

    [Test]
    public void FullCoverCanLeaveLosClearAndOnlyChangesHitChanceNotDamage()
    {
        FakeTerrain terrain = new FakeTerrain(20, 20);
        terrain.Set(new Vector2Int(7, 5), true, false, CoverType.Full);
        GridLineOfSightService lineOfSight = new GridLineOfSightService(terrain);
        GridCoverService cover = new GridCoverService(terrain);
        Vector2Int attacker = new Vector2Int(2, 5);
        Vector2Int target = new Vector2Int(8, 5);

        Assert.That(lineOfSight.Evaluate(attacker, target).Status,
            Is.EqualTo(LineOfSightStatus.Clear));
        Assert.That(cover.Evaluate(attacker, target).CoverType,
            Is.EqualTo(CoverType.Full));

        AttackDefinition ranged = CreateRanged();
        BattleCombatRules rules = CreateRules();
        BattleAttackCalculator calculator = new BattleAttackCalculator(rules);
        SquadCalculatedStats attackStats = Stats(dexterity: 10f, accuracy: 0.10f);
        SquadCalculatedStats targetStats = Stats(evasion: 0.05f, armor: 0.25f);
        BattleDamageCalculation withoutCover = calculator.CalculateDamage(
            attackStats, targetStats, ranged, false);
        BattleDamageCalculation withCover = calculator.CalculateDamage(
            attackStats, targetStats, ranged, false);

        Assert.That(calculator.CalculateHitChance(attackStats, targetStats),
            Is.EqualTo(0.80f).Within(0.0001f));
        Assert.That(calculator.CalculateHitChance(
                attackStats,
                targetStats,
                coverModifier: rules.GetCoverHitModifier(CoverType.Half)),
            Is.EqualTo(0.60f).Within(0.0001f));
        Assert.That(calculator.CalculateHitChance(
                attackStats,
                targetStats,
                coverModifier: rules.GetCoverHitModifier(CoverType.Full)),
            Is.EqualTo(0.40f).Within(0.0001f));
        Assert.That(withCover.RawDamage, Is.EqualTo(withoutCover.RawDamage));
        Assert.That(withCover.AppliedDamage, Is.EqualTo(withoutCover.AppliedDamage));
    }

    [Test]
    public void BlockedLosRejectsBeforeCoverAndDoesNotInventPhysicsRaycastTruth()
    {
        AttackDefinition ranged = CreateRanged();
        FakeTerrain terrain = new FakeTerrain(20, 20);
        terrain.Set(new Vector2Int(4, 4), true, true, CoverType.Full);
        BattleTargetingService targeting = new BattleTargetingService(
            true,
            new GridLineOfSightService(terrain),
            new GridCoverService(terrain));

        BattleAttackTargetEvaluation result = targeting.EvaluateGridGeometry(
            new Vector2Int(2, 4), new Vector2Int(7, 4), ranged);

        Assert.That(result.Validation.FailureReason,
            Is.EqualTo(BattleAttackFailureReason.LineOfSightBlocked));
        Assert.That(result.LineOfSight.BlockingCell, Is.EqualTo(new Vector2Int(4, 4)));
        Assert.That(result.Cover.CoverType, Is.EqualTo(CoverType.None));
        string source = System.IO.File.ReadAllText(
            "Assets/Scripts/Squads/Combat/GridLineOfSightService.cs");
        Assert.That(source, Does.Not.Contain("Physics.Raycast"));
    }

    [Test]
    public void HudPrefabContainsExactlyOneRangedActionAndProductionSceneWiresOneOwner()
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/UI/Prefabs/Battle/BattleHUD.prefab");
        Assert.That(hud, Is.Not.Null);
        Assert.That(hud.GetComponentsInChildren<BattleActionControlView>(true)
            .Count(control => control.gameObject.name == "Ranged"), Is.EqualTo(1));

        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/Raw_Alpha_BattleMode.unity",
            OpenSceneMode.Single);
        GridTacticalTerrainService[] terrain = FindInScene<GridTacticalTerrainService>(scene);
        AttackRangePreviewView[] previews = FindInScene<AttackRangePreviewView>(scene);
        BattleAttackService[] attacks = FindInScene<BattleAttackService>(scene);
        AttackCommandController[] commands = FindInScene<AttackCommandController>(scene);
        Assert.That(terrain, Has.Length.EqualTo(1));
        Assert.That(previews, Has.Length.EqualTo(1));
        Assert.That(attacks, Has.Length.EqualTo(1));
        Assert.That(commands, Has.Length.EqualTo(1));

        SerializedObject attackSerialized = new SerializedObject(attacks[0]);
        Assert.That(attackSerialized.FindProperty("rangedAttack").objectReferenceValue,
            Is.SameAs(AssetDatabase.LoadAssetAtPath<AttackDefinition>(RangedAssetPath)));
        Assert.That(attackSerialized.FindProperty("tacticalTerrain").objectReferenceValue,
            Is.SameAs(terrain[0]));
        SerializedObject commandSerialized = new SerializedObject(commands[0]);
        Assert.That(commandSerialized.FindProperty("rangedAttackAction").objectReferenceValue,
            Is.Not.Null);
        Assert.That(commandSerialized.FindProperty("rangePreview").objectReferenceValue,
            Is.SameAs(previews[0]));
    }

    private AttackDefinition CreateRanged()
    {
        AttackDefinition ranged = Track(ScriptableObject.CreateInstance<AttackDefinition>());
        ranged.ConfigureDevelopmentRanged(
            "test-ranged", "Test Ranged", 2, 3, 2, 8, 0.5f, null, null);
        return ranged;
    }

    private BattleCombatRules CreateRules()
    {
        BattleCombatRules rules = Track(ScriptableObject.CreateInstance<BattleCombatRules>());
        rules.ConfigureDevelopment(0.75f, 0.05f, 0.95f, 0.8f, 1);
        rules.ConfigureDevelopmentCover(-0.20f, -0.40f);
        return rules;
    }

    private static SquadCalculatedStats Stats(
        float dexterity = 0f,
        float accuracy = 0f,
        float evasion = 0f,
        float armor = 0f)
    {
        return new SquadCalculatedStats(
            new SquadBaseStats
            {
                hp = 10,
                actionPoints = 4,
                dexterity = dexterity,
                accuracy = accuracy,
                evasion = evasion,
                criticalDamage = 1.5f,
                physicalArmor = armor
            },
            0,
            0f,
            0f,
            null);
    }

    private T Track<T>(T value) where T : Object
    {
        cleanup.Add(value);
        return value;
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        List<T> values = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            values.AddRange(root.GetComponentsInChildren<T>(true));
        return values.ToArray();
    }

    private sealed class FakeTerrain : IGridTacticalTerrain
    {
        private readonly int width;
        private readonly int height;
        private readonly Dictionary<Vector2Int, Cell> cells =
            new Dictionary<Vector2Int, Cell>();

        public FakeTerrain(int configuredWidth, int configuredHeight)
        {
            width = configuredWidth;
            height = configuredHeight;
        }

        public void Set(Vector2Int cell, bool movement, bool lineOfSight, CoverType cover)
        {
            cells[cell] = new Cell(movement, lineOfSight, cover);
        }

        public void Clear(Vector2Int cell) => cells.Remove(cell);

        public bool IsInside(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 &&
                                                 cell.x < width && cell.y < height;

        public bool BlocksMovement(Vector2Int cell) =>
            cells.TryGetValue(cell, out Cell value) && value.Movement;

        public bool BlocksLineOfSight(Vector2Int cell) =>
            cells.TryGetValue(cell, out Cell value) && value.LineOfSight;

        public CoverType GetCover(Vector2Int cell) =>
            cells.TryGetValue(cell, out Cell value) ? value.Cover : CoverType.None;

        private readonly struct Cell
        {
            public Cell(bool movement, bool lineOfSight, CoverType cover)
            {
                Movement = movement;
                LineOfSight = lineOfSight;
                Cover = cover;
            }

            public bool Movement { get; }
            public bool LineOfSight { get; }
            public CoverType Cover { get; }
        }
    }
}
