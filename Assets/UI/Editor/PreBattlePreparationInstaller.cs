#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PreBattlePreparationInstaller
{
    private const string ScenePath = "Assets/Scenes/first_try.unity";
    private const string BattleScenePath = "Assets/Scenes/Raw_Alpha_BattleMode.unity";
    private const string ThemePath = "Assets/UI/Themes/PurgatoryUITheme.asset";
    private const string PortraitDatabasePath =
        "Assets/Art/CommanderPortraits/CommanderPortraitDatabase.asset";
    private const string OwnedRootName = "PreBattlePreparationCanvas";

    [MenuItem("Tools/Purgatory UI/Apply Pre-Battle Preparation Stage")]
    public static void ApplyStage()
    {
        PurgatoryUITheme theme = AssetDatabase.LoadAssetAtPath<PurgatoryUITheme>(ThemePath);
        CommanderPortraitDatabase portraits =
            AssetDatabase.LoadAssetAtPath<CommanderPortraitDatabase>(PortraitDatabasePath);
        if (theme == null || portraits == null)
            throw new InvalidOperationException("Pre-Battle theme or portrait database is missing.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        TurnSystem[] turnSystems = UnityEngine.Object.FindObjectsByType<TurnSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        SquadSaveParticipant[] repositories =
            UnityEngine.Object.FindObjectsByType<SquadSaveParticipant>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        EventSystem[] eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (turnSystems.Length != 1 || repositories.Length != 1 || eventSystems.Length != 1)
        {
            throw new InvalidOperationException(
                $"first_try requires one TurnSystem, SquadSaveParticipant, and EventSystem; " +
                $"found {turnSystems.Length}, {repositories.Length}, {eventSystems.Length}.");
        }
        EnsureStarterPersistentSquad(repositories[0], portraits);

        GameObject existing = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == OwnedRootName);
        if (existing != null)
        {
            PreBattlePreparationController existingController =
                existing.GetComponent<PreBattlePreparationController>();
            if (existingController == null || existing.GetComponent<PreBattlePreparationView>() == null)
            {
                throw new InvalidOperationException(
                    "Existing PreBattlePreparationCanvas is not the expected owned hierarchy; " +
                    "it was left unchanged.");
            }
            WireTurnSystem(turnSystems[0], existingController);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ValidateScene(scene);
            WireBattleStartupRestore();
            Debug.Log("PreBattlePreparationInstaller: existing Pre-Battle stage validated and rewired.");
            return;
        }

        GameObject canvasRoot = NewUIObject(OwnedRootName, null);
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasRoot.AddComponent<GraphicRaycaster>();
        PreBattlePreparationView view = canvasRoot.AddComponent<PreBattlePreparationView>();
        PreBattlePreparationController controller =
            canvasRoot.AddComponent<PreBattlePreparationController>();

        GameObject overlay = NewUIObject("PreBattlePreparationPanel", canvasRoot.transform);
        Stretch(overlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image overlayImage = overlay.AddComponent<Image>();
        overlayImage.color = theme.Overlay;
        overlayImage.raycastTarget = true;
        CanvasGroup blocker = overlay.AddComponent<CanvasGroup>();

        GameObject frame = NewUIObject("PreparationFrame", overlay.transform);
        SetRect(frame.GetComponent<RectTransform>(), new Vector2(0.035f, 0.055f),
            new Vector2(0.965f, 0.945f));
        AddPanelFrame(frame, theme, PanelFrameStyle.Outer);
        TMP_Text title = CreateText("Title", frame.transform, theme, "BATTLE PREPARATION",
            TextAlignmentOptions.Center, theme.HeadingSize + 5f, theme.Gold);
        SetRect(title.rectTransform, new Vector2(0.02f, 0.89f), new Vector2(0.98f, 0.975f));
        TMP_Text subtitle = CreateText("Subtitle", frame.transform, theme,
            "Select one persistent squad for the hostile encounter",
            TextAlignmentOptions.Center, theme.CaptionSize, theme.TextSecondary);
        SetRect(subtitle.rectTransform, new Vector2(0.02f, 0.855f), new Vector2(0.98f, 0.90f));

        GameObject left = CreateSection(frame.transform, theme, "AvailableSquads", "AVAILABLE SQUADS",
            new Vector2(0.025f, 0.18f), new Vector2(0.345f, 0.85f));
        GameObject center = CreateSection(frame.transform, theme, "SelectedSquad", "SELECTED SQUAD",
            new Vector2(0.355f, 0.18f), new Vector2(0.695f, 0.85f));
        GameObject right = CreateSection(frame.transform, theme, "BattleSummary", "BATTLE SUMMARY",
            new Vector2(0.705f, 0.18f), new Vector2(0.975f, 0.85f));

        RectTransform content = CreateSquadList(left.transform, theme,
            out PreBattleSquadCardView template, out TMP_Text emptyRoster);
        Image selectedPortrait = CreateImage("CommanderPortrait", center.transform,
            theme.DevelopmentPortraitFallback, Color.white);
        SetRect(selectedPortrait.rectTransform, new Vector2(0.06f, 0.62f), new Vector2(0.36f, 0.90f));
        selectedPortrait.preserveAspect = true;
        TMP_Text selectedTitle = CreateText("SquadTitle", center.transform, theme,
            "No squad selected", TextAlignmentOptions.TopLeft, theme.BodySize, theme.Gold);
        SetRect(selectedTitle.rectTransform, new Vector2(0.40f, 0.80f), new Vector2(0.94f, 0.90f));
        TMP_Text selectedCommander = CreateText("Commander", center.transform, theme,
            "Commander —", TextAlignmentOptions.TopLeft, theme.CaptionSize, theme.Marble);
        SetRect(selectedCommander.rectTransform, new Vector2(0.40f, 0.70f), new Vector2(0.94f, 0.80f));
        TMP_Text selectedComposition = CreateText("Composition", center.transform, theme,
            "Warriors —", TextAlignmentOptions.TopLeft, theme.CaptionSize, theme.TextSecondary);
        SetRect(selectedComposition.rectTransform, new Vector2(0.40f, 0.62f), new Vector2(0.94f, 0.70f));
        TMP_Text selectedStats = CreateText("CalculatedStats", center.transform, theme,
            "Select a squad to inspect calculated battle values.", TextAlignmentOptions.TopLeft,
            theme.CaptionSize, theme.Marble, true);
        SetRect(selectedStats.rectTransform, new Vector2(0.06f, 0.26f), new Vector2(0.94f, 0.58f));
        TMP_Text equipment = CreateText("EquipmentSummary", center.transform, theme,
            "Equipment —", TextAlignmentOptions.TopLeft, theme.CaptionSize, theme.TextSecondary, true);
        SetRect(equipment.rectTransform, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.24f));

        TMP_Text encounter = CreateText("EncounterSummary", right.transform, theme,
            string.Empty, TextAlignmentOptions.TopLeft, theme.CaptionSize, theme.Marble, true);
        SetRect(encounter.rectTransform, new Vector2(0.07f, 0.39f), new Vector2(0.93f, 0.90f));
        TMP_Text warning = CreateText("EnemyIntelWarning", right.transform, theme,
            "Only confirmed overworld intelligence is shown.", TextAlignmentOptions.TopLeft,
            theme.CaptionSize - 2f, theme.TextSecondary, true);
        SetRect(warning.rectTransform, new Vector2(0.07f, 0.26f), new Vector2(0.93f, 0.38f));
        TMP_Text validation = CreateText("ValidationStatus", right.transform, theme,
            "Select one battle-ready squad.", TextAlignmentOptions.TopLeft,
            theme.CaptionSize, theme.Gold, true);
        SetRect(validation.rectTransform, new Vector2(0.07f, 0.06f), new Vector2(0.93f, 0.24f));

        Button cancel = CreateButton(frame.transform, theme, "CancelButton", "CANCEL",
            ThemedButtonStyle.Secondary);
        SetRect(cancel.GetComponent<RectTransform>(), new Vector2(0.60f, 0.055f),
            new Vector2(0.77f, 0.14f));
        Button confirm = CreateButton(frame.transform, theme, "ConfirmButton", "CONFIRM",
            ThemedButtonStyle.Primary);
        SetRect(confirm.GetComponent<RectTransform>(), new Vector2(0.79f, 0.055f),
            new Vector2(0.955f, 0.14f));
        confirm.interactable = false;

        view.Configure(overlay, blocker, content, template, emptyRoster, selectedPortrait,
            selectedTitle, selectedCommander, selectedComposition, selectedStats, equipment,
            encounter, validation, confirm, cancel);
        controller.Configure(repositories[0], portraits, view, turnSystems[0]);
        WireTurnSystem(turnSystems[0], controller);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        ValidateScene(scene);
        WireBattleStartupRestore();
        Debug.Log("PreBattlePreparationInstaller: first_try Pre-Battle stage wired successfully.");
    }

    private static RectTransform CreateSquadList(Transform parent, PurgatoryUITheme theme,
        out PreBattleSquadCardView template, out TMP_Text emptyRoster)
    {
        GameObject scrollObject = NewUIObject("SquadScroll", parent);
        SetRect(scrollObject.GetComponent<RectTransform>(), new Vector2(0.045f, 0.05f),
            new Vector2(0.955f, 0.90f));
        Image scrollBackground = scrollObject.AddComponent<Image>();
        scrollBackground.sprite = theme.InsetPanelSprite;
        scrollBackground.type = Image.Type.Sliced;
        scrollBackground.color = Color.white;
        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = NewUIObject("Viewport", scrollObject.transform);
        Stretch(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(8f, 8f), new Vector2(-8f, -8f));
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = viewport.GetComponent<RectTransform>();

        GameObject contentObject = NewUIObject("Content", viewport.transform);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;

        GameObject card = NewUIObject("SquadCardTemplate", content);
        card.AddComponent<LayoutElement>().preferredHeight = 142f;
        Image cardImage = card.AddComponent<Image>();
        cardImage.sprite = theme.InitiativeCardSprite;
        cardImage.type = Image.Type.Sliced;
        cardImage.color = Color.white;
        Button button = card.AddComponent<Button>();
        button.targetGraphic = cardImage;
        Image portrait = CreateImage("Portrait", card.transform,
            theme.DevelopmentPortraitFallback, Color.white);
        SetRect(portrait.rectTransform, new Vector2(0.035f, 0.13f), new Vector2(0.24f, 0.87f));
        portrait.preserveAspect = true;
        TMP_Text squad = CreateText("Squad", card.transform, theme, "Squad",
            TextAlignmentOptions.TopLeft, theme.CaptionSize, theme.Gold);
        SetRect(squad.rectTransform, new Vector2(0.28f, 0.67f), new Vector2(0.95f, 0.91f));
        TMP_Text commander = CreateText("Commander", card.transform, theme, "Commander",
            TextAlignmentOptions.TopLeft, theme.CaptionSize - 2f, theme.Marble);
        SetRect(commander.rectTransform, new Vector2(0.28f, 0.43f), new Vector2(0.95f, 0.68f));
        TMP_Text composition = CreateText("Composition", card.transform, theme, "Warriors",
            TextAlignmentOptions.TopLeft, theme.CaptionSize - 2f, theme.TextSecondary);
        SetRect(composition.rectTransform, new Vector2(0.28f, 0.22f), new Vector2(0.95f, 0.45f));
        TMP_Text status = CreateText("Status", card.transform, theme, "READY",
            TextAlignmentOptions.TopLeft, theme.CaptionSize - 3f, theme.Emerald, true);
        SetRect(status.rectTransform, new Vector2(0.28f, 0.03f), new Vector2(0.95f, 0.25f));
        Image selected = CreateImage("SelectedFrame", card.transform,
            theme.SelectedFrameSprite, Color.white);
        Stretch(selected.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        selected.type = Image.Type.Sliced;
        selected.raycastTarget = false;
        selected.gameObject.SetActive(false);
        template = card.AddComponent<PreBattleSquadCardView>();
        template.Configure(button, portrait, squad, commander, composition, status, selected.gameObject);
        card.SetActive(false);

        emptyRoster = CreateText("EmptyRoster", scrollObject.transform, theme,
            "No persistent squads available.", TextAlignmentOptions.Center,
            theme.CaptionSize, theme.TextSecondary, true);
        SetRect(emptyRoster.rectTransform, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.65f));
        return content;
    }

    private static GameObject CreateSection(Transform parent, PurgatoryUITheme theme,
        string name, string title, Vector2 min, Vector2 max)
    {
        GameObject section = NewUIObject(name, parent);
        SetRect(section.GetComponent<RectTransform>(), min, max);
        AddPanelFrame(section, theme, PanelFrameStyle.Inset);
        TMP_Text label = CreateText("Header", section.transform, theme, title,
            TextAlignmentOptions.Center, theme.CaptionSize, theme.Gold);
        SetRect(label.rectTransform, new Vector2(0.04f, 0.91f), new Vector2(0.96f, 0.985f));
        Image separator = CreateImage("Separator", section.transform, theme.SeparatorSprite, Color.white);
        SetRect(separator.rectTransform, new Vector2(0.04f, 0.90f), new Vector2(0.96f, 0.912f));
        separator.type = Image.Type.Sliced;
        return section;
    }

    private static Button CreateButton(Transform parent, PurgatoryUITheme theme,
        string name, string label, ThemedButtonStyle style)
    {
        GameObject root = NewUIObject(name, parent);
        Image background = root.AddComponent<Image>();
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
        TMP_Text text = CreateText("Label", root.transform, theme, label,
            TextAlignmentOptions.Center, theme.CaptionSize, theme.Marble);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.AddComponent<ThemedButtonView>().Configure(theme, style, button, background, text, null);
        return button;
    }

    private static void AddPanelFrame(GameObject root, PurgatoryUITheme theme, PanelFrameStyle style)
    {
        Image image = root.GetComponent<Image>() ?? root.AddComponent<Image>();
        root.AddComponent<PanelFrameView>().Configure(theme, image, style);
    }

    private static TMP_Text CreateText(string name, Transform parent, PurgatoryUITheme theme,
        string value, TextAlignmentOptions alignment, float size, Color color, bool wrap = false)
    {
        GameObject root = NewUIObject(name, parent);
        TextMeshProUGUI text = root.AddComponent<TextMeshProUGUI>();
        text.font = theme.PrimaryFont;
        text.fontSize = size;
        text.color = color;
        text.text = value ?? string.Empty;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject root = NewUIObject(name, parent);
        Image image = root.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static GameObject NewUIObject(string name, Transform parent)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
            root.transform.SetParent(parent, false);
        return root;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max) =>
        Stretch(rect, min, max, Vector2.zero, Vector2.zero);

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private static void WireTurnSystem(TurnSystem turnSystem, PreBattlePreparationController controller)
    {
        SerializedObject serializedTurn = new SerializedObject(turnSystem);
        SerializedProperty preparation = serializedTurn.FindProperty("preBattlePreparationController");
        if (preparation == null)
            throw new InvalidOperationException("TurnSystem Pre-Battle serialized field is missing.");
        preparation.objectReferenceValue = controller;
        serializedTurn.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureStarterPersistentSquad(
        SquadSaveParticipant repository,
        CommanderPortraitDatabase portraits)
    {
        if (repository.Squads.Count > 0)
            return;

        CommanderPortraitEntry humanPortrait = portraits.Entries.FirstOrDefault(entry =>
            entry != null && entry.Race == CommanderRace.Human &&
            !string.IsNullOrWhiteSpace(entry.Id));
        if (humanPortrait == null)
            throw new InvalidOperationException("Starter persistent squad requires a Human portrait ID.");

        SquadData starter = new SquadData(
            "starter-player-squad",
            new CommanderData
            {
                id = "starter-player-commander",
                race = CommanderRace.Human,
                commanderPortraitId = humanPortrait.Id,
                baseStats = new SquadBaseStats
                {
                    hp = 18,
                    actionPoints = 8,
                    initiative = 12,
                    physicalSpeed = 5,
                    strength = 6,
                    dexterity = 5,
                    accuracy = 0.1f,
                    evasion = 0.05f,
                    criticalChance = 0.1f,
                    criticalDamage = 1.5f,
                    physicalArmor = 0.1f,
                    morale = 60,
                    resolve = 6,
                    visionRange = 6
                }
            },
            new[]
            {
                new WarriorData { id = "starter-warrior-01", maxHP = 10, strength = 2, dexterity = 2 },
                new WarriorData { id = "starter-warrior-02", maxHP = 10, strength = 2, dexterity = 2 },
                new WarriorData { id = "starter-warrior-03", maxHP = 10, strength = 2, dexterity = 2 }
            });
        if (!repository.TryAddSquad(starter, out string error))
            throw new InvalidOperationException($"Starter persistent squad is invalid: {error}");
    }

    private static void ValidateScene(Scene scene)
    {
        PreBattlePreparationController[] controllers =
            UnityEngine.Object.FindObjectsByType<PreBattlePreparationController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        EventSystem[] eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (controllers.Length != 1 || eventSystems.Length != 1)
            throw new InvalidOperationException("Pre-Battle owner or EventSystem count is invalid.");
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.GetComponents<Component>().Any(component => component == null))
                throw new InvalidOperationException($"{scene.path} contains a missing script at {child.name}.");
        }
    }

    private static void WireBattleStartupRestore()
    {
        Scene battleScene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
        BattleMapBootstrap[] bootstraps = UnityEngine.Object.FindObjectsByType<BattleMapBootstrap>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        SaveSystemBehaviour[] saveSystems = UnityEngine.Object.FindObjectsByType<SaveSystemBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (bootstraps.Length != 1 || saveSystems.Length != 1)
        {
            throw new InvalidOperationException(
                $"Raw battle requires one BattleMapBootstrap and SaveSystemBehaviour; " +
                $"found {bootstraps.Length} and {saveSystems.Length}.");
        }
        SerializedObject serializedBootstrap = new SerializedObject(bootstraps[0]);
        SerializedProperty startupSave = serializedBootstrap.FindProperty("startupSaveSystem");
        if (startupSave == null)
            throw new InvalidOperationException("BattleMapBootstrap startup save field is missing.");
        startupSave.objectReferenceValue = saveSystems[0];
        serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(battleScene);
        EditorSceneManager.SaveScene(battleScene);
    }
}
#endif
