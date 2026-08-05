using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class BattleUIEditModeTests
{
    private readonly List<GameObject> objectsToDestroy = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject target in objectsToDestroy)
        {
            if (target != null)
                Object.DestroyImmediate(target);
        }
        objectsToDestroy.Clear();
    }

    [Test]
    public void FormatterSupportsHpApPercentMultiplierAndModifiers()
    {
        Assert.That(UIStatFormatter.FormatCurrentMaximum(12, 20), Is.EqualTo("12 / 20"));
        Assert.That(UIStatFormatter.FormatCurrentMaximum(3, 4), Is.EqualTo("3 / 4"));
        Assert.That(UIStatFormatter.FormatPercentage(0.25f), Is.EqualTo("25%"));
        Assert.That(UIStatFormatter.FormatMultiplier(1.5f, 1), Is.EqualTo("x1.5"));
        Assert.That(UIStatFormatter.FormatModifier(4), Is.EqualTo("+4"));
        Assert.That(UIStatFormatter.FormatModifier(-2), Is.EqualTo("-2"));
    }

    [Test]
    public void StatusModelReadsLivingAndMaximumWarriorsFromRuntime()
    {
        SquadBattleRuntime runtime = new SquadBattleRuntime(CreateSquad("player", 2, 12));
        BattleSquadStatusModel initial = BattleSquadStatusModel.FromRuntime(runtime, null);

        Assert.That(initial.LivingWarriors, Is.EqualTo(2));
        Assert.That(initial.MaximumWarriors, Is.EqualTo(2));
        Assert.That(initial.CurrentHealth, Is.EqualTo(runtime.State.CurrentSquadHP));
        Assert.That(initial.MaximumHealth, Is.EqualTo(runtime.Stats.MaxHP));

        runtime.ApplyDamage(8, SquadDamageDistribution.SingleTarget);
        BattleSquadStatusModel damaged = BattleSquadStatusModel.FromRuntime(runtime, null);
        Assert.That(damaged.LivingWarriors, Is.EqualTo(1));
        Assert.That(damaged.MaximumWarriors, Is.EqualTo(2));
    }

    [Test]
    public void StatusPresenterRebindDoesNotDuplicateAndUnbindStopsUpdates()
    {
        BattleSquadStatusView view = Track(new GameObject("StatusView"))
            .AddComponent<BattleSquadStatusView>();
        BattleSquadStatusPresenter presenter = Track(new GameObject("StatusPresenter"))
            .AddComponent<BattleSquadStatusPresenter>();
        SquadBattleController controller = Track(new GameObject("Controller"))
            .AddComponent<SquadBattleController>();
        Assert.That(controller.Initialize(CreateSquad("player", 2, 10)), Is.True);
        presenter.Configure(view, LoadPortraitDatabase());

        LogAssert.Expect(
            LogType.Warning,
            "BattleSquadStatusPresenter: portrait '<empty>' is unavailable; " +
            "the configured UI development fallback will be shown.");
        Assert.That(presenter.Bind(controller), Is.True);
        Assert.That(presenter.Bind(controller), Is.True);
        int beforeActionPointChange = view.RenderCount;
        Assert.That(controller.Runtime.TrySpendActionPoints(1), Is.True);
        Assert.That(view.RenderCount, Is.EqualTo(beforeActionPointChange + 1));

        presenter.Unbind();
        int afterUnbind = view.RenderCount;
        Assert.That(controller.Runtime.TrySpendActionPoints(1), Is.True);
        Assert.That(view.RenderCount, Is.EqualTo(afterUnbind));
    }

    [Test]
    public void MissingRuntimeProducesControlledEmptyState()
    {
        BattleSquadStatusView view = Track(new GameObject("StatusView"))
            .AddComponent<BattleSquadStatusView>();
        BattleSquadStatusPresenter presenter = Track(new GameObject("StatusPresenter"))
            .AddComponent<BattleSquadStatusPresenter>();
        SquadBattleController controller = Track(new GameObject("UninitializedController"))
            .AddComponent<SquadBattleController>();
        presenter.Configure(view, LoadPortraitDatabase());

        Assert.That(presenter.Bind(controller), Is.False);
        Assert.That(view.HasData, Is.False);
        Assert.That(view.EmptyStateCount, Is.EqualTo(1));
    }

    [Test]
    public void MissingPortraitUsesConfiguredFallbackWithoutException()
    {
        CommanderPortraitDatabase database = LoadPortraitDatabase();
        CommanderPortraitService service = new CommanderPortraitService(database, 7);

        Assert.That(
            () => service.GetDisplaySprite("portrait-that-does-not-exist"),
            Throws.Nothing);
        Assert.That(
            service.GetDisplaySprite("portrait-that-does-not-exist"),
            Is.SameAs(database.FallbackPortrait));
        Assert.That(database.FallbackPortrait, Is.Not.Null);
    }

    [Test]
    public void InitiativeModelsPreserveProductionOrderAndCount()
    {
        SquadBattleController first = CreateController("first", 8, BattleSide.Player, 0);
        SquadBattleController second = CreateController("second", 20, BattleSide.Enemy, 1);
        SquadInitiativeOrder order = new SquadInitiativeOrder();
        Assert.That(order.Register(first), Is.True);
        Assert.That(order.Register(second), Is.True);

        string[] productionOrder = order.Entries.Select(entry => entry.SquadId).ToArray();
        List<InitiativeEntryModel> models = InitiativeQueuePresenter.BuildModels(
            order,
            LoadPortraitDatabase(),
            first.SquadId);

        Assert.That(models.Count, Is.EqualTo(2));
        Assert.That(models.Select(model => model.SquadId), Is.EqualTo(productionOrder));
        Assert.That(models.Single(model => model.SquadId == first.SquadId).IsSelected, Is.True);
        order.Clear();
    }

    [Test]
    public void InitiativePresenterRebindHasOneRuntimeSubscription()
    {
        InitiativeQueueView view = Track(new GameObject("InitiativeView"))
            .AddComponent<InitiativeQueueView>();
        InitiativeQueuePresenter presenter = Track(new GameObject("InitiativePresenter"))
            .AddComponent<InitiativeQueuePresenter>();
        SquadBattleController controller = CreateController("player", 10, BattleSide.Player, 0);
        SquadInitiativeOrder order = new SquadInitiativeOrder();
        Assert.That(order.Register(controller), Is.True);
        presenter.Configure(view, LoadPortraitDatabase());

        presenter.Bind(order, controller.SquadId);
        presenter.Bind(order, controller.SquadId);
        int beforeRefresh = view.RenderCount;
        controller.Runtime.RecalculateStats();
        Assert.That(view.RenderCount, Is.EqualTo(beforeRefresh + 1));

        presenter.Unbind();
        int afterUnbind = view.RenderCount;
        controller.Runtime.RecalculateStats();
        Assert.That(view.RenderCount, Is.EqualTo(afterUnbind));
        order.Clear();
    }

    [Test]
    public void BattleHudPrefabHasResponsiveFoundationAndRequiredLayers()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/UI/Prefabs/Battle/BattleHUD.prefab");
        Assert.That(prefab, Is.Not.Null);
        CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
        Assert.That(scaler, Is.Not.Null);
        Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
        Assert.That(prefab.GetComponent<BattleHUDController>(), Is.Not.Null);
        Assert.That(prefab.transform.Find("HUDLayer/TopBar"), Is.Not.Null);
        Assert.That(prefab.transform.Find("HUDLayer/TopRight_MinimapContainer"), Is.Not.Null);
        Assert.That(prefab.transform.Find("HUDLayer/SelectedSquadPanel"), Is.Not.Null);
        Assert.That(prefab.transform.Find("HUDLayer/BottomActionBar"), Is.Not.Null);
        Assert.That(prefab.transform.Find("TooltipLayer"), Is.Not.Null);
        Assert.That(prefab.transform.Find("ModalLayer"), Is.Not.Null);
    }

    [TestCase(1920, 1080)]
    [TestCase(2560, 1440)]
    [TestCase(1366, 768)]
    public void BattleHudAnchorZonesDoNotOverlapAtSupportedResolutions(int width, int height)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/UI/Prefabs/Battle/BattleHUD.prefab");
        Transform hudLayer = prefab.transform.Find("HUDLayer");
        Rect top = AnchorRect(hudLayer.Find("TopBar") as RectTransform, width, height);
        Rect minimap = AnchorRect(
            hudLayer.Find("TopRight_MinimapContainer") as RectTransform,
            width,
            height);
        Rect selected = AnchorRect(
            hudLayer.Find("SelectedSquadPanel") as RectTransform,
            width,
            height);
        Rect actions = AnchorRect(
            hudLayer.Find("BottomActionBar") as RectTransform,
            width,
            height);

        Assert.That(top.Overlaps(minimap), Is.False);
        Assert.That(top.Overlaps(selected), Is.False);
        Assert.That(selected.Overlaps(actions), Is.False);
        Assert.That(minimap.Overlaps(actions), Is.False);
        Assert.That(top.xMin, Is.GreaterThanOrEqualTo(0));
        Assert.That(minimap.xMax, Is.LessThanOrEqualTo(width));
        Assert.That(actions.yMin, Is.GreaterThanOrEqualTo(0));
        Assert.That(top.yMax, Is.LessThanOrEqualTo(height));
    }

    private SquadBattleController CreateController(
        string id,
        int initiative,
        BattleSide side,
        int sequence)
    {
        SquadBattleController controller = Track(new GameObject(id))
            .AddComponent<SquadBattleController>();
        Assert.That(
            controller.AssignBattleContext(
                side,
                side == BattleSide.Player ? SquadControlType.Human : SquadControlType.AI,
                sequence),
            Is.True);
        Assert.That(controller.Initialize(CreateSquad(id, 1, initiative)), Is.True);
        return controller;
    }

    private static SquadData CreateSquad(string id, int warriorCount, int initiative)
    {
        List<WarriorData> warriors = new List<WarriorData>();
        for (int i = 0; i < warriorCount; i++)
        {
            warriors.Add(new WarriorData
            {
                id = $"{id}-warrior-{i}",
                maxHP = 8,
                strength = 2,
                dexterity = 1
            });
        }

        return new SquadData(
            id,
            new CommanderData
            {
                id = $"{id}-commander",
                commanderPortraitId = string.Empty,
                baseStats = new SquadBaseStats
                {
                    hp = 20,
                    actionPoints = 4,
                    morale = 20,
                    initiative = initiative
                }
            },
            warriors);
    }

    private static CommanderPortraitDatabase LoadPortraitDatabase()
    {
        CommanderPortraitDatabase database =
            AssetDatabase.LoadAssetAtPath<CommanderPortraitDatabase>(
                "Assets/Art/CommanderPortraits/CommanderPortraitDatabase.asset");
        Assert.That(database, Is.Not.Null);
        return database;
    }

    private static Rect AnchorRect(RectTransform rect, float width, float height)
    {
        Assert.That(rect, Is.Not.Null);
        Vector2 minimum = Vector2.Scale(rect.anchorMin, new Vector2(width, height));
        Vector2 maximum = Vector2.Scale(rect.anchorMax, new Vector2(width, height));
        return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
    }

    private GameObject Track(GameObject target)
    {
        objectsToDestroy.Add(target);
        return target;
    }
}
