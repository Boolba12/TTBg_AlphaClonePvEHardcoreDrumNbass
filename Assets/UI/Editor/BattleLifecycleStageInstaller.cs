#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class BattleLifecycleStageInstaller
{
    private const string BattleScenePath = "Assets/Scenes/Raw_Alpha_BattleMode.unity";
    private const string OverworldScenePath = "Assets/Scenes/first_try.unity";
    private const string DataFolder = "Assets/GameData/BattleLifecycle";
    private const string DebuffPath = DataFolder + "/DEV_BattleScar.asset";
    private const string RulesPath = DataFolder + "/DEV_PostBattleRules.asset";

    [MenuItem("Tools/Purgatory UI/Apply Battle Lifecycle Stage (Non-Destructive)")]
    public static void ApplyStage()
    {
        EnsureFolder("Assets/GameData", "BattleLifecycle");
        PersistentDebuffDefinition debuff = LoadOrCreate<PersistentDebuffDefinition>(DebuffPath);
        debuff.ConfigureDevelopment(
            "DEV_BattleScar",
            "Battle Scar",
            "A permanent development injury: Resolve -1.",
            -1f);
        PostBattleRules rules = LoadOrCreate<PostBattleRules>(RulesPath);
        rules.ConfigureDevelopment(0.20f, debuff);
        EditorUtility.SetDirty(debuff);
        EditorUtility.SetDirty(rules);
        AssetDatabase.SaveAssets();

        WireBattleScene(rules);
        WireOverworldScene();
        AssetDatabase.SaveAssets();
        Debug.Log("BattleLifecycleStageInstaller: Phase A lifecycle, result UI, autosave, and return composition installed.");
    }

    private static void WireBattleScene(PostBattleRules rules)
    {
        Scene scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
        SquadBattleBootstrap bootstrap = RequireExactlyOne<SquadBattleBootstrap>();
        BattleHUDController hud = RequireExactlyOne<BattleHUDController>();
        SaveSystemBehaviour saves = RequireExactlyOne<SaveSystemBehaviour>();
        SquadSaveParticipant repository = RequireExactlyOne<SquadSaveParticipant>();
        SquadBattleTacticalBootstrap tactical = RequireExactlyOne<SquadBattleTacticalBootstrap>();
        GameObject tacticalRoot = tactical.gameObject;

        GridOccupancyService occupancy = RequireOnRoot<GridOccupancyService>(tacticalRoot);
        BattleSquadSelectionController selection = RequireOnRoot<BattleSquadSelectionController>(tacticalRoot);
        BattleTurnController turns = RequireOnRoot<BattleTurnController>(tacticalRoot);
        SquadMovementService movement = RequireOnRoot<SquadMovementService>(tacticalRoot);
        BattleCommandModeController modes = RequireOnRoot<BattleCommandModeController>(tacticalRoot);
        BattleAttackService attacks = RequireOnRoot<BattleAttackService>(tacticalRoot);
        MovementCommandController movementCommands = RequireOnRoot<MovementCommandController>(tacticalRoot);
        AttackCommandController attackCommands = RequireOnRoot<AttackCommandController>(tacticalRoot);
        BattleAbilityService abilityService = tacticalRoot.GetComponent<BattleAbilityService>();
        AbilityCommandController abilityCommands = tacticalRoot.GetComponent<AbilityCommandController>();
        BattleResultPanelView panel = EnsureResultPanel(hud);
        BattleCompletionController completion = GetOrAdd<BattleCompletionController>(tacticalRoot);
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
            saves,
            rules,
            panel,
            "first_try");
        completion.ConfigureAbilities(abilityService, abilityCommands);
        tactical.Configure(
            bootstrap,
            occupancy,
            selection,
            turns,
            movement,
            modes,
            attacks,
            movementCommands,
            attackCommands,
            completion,
            abilityService,
            abilityCommands);

        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(completion);
        EditorUtility.SetDirty(tactical);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void WireOverworldScene()
    {
        Scene scene = EditorSceneManager.OpenScene(OverworldScenePath, OpenSceneMode.Single);
        GameObject root = GameObject.Find("PersistentGameRuntime");
        if (root == null)
        {
            root = new GameObject("PersistentGameRuntime");
            SceneManager.MoveGameObjectToScene(root, scene);
        }

        SquadSaveParticipant repository = GetOrAdd<SquadSaveParticipant>(root);
        OverworldSaveParticipant overworld = GetOrAdd<OverworldSaveParticipant>(root);
        SaveSystemBehaviour saves = GetOrAdd<SaveSystemBehaviour>(root);
        OverworldBattleResultReceiver receiver = GetOrAdd<OverworldBattleResultReceiver>(root);
        EventSystem[] eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include);
        if (eventSystems.Length > 1)
            throw new InvalidOperationException(
                $"first_try contains {eventSystems.Length} EventSystems; expected at most one.");
        if (eventSystems.Length == 0)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        MapGenerator generator = RequireExactlyOne<MapGenerator>();
        MapRenderer renderer = RequireExactlyOne<MapRenderer>();
        MapRockPlacer rocks = RequireExactlyOne<MapRockPlacer>();
        PlayerController player = RequireExactlyOne<PlayerController>();
        EnemyController enemy = RequireExactlyOne<EnemyController>();
        TurnSystem turns = RequireExactlyOne<TurnSystem>();
        overworld.Configure(generator, renderer, rocks, player, enemy);
        saves.ConfigureParticipants(new MonoBehaviour[] { overworld, repository });
        receiver.Configure(saves);

        SerializedObject turnSerialized = new SerializedObject(turns);
        turnSerialized.FindProperty("squadRepository").objectReferenceValue = repository;
        turnSerialized.FindProperty("saveSystem").objectReferenceValue = saves;
        turnSerialized.FindProperty("battleSceneName").stringValue = "Raw_Alpha_BattleMode";
        turnSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(overworld);
        EditorUtility.SetDirty(saves);
        EditorUtility.SetDirty(receiver);
        EditorUtility.SetDirty(turns);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static BattleResultPanelView EnsureResultPanel(BattleHUDController hud)
    {
        BattleResultPanelView existing = UnityEngine.Object.FindFirstObjectByType<BattleResultPanelView>(
            FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        Transform modalLayer = hud.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(candidate => candidate.name == "ModalLayer");
        if (modalLayer == null)
            throw new InvalidOperationException("Battle HUD ModalLayer is missing.");
        PurgatoryUITheme theme = LoadSingleAsset<PurgatoryUITheme>();

        GameObject overlay = NewUI("BattleResultPanel", modalLayer);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        Stretch(overlayRect);
        Image overlayImage = overlay.AddComponent<Image>();
        overlayImage.color = theme != null ? theme.Overlay : new Color(0f, 0f, 0f, 0.84f);

        GameObject card = NewUI("ResultCard", overlay.transform);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.31f, 0.18f);
        cardRect.anchorMax = new Vector2(0.69f, 0.82f);
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;
        Image cardImage = card.AddComponent<Image>();
        cardImage.sprite = theme?.PanelSprite;
        cardImage.type = theme?.PanelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        cardImage.color = theme != null ? theme.SurfaceRaised : new Color32(36, 42, 43, 255);
        VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 12f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text title = CreateText("ResultTitle", card.transform, theme, 32f, FontStyles.Bold);
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
        TMP_Text summary = CreateText("ResultSummary", card.transform, theme, 21f, FontStyles.Normal);
        summary.gameObject.AddComponent<LayoutElement>().preferredHeight = 130f;
        TMP_Text commander = CreateText("CommanderOutcome", card.transform, theme, 20f, FontStyles.Normal);
        commander.gameObject.AddComponent<LayoutElement>().preferredHeight = 62f;
        TMP_Text saveStatus = CreateText("SaveStatus", card.transform, theme, 18f, FontStyles.Italic);
        saveStatus.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;

        GameObject buttons = NewUI("Buttons", card.transform);
        buttons.AddComponent<LayoutElement>().preferredHeight = 58f;
        HorizontalLayoutGroup buttonLayout = buttons.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 12f;
        buttonLayout.childControlHeight = true;
        buttonLayout.childControlWidth = true;
        buttonLayout.childForceExpandWidth = true;
        Button retry = CreateButton("RetrySave", buttons.transform, "Retry Save", theme);
        Button continueButton = CreateButton("Continue", buttons.transform, "Continue", theme);

        BattleResultPanelView panel = overlay.AddComponent<BattleResultPanelView>();
        panel.Configure(overlay, title, summary, commander, saveStatus, continueButton, retry);
        overlay.transform.SetAsLastSibling();
        return panel;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        PurgatoryUITheme theme,
        float size,
        FontStyles style)
    {
        GameObject gameObject = NewUI(name, parent);
        TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
        text.font = theme?.PrimaryFont ?? TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = theme != null ? theme.TextPrimary : Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = true;
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        PurgatoryUITheme theme)
    {
        GameObject gameObject = NewUI(name, parent);
        Image image = gameObject.AddComponent<Image>();
        image.sprite = theme?.ButtonSprite;
        image.type = theme?.ButtonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = theme != null ? theme.Bronze : new Color32(133, 88, 42, 255);
        Button button = gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText("Label", gameObject.transform, theme, 20f, FontStyles.Bold);
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform);
        return button;
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static T RequireExactlyOne<T>() where T : UnityEngine.Object
    {
        T[] values = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (values.Length != 1)
            throw new InvalidOperationException($"Expected exactly one {typeof(T).Name}; found {values.Length}.");
        return values[0];
    }

    private static T RequireOnRoot<T>(GameObject root) where T : Component
    {
        T value = root.GetComponent<T>();
        if (value == null)
            throw new InvalidOperationException($"{root.name} is missing {typeof(T).Name}.");
        return value;
    }

    private static T GetOrAdd<T>(GameObject root) where T : Component
    {
        return root.GetComponent<T>() ?? root.AddComponent<T>();
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static T LoadSingleAsset<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids.Length != 1)
            throw new InvalidOperationException($"Expected exactly one {typeof(T).Name} asset; found {guids.Length}.");
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
