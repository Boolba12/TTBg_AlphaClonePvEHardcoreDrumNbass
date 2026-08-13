using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BattleMinimapAndMapScaleTests
{
    private readonly List<GameObject> cleanup = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        }
        cleanup.Clear();
    }

    [Test]
    public void ProductionSceneUses32x32AndOwnsOneCameraAndMinimapContract()
    {
        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/Raw_Alpha_BattleMode.unity",
            OpenSceneMode.Additive);
        try
        {
            BattleMapBootstrap bootstrap = FindAllInScene<BattleMapBootstrap>(scene).Single();
            Assert.That(bootstrap.overrideBattleSize, Is.True);
            Assert.That(bootstrap.battleWidth, Is.EqualTo(32));
            Assert.That(bootstrap.battleHeight, Is.EqualTo(32));
            Assert.That(bootstrap.battlePlayableCount, Is.EqualTo(720));
            Assert.That(bootstrap.mapGenerator.Width, Is.EqualTo(32));
            Assert.That(bootstrap.mapGenerator.Height, Is.EqualTo(32));
            Assert.That(FindAllInScene<TacticalCameraController>(scene).Length, Is.EqualTo(1));
            Assert.That(FindAllInScene<TacticalMinimapController>(scene).Length, Is.EqualTo(1));
            Assert.That(FindAllInScene<UnityEngine.EventSystems.EventSystem>(scene).Length,
                Is.EqualTo(1));

            TacticalMinimapController minimap =
                FindAllInScene<TacticalMinimapController>(scene).Single();
            TacticalCameraController camera =
                FindAllInScene<TacticalCameraController>(scene).Single();
            SerializedObject serialized = new SerializedObject(minimap);
            Assert.That(serialized.FindProperty("mapGenerator").objectReferenceValue,
                Is.SameAs(bootstrap.mapGenerator));
            Assert.That(serialized.FindProperty("mapRenderer").objectReferenceValue,
                Is.SameAs(bootstrap.mapRenderer));
            Assert.That(serialized.FindProperty("squadBootstrap").objectReferenceValue,
                Is.Not.Null);
            Assert.That(serialized.FindProperty("cameraController").objectReferenceValue,
                Is.Not.Null);
            SerializedObject serializedCamera = new SerializedObject(camera);
            Assert.That(serializedCamera.FindProperty("turnController").objectReferenceValue,
                Is.SameAs(FindAllInScene<BattleTurnController>(scene).Single()),
                "Production tactical camera must use the explicit turn owner reference.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ProductionMinimapGraphicOwnsCanvasRendererAndRaycastsWithoutException()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/UI/Prefabs/Battle/BattleHUD.prefab");
        Assert.That(prefab, Is.Not.Null);
        Transform prefabMapContent = prefab.GetComponentsInChildren<Transform>(true)
            .Single(transform => transform.name == "MapContent");
        MinimapGridGraphic prefabGraphic =
            prefabMapContent.GetComponent<MinimapGridGraphic>();
        Assert.That(prefabGraphic, Is.Not.Null);
        Assert.That(prefabGraphic.raycastTarget, Is.True);
        Assert.That(prefabMapContent.GetComponents<CanvasRenderer>().Length, Is.EqualTo(1),
            "A raycastable Graphic requires exactly one CanvasRenderer.");

        GameObject instance = Track(Object.Instantiate(prefab));
        GraphicRaycaster raycaster = instance.GetComponentInChildren<GraphicRaycaster>(true);
        Assert.That(raycaster, Is.Not.Null);
        GameObject eventObject = Track(new GameObject("RegressionEventSystem"));
        EventSystem eventSystem = eventObject.AddComponent<EventSystem>();
        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            position = Vector2.zero
        };
        List<RaycastResult> results = new List<RaycastResult>();
        Canvas.ForceUpdateCanvases();
        Assert.DoesNotThrow(() => raycaster.Raycast(pointer, results));
    }

    [Test]
    public void Rectangular48x32MappingRoundTripsAndRejectsInteriorHole()
    {
        Bounds bounds = new Bounds(new Vector3(24f, 0f, 16f), new Vector3(48f, 0f, 32f));
        MinimapCoordinateMapper mapper = new MinimapCoordinateMapper(
            48,
            32,
            bounds,
            (x, y) => x != 17 || y != 11,
            cell => new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f));

        Vector2Int original = new Vector2Int(43, 27);
        Vector2 normalized = mapper.GridToNormalized(original);
        Assert.That(mapper.TryNormalizedToGrid(normalized, out Vector2Int roundTrip), Is.True);
        Assert.That(roundTrip, Is.EqualTo(original));
        Vector2 worldCenter = mapper.WorldToNormalized(new Vector3(24f, 0f, 16f));
        Assert.That(worldCenter.x, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(worldCenter.y, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(mapper.MapAspect, Is.EqualTo(1.5f).Within(0.0001f));

        Vector2 hole = mapper.GridToNormalized(new Vector2Int(17, 11));
        Assert.That(mapper.TryNormalizedToGrid(hole, out _, true), Is.False);
        Assert.That(mapper.TryNormalizedToGrid(hole, out Vector2Int raw, false), Is.True);
        Assert.That(raw, Is.EqualTo(new Vector2Int(17, 11)));
    }

    [Test]
    public void AspectFitPreservesRectangularMapShape()
    {
        Rect fitWide = MinimapCoordinateMapper.CalculateAspectFitRect(
            new Rect(0f, 0f, 300f, 300f),
            1.5f);
        Assert.That(fitWide.width, Is.EqualTo(300f).Within(0.001f));
        Assert.That(fitWide.height, Is.EqualTo(200f).Within(0.001f));
        Assert.That(fitWide.y, Is.EqualTo(50f).Within(0.001f));

        Rect fitTall = MinimapCoordinateMapper.CalculateAspectFitRect(
            new Rect(0f, 0f, 500f, 200f),
            1f);
        Assert.That(fitTall.width, Is.EqualTo(200f).Within(0.001f));
        Assert.That(fitTall.x, Is.EqualTo(150f).Within(0.001f));
    }

    [Test]
    public void Generated32x32MapSupportsLongDiagonalPathWithinBounds()
    {
        MapSetup setup = CreateMap(32, 32, 720, 20260809);
        List<Vector2Int> playable = EnumeratePlayable(setup.Generator);
        Vector2Int start = setup.Generator.GetStartCell();
        Vector2Int target = playable
            .OrderByDescending(cell => Mathf.Abs(cell.x - start.x) + Mathf.Abs(cell.y - start.y))
            .First();

        Assert.That(setup.Generator.PotentialCellCount, Is.EqualTo(1024));
        Assert.That(setup.Generator.PlayableCellCount, Is.EqualTo(playable.Count));
        Assert.That(playable.Count, Is.GreaterThan(0));
        Assert.That(GridPathfinder.TryBuildPath(
            setup.Generator,
            start,
            target,
            true,
            null,
            out List<Vector2Int> path), Is.True);
        Assert.That(path.Count, Is.GreaterThan(2));
        Assert.That(path.All(cell => cell.x >= 0 && cell.x < 32 && cell.y >= 0 && cell.y < 32),
            Is.True);
        Assert.That(path.All(cell => setup.Generator.GetIsPlayable(cell.x, cell.y)), Is.True);
        Assert.That(setup.Generator.GetIsPlayable(-1, 0), Is.False);
        Assert.That(setup.Generator.GetIsPlayable(32, 31), Is.False);
    }

    [Test]
    public void DirectWorldLookupDoesNotSnapInteriorHoleToNeighbour()
    {
        MapSetup setup = CreateMap(12, 8, 50, 8181);
        Vector2Int blocked = FindCell(setup.Generator, false);
        Vector3 world = setup.Renderer.GetCellWorldCenter(blocked);
        Assert.That(setup.Renderer.TryGetGridCell(world, out Vector2Int raw, false), Is.True);
        Assert.That(raw, Is.EqualTo(blocked));
        Assert.That(setup.Renderer.TryGetGridCell(world, out _, true), Is.False);
    }

    [Test]
    public void TacticalCameraUsesGeneratedBoundsAndEmitsViewportForPanAndZoom()
    {
        MapSetup setup = CreateMap(32, 24, 600, 5656);
        Assert.That(setup.Renderer.TryGetGeneratedWorldBounds(
            out Bounds generatedBounds, true), Is.True);
        GameObject cameraObject = Track(new GameObject("TacticalCamera"));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.aspect = 1.25f;
        camera.fieldOfView = 36f;
        camera.transform.position = generatedBounds.center + new Vector3(0f, 8f, -3f);
        camera.transform.rotation = Quaternion.Euler(68f, 0f, 0f);
        TacticalCameraController controller = cameraObject.AddComponent<TacticalCameraController>();
        controller.Configure(camera, setup.Generator, setup.Renderer);

        Assert.That(controller.Initialize(), Is.True);
        Assert.That(controller.MapBounds.size.x, Is.GreaterThan(0f));
        Assert.That(controller.MapBounds.size.z, Is.GreaterThan(0f));
        Assert.That(controller.CurrentFootprint.Count, Is.EqualTo(4));
        int viewportBefore = controller.ViewportChangeCount;
        Assert.That(controller.ZoomBy(1f), Is.True);
        Assert.That(controller.ViewportChangeCount, Is.GreaterThan(viewportBefore));
        Vector2Int playable = setup.Generator.GetCentralPlayableCell();
        Assert.That(controller.FocusGrid(playable), Is.True);
        Vector3 worldFocus = setup.Renderer.GetCellWorldCenter(playable);
        Vector3 footprintCenter = controller.CurrentFootprint.Aggregate(
            Vector3.zero,
            (sum, point) => sum + point) / controller.CurrentFootprint.Count;
        Assert.That(footprintCenter.x, Is.EqualTo(worldFocus.x).Within(0.01f));
        Assert.That(footprintCenter.z, Is.EqualTo(worldFocus.z).Within(0.01f));
        Assert.That(controller.FocusGrid(new Vector2Int(-1, -1)), Is.False);

        Vector2[] directions =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };
        foreach (Vector2 direction in directions)
        {
            Assert.That(controller.FocusGrid(playable), Is.True);
            Vector3 beforeKeyboardPan = camera.transform.position;
            Assert.That(controller.PanFromKeyboard(direction, 0.25f), Is.True);
            Assert.That(camera.transform.position, Is.Not.EqualTo(beforeKeyboardPan));
        }
        Assert.That(controller.KeyboardPanCount, Is.EqualTo(4));

        Assert.That(controller.PanWorld(new Vector2(1000f, 1000f)), Is.True);
        foreach (Vector3 point in controller.CurrentFootprint)
        {
            Assert.That(point.x, Is.InRange(
                controller.MapBounds.min.x - 0.01f,
                controller.MapBounds.max.x + 0.01f));
            Assert.That(point.z, Is.InRange(
                controller.MapBounds.min.z - 0.01f,
                controller.MapBounds.max.z + 0.01f));
        }

        string source = System.IO.File.ReadAllText(
            "Assets/Scripts/Squads/Tactical/TacticalCameraController.cs");
        Assert.That(source, Does.Contain("leftArrowKey").And.Contain("rightArrowKey")
            .And.Contain("upArrowKey").And.Contain("downArrowKey"));
        Assert.That(source, Does.Not.Contain("Keyboard.current.aKey"),
            "A remains reserved for the Attack command.");
    }

    [Test]
    public void StaticGridGraphicBuildsPotentialCellsExactlyOnce()
    {
        MapSetup setup = CreateMap(32, 32, 720, 7878);
        GameObject canvasObject = Track(new GameObject("Canvas", typeof(Canvas)));
        GameObject graphicObject = NewUi(canvasObject.transform, "Grid");
        RectTransform rect = graphicObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(320f, 320f);
        MinimapGridGraphic graphic = graphicObject.AddComponent<MinimapGridGraphic>();
        AspectRatioFitter fitter = graphicObject.AddComponent<AspectRatioFitter>();
        MinimapGridPresenter presenter = graphicObject.AddComponent<MinimapGridPresenter>();
        presenter.Configure(graphic, fitter, null);
        Assert.That(setup.Renderer.TryGetGeneratedWorldBounds(out Bounds bounds, false), Is.True);
        MinimapCoordinateMapper mapper = CreateMapper(setup, bounds);

        Assert.That(presenter.Build(setup.Generator, mapper), Is.True);
        graphic.Rebuild(CanvasUpdate.PreRender);
        Assert.That(graphic.PotentialElementCount, Is.EqualTo(1024));
        Assert.That(graphic.PlayableElementCount, Is.EqualTo(setup.Generator.PlayableCellCount));
        Assert.That(graphic.BuildCount, Is.EqualTo(1));
        Assert.That(presenter.Build(setup.Generator, mapper), Is.False);
        Assert.That(graphic.BuildCount, Is.EqualTo(1));
    }

    [Test]
    public void SquadMarkerMovesOnCellEventAndShowsDefeatedState()
    {
        MapSetup setup = CreateMap(10, 10, 80, 9991);
        List<Vector2Int> cells = EnumeratePlayable(setup.Generator);
        SquadBattleController player = CreateController(
            setup,
            "player-marker",
            cells[0],
            BattleSide.Player,
            SquadControlType.Human,
            0);
        GameObject layerObject = NewUi(Track(new GameObject("Canvas", typeof(Canvas))).transform, "Markers");
        RectTransform layer = layerObject.GetComponent<RectTransform>();
        MinimapSquadMarkerPresenter presenter = layerObject.AddComponent<MinimapSquadMarkerPresenter>();
        presenter.Configure(layer, null);
        Assert.That(setup.Renderer.TryGetGeneratedWorldBounds(out Bounds bounds, false), Is.True);
        MinimapCoordinateMapper mapper = CreateMapper(setup, bounds);
        Assert.That(presenter.Bind(new[] { player }, mapper), Is.True);
        RectTransform marker = presenter.GetMarkerRect(player.SquadId);
        Assert.That(marker, Is.Not.Null);
        AssertVector2(marker.anchorMin, mapper.GridToNormalized(cells[0]));

        Assert.That(player.GridAnchor.TryMoveToCell(cells[1]), Is.True);
        AssertVector2(marker.anchorMin, mapper.GridToNormalized(cells[1]));
        player.Runtime.ApplyDamage(10000, SquadDamageDistribution.Area);
        Assert.That(presenter.DisplaysDefeated(player.SquadId), Is.True);
        Assert.That(marker.sizeDelta.x, Is.LessThan(12f));
    }

    [Test]
    public void AutoCollapseUsesSingleStateMachineAndInteractionResetsTimer()
    {
        GameObject root = NewUi(Track(new GameObject("Canvas", typeof(Canvas))).transform, "Minimap");
        GameObject expanded = NewUi(root.transform, "Expanded");
        CanvasGroup group = expanded.AddComponent<CanvasGroup>();
        GameObject collapsed = NewUi(root.transform, "Collapsed");
        Button collapseButton = NewUi(expanded.transform, "Collapse").AddComponent<Button>();
        Button expandButton = collapsed.AddComponent<Button>();
        MinimapCollapseController controller = root.AddComponent<MinimapCollapseController>();
        controller.Configure(
            expanded.GetComponent<RectTransform>(),
            group,
            collapsed,
            collapseButton,
            expandButton,
            10f,
            0.2f);

        controller.Advance(9.5f);
        controller.RegisterInteraction();
        controller.Advance(9.5f);
        Assert.That(controller.State, Is.EqualTo(MinimapCollapseState.Expanded));
        controller.Advance(0.6f);
        Assert.That(controller.State, Is.EqualTo(MinimapCollapseState.Collapsing));
        Assert.That(controller.ActiveAnimationCount, Is.EqualTo(1));
        controller.BeginCollapse();
        Assert.That(controller.ActiveAnimationCount, Is.EqualTo(1));
        controller.Advance(0.3f);
        Assert.That(controller.State, Is.EqualTo(MinimapCollapseState.Collapsed));
        Assert.That(controller.ActiveAnimationCount, Is.Zero);
        expandButton.onClick.Invoke();
        controller.Advance(0.3f);
        Assert.That(controller.State, Is.EqualTo(MinimapCollapseState.Expanded));
    }

    [Test]
    public void MinimapInteractionFocusesCameraWithoutOwningGameplayContracts()
    {
        MapSetup setup = CreateMap(14, 10, 110, 6633);
        GameObject cameraObject = Track(new GameObject("Camera"));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(7f, 16f, -3f);
        camera.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
        TacticalCameraController tactical = cameraObject.AddComponent<TacticalCameraController>();
        tactical.Configure(camera, setup.Generator, setup.Renderer);
        Assert.That(tactical.Initialize(), Is.True);
        Assert.That(setup.Renderer.TryGetGeneratedWorldBounds(out Bounds bounds, false), Is.True);
        MinimapCoordinateMapper mapper = CreateMapper(setup, bounds);
        GameObject interactionObject = NewUi(
            Track(new GameObject("Canvas", typeof(Canvas))).transform,
            "Interaction");
        MinimapInteractionController interaction =
            interactionObject.AddComponent<MinimapInteractionController>();
        interaction.Configure(
            interactionObject.GetComponent<RectTransform>(),
            tactical,
            mapper,
            null);
        Vector2 normalized = mapper.GridToNormalized(setup.Generator.GetCentralPlayableCell());
        Assert.That(interaction.TryFocusNormalized(normalized), Is.True);
        Assert.That(interaction.AcceptedFocusCount, Is.EqualTo(1));
        Assert.That(interaction.GetComponent<MovementCommandController>(), Is.Null);
        Assert.That(interaction.GetComponent<AttackCommandController>(), Is.Null);
        Assert.That(interaction.GetComponent<AbilityCommandController>(), Is.Null);
        Assert.That(interaction.GetComponent<GridOccupancyService>(), Is.Null);
    }

    private MapSetup CreateMap(int width, int height, int playable, int seed)
    {
        GameObject root = Track(new GameObject($"Map_{width}x{height}"));
        MapGenerator generator = root.AddComponent<MapGenerator>();
        generator.autoGenerate = false;
        generator.width = width;
        generator.height = height;
        generator.playableCount = playable;
        generator.seed = seed;
        generator.Generate();
        MapRenderer renderer = root.AddComponent<MapRenderer>();
        renderer.autoRender = false;
        renderer.mapGenerator = generator;
        return new MapSetup { Generator = generator, Renderer = renderer };
    }

    private SquadBattleController CreateController(
        MapSetup map,
        string id,
        Vector2Int cell,
        BattleSide side,
        SquadControlType control,
        int sequence)
    {
        GameObject root = Track(new GameObject(id));
        SquadGridAnchor anchor = root.AddComponent<SquadGridAnchor>();
        SquadBattleController controller = root.AddComponent<SquadBattleController>();
        controller.Configure(anchor, null);
        Assert.That(controller.InitializeAtCell(
            CreateSquad(id),
            null,
            map.Generator,
            map.Renderer,
            cell,
            side,
            control,
            sequence), Is.True);
        return controller;
    }

    private static SquadData CreateSquad(string id) => new SquadData(
        id,
        new CommanderData
        {
            id = id + "-commander",
            baseStats = new SquadBaseStats
            {
                hp = 10,
                actionPoints = 5,
                initiative = 10,
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
                maxHP = 5,
                strength = 2,
                dexterity = 2
            }
        });

    private static MinimapCoordinateMapper CreateMapper(MapSetup setup, Bounds bounds) =>
        new MinimapCoordinateMapper(
            setup.Generator.Width,
            setup.Generator.Height,
            bounds,
            setup.Generator.GetIsPlayable,
            setup.Renderer.GetCellWorldCenter);

    private static List<Vector2Int> EnumeratePlayable(MapGenerator generator)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        for (int x = 0; x < generator.Width; x++)
        for (int y = 0; y < generator.Height; y++)
            if (generator.GetIsPlayable(x, y))
                result.Add(new Vector2Int(x, y));
        return result;
    }

    private static Vector2Int FindCell(MapGenerator generator, bool playable)
    {
        for (int x = 0; x < generator.Width; x++)
        for (int y = 0; y < generator.Height; y++)
            if (generator.GetIsPlayable(x, y) == playable)
                return new Vector2Int(x, y);
        Assert.Fail($"No cell with playable={playable} was generated.");
        return default;
    }

    private GameObject Track(GameObject value)
    {
        cleanup.Add(value);
        return value;
    }

    private static GameObject NewUi(Transform parent, string name)
    {
        GameObject value = new GameObject(name, typeof(RectTransform));
        value.transform.SetParent(parent, false);
        return value;
    }

    private static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        List<T> result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            result.AddRange(root.GetComponentsInChildren<T>(true));
        return result.ToArray();
    }

    private static void AssertVector2(Vector2 actual, Vector2 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
    }

    private sealed class MapSetup
    {
        public MapGenerator Generator;
        public MapRenderer Renderer;
    }
}
