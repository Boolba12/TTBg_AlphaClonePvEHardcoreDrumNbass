#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BattleMinimapStageInstaller
{
    private const string ScenePath = "Assets/Scenes/Raw_Alpha_BattleMode.unity";
    private const string HudPrefabPath = "Assets/UI/Prefabs/Battle/BattleHUD.prefab";
    private const string ThemePath = "Assets/UI/Themes/PurgatoryUITheme.asset";

    [MenuItem("Tools/Purgatory UI/Apply 32x32 Camera + Minimap Stage (Non-Destructive)")]
    public static void ApplyStage()
    {
        PurgatoryUITheme theme = AssetDatabase.LoadAssetAtPath<PurgatoryUITheme>(ThemePath);
        Require(theme != null, "Purgatory UI theme is missing.");
        UpgradeHudPrefab(theme);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        WireScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "BattleMinimapStageInstaller: production 32x32 configuration, tactical camera, " +
            "schematic minimap, event markers, viewport and unscaled auto-collapse installed.");
    }

    private static void UpgradeHudPrefab(PurgatoryUITheme theme)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
        try
        {
            Transform container = FindDescendant(root.transform, "TopRight_MinimapContainer");
            Require(container != null, "Battle HUD minimap placeholder is missing.");
            Transform expanded = container.Find("MinimapExpanded");
            if (expanded == null)
                expanded = CreateMinimapHierarchy(container, theme);

            foreach (Transform child in container)
            {
                if (child != expanded && child.name != "MinimapCollapsed")
                    child.gameObject.SetActive(false);
            }
            PanelFrameView oldFrame = container.GetComponent<PanelFrameView>();
            if (oldFrame != null)
                oldFrame.enabled = false;
            Image oldImage = container.GetComponent<Image>();
            if (oldImage != null)
            {
                oldImage.color = Color.clear;
                oldImage.raycastTarget = false;
            }

            RectTransform expandedRect = expanded as RectTransform;
            CanvasGroup expandedGroup = expanded.GetComponent<CanvasGroup>();
            Transform collapsed = container.Find("MinimapCollapsed");
            Button collapseButton = FindDescendant(expanded, "CollapseButton")?.GetComponent<Button>();
            Button expandButton = collapsed?.GetComponent<Button>();
            Transform mapContent = FindDescendant(expanded, "MapContent");
            Require(expandedGroup != null && collapsed != null && collapseButton != null &&
                    expandButton != null && mapContent != null,
                "Minimap hierarchy is incomplete.");

            GetOrAdd<CanvasRenderer>(mapContent.gameObject);
            Require(mapContent.GetComponents<CanvasRenderer>().Length == 1,
                "MapContent must own exactly one CanvasRenderer for its raycastable UI Graphic.");

            MinimapGridGraphic gridGraphic = mapContent.GetComponent<MinimapGridGraphic>();
            AspectRatioFitter fitter = mapContent.GetComponent<AspectRatioFitter>();
            MinimapGridPresenter grid = mapContent.GetComponent<MinimapGridPresenter>();
            MinimapSquadMarkerPresenter markers = mapContent.GetComponent<MinimapSquadMarkerPresenter>();
            MinimapCameraViewportPresenter viewport =
                mapContent.GetComponent<MinimapCameraViewportPresenter>();
            MinimapInteractionController interaction =
                mapContent.GetComponent<MinimapInteractionController>();
            RectTransform markerLayer = mapContent.Find("MarkerLayer") as RectTransform;
            MinimapViewportGraphic viewportGraphic =
                mapContent.Find("ViewportLayer")?.GetComponent<MinimapViewportGraphic>();
            Require(gridGraphic != null && fitter != null && grid != null && markers != null &&
                    viewport != null && interaction != null && markerLayer != null &&
                    viewportGraphic != null,
                "Minimap components are incomplete.");

            grid.Configure(gridGraphic, fitter, theme);
            markers.Configure(markerLayer, theme);
            viewport.Configure(viewportGraphic);
            MinimapCollapseController collapse = GetOrAdd<MinimapCollapseController>(container.gameObject);
            collapse.Configure(
                expandedRect,
                expandedGroup,
                collapsed.gameObject,
                collapseButton,
                expandButton,
                10f,
                0.18f);
            interaction.Configure(
                mapContent as RectTransform,
                null,
                null,
                collapse);
            TacticalMinimapController owner = GetOrAdd<TacticalMinimapController>(container.gameObject);
            owner.Configure(null, null, null, null, grid, markers, viewport, interaction, collapse);
            EditorUtility.SetDirty(owner);
            EditorUtility.SetDirty(collapse);
            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform CreateMinimapHierarchy(Transform parent, PurgatoryUITheme theme)
    {
        GameObject expanded = NewUI("MinimapExpanded", parent);
        Stretch(expanded.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image background = expanded.AddComponent<Image>();
        PanelFrameView frame = expanded.AddComponent<PanelFrameView>();
        frame.Configure(theme, background, PanelFrameStyle.Outer);
        expanded.AddComponent<CanvasGroup>();

        TMP_Text title = CreateText("Title", expanded.transform, theme, "TACTICAL MAP", 15f);
        Stretch(title.rectTransform, new Vector2(0.08f, 0.81f), new Vector2(0.78f, 0.97f), Vector2.zero, Vector2.zero);
        title.alignment = TextAlignmentOptions.MidlineLeft;

        Button collapse = CreateButton("CollapseButton", expanded.transform, theme, "−");
        RectTransform collapseRect = collapse.GetComponent<RectTransform>();
        collapseRect.anchorMin = new Vector2(0.82f, 0.82f);
        collapseRect.anchorMax = new Vector2(0.96f, 0.96f);
        collapseRect.offsetMin = collapseRect.offsetMax = Vector2.zero;

        GameObject mapSurface = NewUI("MapSurface", expanded.transform);
        Stretch(mapSurface.GetComponent<RectTransform>(), new Vector2(0.07f, 0.08f), new Vector2(0.93f, 0.79f), Vector2.zero, Vector2.zero);
        Image surfaceImage = mapSurface.AddComponent<Image>();
        surfaceImage.sprite = theme.InsetPanelSprite;
        surfaceImage.type = surfaceImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        surfaceImage.color = Color.white;
        surfaceImage.raycastTarget = false;

        GameObject mapContent = NewUI("MapContent", mapSurface.transform);
        Stretch(mapContent.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(7f, 7f), new Vector2(-7f, -7f));
        mapContent.AddComponent<CanvasRenderer>();
        MinimapGridGraphic gridGraphic = mapContent.AddComponent<MinimapGridGraphic>();
        gridGraphic.raycastTarget = true;
        AspectRatioFitter fitter = mapContent.AddComponent<AspectRatioFitter>();
        mapContent.AddComponent<MinimapGridPresenter>();
        mapContent.AddComponent<MinimapSquadMarkerPresenter>();
        mapContent.AddComponent<MinimapCameraViewportPresenter>();
        mapContent.AddComponent<MinimapInteractionController>();

        GameObject markerLayer = NewUI("MarkerLayer", mapContent.transform);
        Stretch(markerLayer.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        GameObject viewportLayer = NewUI("ViewportLayer", mapContent.transform);
        Stretch(viewportLayer.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        MinimapViewportGraphic viewport = viewportLayer.AddComponent<MinimapViewportGraphic>();
        viewport.color = theme.Gold;
        viewport.raycastTarget = false;

        GameObject collapsed = NewUI("MinimapCollapsed", parent);
        RectTransform collapsedRect = collapsed.GetComponent<RectTransform>();
        collapsedRect.anchorMin = collapsedRect.anchorMax = Vector2.one;
        collapsedRect.pivot = Vector2.one;
        collapsedRect.anchoredPosition = Vector2.zero;
        collapsedRect.sizeDelta = new Vector2(62f, 48f);
        Button expandButton = collapsed.AddComponent<Button>();
        Image collapsedImage = collapsed.AddComponent<Image>();
        expandButton.targetGraphic = collapsedImage;
        collapsedImage.sprite = theme.ButtonSprite;
        collapsedImage.type = collapsedImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        TMP_Text compactLabel = CreateText("Label", collapsed.transform, theme, "MAP", 13f);
        Stretch(compactLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        compactLabel.alignment = TextAlignmentOptions.Center;
        collapsed.SetActive(false);
        return expanded.transform;
    }

    private static void WireScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BattleMapBootstrap mapBootstrap = RequireExactlyOne<BattleMapBootstrap>();
        MapGenerator generator = mapBootstrap.mapGenerator;
        MapRenderer renderer = mapBootstrap.mapRenderer;
        Require(generator != null && renderer != null,
            "BattleMapBootstrap canonical map references are missing.");
        SquadBattleBootstrap squads = RequireExactlyOne<SquadBattleBootstrap>();
        BattleTurnController turns = RequireExactlyOne<BattleTurnController>();
        Camera camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)
            .Single(candidate => candidate.CompareTag("MainCamera"));
        TacticalMinimapController minimap = RequireExactlyOne<TacticalMinimapController>();
        TacticalCameraController tacticalCamera = GetOrAdd<TacticalCameraController>(camera.gameObject);

        mapBootstrap.overrideBattleSize = true;
        mapBootstrap.battleWidth = 32;
        mapBootstrap.battleHeight = 32;
        mapBootstrap.battlePlayableCount = 720;
        generator.width = 32;
        generator.height = 32;
        generator.playableCount = 720;
        tacticalCamera.Configure(camera, generator, renderer, turns);
        minimap.Configure(
            generator,
            renderer,
            squads,
            tacticalCamera,
            minimap.GridPresenter,
            minimap.MarkerPresenter,
            minimap.ViewportPresenter,
            minimap.InteractionController,
            minimap.CollapseController);

        EditorUtility.SetDirty(mapBootstrap);
        EditorUtility.SetDirty(generator);
        EditorUtility.SetDirty(tacticalCamera);
        EditorUtility.SetDirty(minimap);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        GameObject value = new GameObject(name, typeof(RectTransform));
        value.transform.SetParent(parent, false);
        return value;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        PurgatoryUITheme theme,
        string text,
        float size)
    {
        GameObject value = NewUI(name, parent);
        TextMeshProUGUI label = value.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = theme.AccentFont;
        label.fontSize = size;
        label.color = theme.TextPrimary;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        PurgatoryUITheme theme,
        string label)
    {
        GameObject value = NewUI(name, parent);
        Image image = value.AddComponent<Image>();
        image.sprite = theme.ButtonSprite;
        image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        Button button = value.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText("Label", value.transform, theme, label, 18f);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static void Stretch(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null)
            return null;
        foreach (Transform child in root)
        {
            if (child.name == name)
                return child;
            Transform nested = FindDescendant(child, name);
            if (nested != null)
                return nested;
        }
        return null;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component =>
        target.GetComponent<T>() ?? target.AddComponent<T>();

    private static T RequireExactlyOne<T>() where T : UnityEngine.Object
    {
        T[] values = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        Require(values.Length == 1,
            $"Expected exactly one {typeof(T).Name}; found {values.Length}.");
        return values[0];
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
