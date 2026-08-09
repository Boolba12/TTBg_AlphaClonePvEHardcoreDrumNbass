#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Non-destructive stage upgrader. It preserves the existing HUD hierarchy and
/// only adds/configures the references required by the AA tactical foundation.
/// </summary>
public static class BattleAATacticalStageInstaller
{
    private const string TestRoot = "Assets/3DModel/Test";
    private const string ScenePath = "Assets/Scenes/Raw_Alpha_BattleMode.unity";
    private const string SquadPrefabPath = "Assets/Prefabs/Squads/SquadBattle.prefab";
    private const string PlaceholderPath =
        "Assets/Prefabs/Squads/DevelopmentSquadMemberPlaceholder.prefab";
    private const string PresentationRoot = "Assets/Prefabs/Squads/Presentation";
    private const string PlayerPresentationPath =
        PresentationRoot + "/DevelopmentPlayerFormation.asset";
    private const string EnemyPresentationPath =
        PresentationRoot + "/DevelopmentEnemyFormation.asset";
    private const string HudPrefabPath = "Assets/UI/Prefabs/Battle/BattleHUD.prefab";
    private const string InitiativePrefabPath =
        "Assets/UI/Prefabs/Components/InitiativeEntry.prefab";
    private const string ThemePath = "Assets/UI/Themes/PurgatoryUITheme.asset";
    private const string CatalogPath =
        "Assets/UI/Presentation/DevelopmentItemPresentationCatalog.asset";
    private const string CombatDataRoot = "Assets/GameData/Combat";
    private const string AttackDefinitionPath =
        CombatDataRoot + "/DEV_BasicPhysicalMeleeAttack.asset";
    private const string CombatRulesPath =
        CombatDataRoot + "/DEV_BattleCombatRules.asset";

    private static readonly WeaponAsset[] Weapons =
    {
        new WeaponAsset("Weapon_07", "Weapon 07", "test-weapon-07"),
        new WeaponAsset("WP_Dagger_01", "Dagger 01", "test-wp-dagger-01"),
        new WeaponAsset("WP_Estoc_03", "Estoc 03", "test-wp-estoc-03"),
        new WeaponAsset("WP_Falchion_04", "Falchion 04", "test-wp-falchion-04"),
        new WeaponAsset("WP_Greatsword_02", "Greatsword 02", "test-wp-greatsword-02"),
        new WeaponAsset("WP_Longsword_05", "Longsword 05", "test-wp-longsword-05"),
        new WeaponAsset("WP_Mace_04", "Mace 04", "test-wp-mace-04"),
        new WeaponAsset("WP_Mace_10", "Mace 10", "test-wp-mace-10"),
        new WeaponAsset("WP_Sword_01", "Sword 01", "test-wp-sword-01"),
        new WeaponAsset("WP_Sword_02", "Sword 02", "test-wp-sword-02"),
        new WeaponAsset("WP_Sword_03", "Sword 03", "test-wp-sword-03"),
        new WeaponAsset("WP_Sword_04", "Sword 04", "test-wp-sword-04")
    };

    [MenuItem("Tools/Purgatory UI/Apply AA Tactical Stage (Non-Destructive)")]
    public static void ApplyFromMenu()
    {
        ApplyInternal();
    }

    public static void ApplyForAutomation()
    {
        ApplyInternal();
    }

    private static void ApplyInternal()
    {
        ConfigureTestAssetImporters();
        BuildTestWeaponCatalog();
        (AttackDefinition attack, BattleCombatRules rules) = BuildCombatDefinitions();
        (SquadFormationPresentation player, SquadFormationPresentation enemy) =
            BuildFormationPresentations();
        UpgradeSquadPrefab();
        UpgradeInitiativeEntryPrefab();
        UpgradeBattleHudPrefab();
        UpgradeBattleScene(player, enemy, attack, rules);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "BattleAATacticalStageInstaller: non-destructive AA tactical stage applied; " +
            "existing HUD hierarchy and legacy combat objects were preserved.");
    }

    private static void ConfigureTestAssetImporters()
    {
        foreach (WeaponAsset weapon in Weapons)
        {
            string modelPath = $"{TestRoot}/{weapon.FileStem}.fbx";
            ModelImporter modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            Require(modelImporter != null, $"ModelImporter missing for {modelPath}.");
            if (modelImporter.importAnimation)
            {
                modelImporter.importAnimation = false;
                modelImporter.SaveAndReimport();
            }

            ConfigureSpriteImporter($"{TestRoot}/{weapon.FileStem}_Preview.png");
            string frontPath = $"{TestRoot}/{weapon.FileStem}_Front.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(frontPath) != null)
                ConfigureSpriteImporter(frontPath);
        }
    }

    private static void ConfigureSpriteImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        Require(importer != null, $"TextureImporter missing for {assetPath}.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
    }

    private static void BuildTestWeaponCatalog()
    {
        ItemPresentationCatalog catalog =
            AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(CatalogPath);
        Require(catalog != null, $"Item presentation catalog missing at {CatalogPath}.");

        List<ItemPresentationRecord> records = new List<ItemPresentationRecord>();
        ItemPresentationRecord legacy = catalog.Entries.FirstOrDefault(
            entry => entry != null && entry.StableId == "dev-legacy-test-weapon");
        if (legacy != null)
            records.Add(legacy);

        foreach (WeaponAsset weapon in Weapons)
        {
            Sprite preview = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"{TestRoot}/{weapon.FileStem}_Preview.png");
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{TestRoot}/{weapon.FileStem}.fbx");
            Require(preview != null && model != null,
                $"Preview or FBX model missing for {weapon.FileStem}.");

            ItemPresentationRecord record = new ItemPresentationRecord();
            record.ConfigureDevelopment(
                weapon.StableId,
                weapon.DisplayName,
                preview,
                model,
                ItemPresentationCategory.Weapon,
                "Audited static low-poly weapon: one mesh, UV0, internal PBR material colors, " +
                "no rig, no animation, and no external texture dependency. Presentation-only in this stage.",
                false);
            records.Add(record);
        }

        catalog.ReplaceDevelopmentEntries(records);
        EditorUtility.SetDirty(catalog);
    }

    private static (SquadFormationPresentation, SquadFormationPresentation)
        BuildFormationPresentations()
    {
        EnsureFolder(PresentationRoot);
        GameObject placeholder = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderPath);
        Require(placeholder != null, $"Development formation placeholder missing at {PlaceholderPath}.");

        SquadFormationPresentation player = GetOrCreatePresentation(
            PlayerPresentationPath,
            "development-player-formation",
            placeholder);
        SquadFormationPresentation enemy = GetOrCreatePresentation(
            EnemyPresentationPath,
            "development-enemy-formation",
            placeholder);
        return (player, enemy);
    }

    private static (AttackDefinition, BattleCombatRules) BuildCombatDefinitions()
    {
        EnsureFolder(CombatDataRoot);
        AttackDefinition attack =
            AssetDatabase.LoadAssetAtPath<AttackDefinition>(AttackDefinitionPath);
        if (attack == null)
        {
            attack = ScriptableObject.CreateInstance<AttackDefinition>();
            AssetDatabase.CreateAsset(attack, AttackDefinitionPath);
        }
        Sprite preview = AssetDatabase.LoadAssetAtPath<Sprite>(
            TestRoot + "/WP_Sword_01_Preview.png");
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
            TestRoot + "/WP_Sword_01.fbx");
        Require(preview != null && model != null,
            "Development Sword 01 preview or model is missing.");
        attack.ConfigureDevelopment(
            "dev-basic-physical-melee",
            "Basic Physical Attack",
            2,
            2,
            0.5f,
            preview,
            model);
        EditorUtility.SetDirty(attack);

        BattleCombatRules rules =
            AssetDatabase.LoadAssetAtPath<BattleCombatRules>(CombatRulesPath);
        if (rules == null)
        {
            rules = ScriptableObject.CreateInstance<BattleCombatRules>();
            AssetDatabase.CreateAsset(rules, CombatRulesPath);
        }
        rules.ConfigureDevelopment(0.75f, 0.05f, 0.95f, 0.8f, 1);
        EditorUtility.SetDirty(rules);
        return (attack, rules);
    }

    private static SquadFormationPresentation GetOrCreatePresentation(
        string path,
        string stableId,
        GameObject placeholder)
    {
        SquadFormationPresentation presentation =
            AssetDatabase.LoadAssetAtPath<SquadFormationPresentation>(path);
        if (presentation == null)
        {
            presentation = ScriptableObject.CreateInstance<SquadFormationPresentation>();
            AssetDatabase.CreateAsset(presentation, path);
        }
        presentation.ConfigureDevelopment(stableId, placeholder, placeholder);
        EditorUtility.SetDirty(presentation);
        return presentation;
    }

    private static void UpgradeSquadPrefab()
    {
        PurgatoryUITheme theme = AssetDatabase.LoadAssetAtPath<PurgatoryUITheme>(ThemePath);
        Require(theme != null, $"Purgatory UI theme missing at {ThemePath}.");
        GameObject root = PrefabUtility.LoadPrefabContents(SquadPrefabPath);
        try
        {
            SquadBattleController controller = root.GetComponent<SquadBattleController>();
            SquadGridAnchor anchor = root.GetComponent<SquadGridAnchor>();
            SquadFormationView formation = root.GetComponent<SquadFormationView>();
            Require(controller != null && anchor != null,
                "SquadBattle prefab is missing its controller or grid anchor.");

            BoxCollider collider = root.GetComponent<BoxCollider>();
            if (collider == null)
                collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 0.55f, 0f);
            collider.size = new Vector3(0.88f, 1.1f, 0.88f);

            LineRenderer ring = root.GetComponent<LineRenderer>();
            if (ring == null)
                ring = root.AddComponent<LineRenderer>();
            SquadSelectionView selectionView = root.GetComponent<SquadSelectionView>();
            if (selectionView == null)
                selectionView = root.AddComponent<SquadSelectionView>();
            selectionView.Configure(theme, ring);
            SquadSelectionTarget target = root.GetComponent<SquadSelectionTarget>();
            if (target == null)
                target = root.AddComponent<SquadSelectionTarget>();
            target.Configure(controller, selectionView);

            SquadAttackTargetView attackView = root.GetComponent<SquadAttackTargetView>();
            if (attackView == null)
                attackView = root.AddComponent<SquadAttackTargetView>();
            LineRenderer attackRing = attackView.TargetRing;
            if (attackRing == null)
            {
                Transform ringTransform = root.transform.Find("AttackTargetRing");
                GameObject ringObject = ringTransform != null
                    ? ringTransform.gameObject
                    : new GameObject("AttackTargetRing");
                if (ringTransform == null)
                    ringObject.transform.SetParent(root.transform, false);
                attackRing = ringObject.GetComponent<LineRenderer>();
                if (attackRing == null)
                    attackRing = ringObject.AddComponent<LineRenderer>();
            }
            attackRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            attackRing.receiveShadows = false;
            attackRing.enabled = false;

            Transform feedbackTransform = root.transform.Find("AttackFeedback");
            GameObject feedbackObject = feedbackTransform != null
                ? feedbackTransform.gameObject
                : new GameObject("AttackFeedback");
            if (feedbackTransform == null)
                feedbackObject.transform.SetParent(root.transform, false);
            TextMeshPro feedback = feedbackObject.GetComponent<TextMeshPro>();
            if (feedback == null)
                feedback = feedbackObject.AddComponent<TextMeshPro>();
            feedback.text = string.Empty;
            feedback.font = theme.PrimaryFont;
            feedback.fontSize = 2.5f;
            feedback.alignment = TextAlignmentOptions.Center;
            feedback.raycastTarget = false;
            feedback.rectTransform.sizeDelta = new Vector2(4f, 1f);
            feedback.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            feedback.gameObject.SetActive(false);
            attackView.Configure(theme, attackRing, feedback);

            SquadAttackTarget attackTarget = root.GetComponent<SquadAttackTarget>();
            if (attackTarget == null)
                attackTarget = root.AddComponent<SquadAttackTarget>();
            attackTarget.Configure(controller, attackView);
            controller.Configure(anchor, formation, target, attackTarget);

            PrefabUtility.SaveAsPrefabAsset(root, SquadPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpgradeInitiativeEntryPrefab()
    {
        PurgatoryUITheme theme = AssetDatabase.LoadAssetAtPath<PurgatoryUITheme>(ThemePath);
        GameObject root = PrefabUtility.LoadPrefabContents(InitiativePrefabPath);
        try
        {
            InitiativeEntryView view = root.GetComponent<InitiativeEntryView>();
            Require(view != null && theme != null,
                "InitiativeEntry prefab or PurgatoryUITheme is missing.");

            Transform activeTransform = root.transform.Find("ActiveIndicator");
            GameObject activeObject = activeTransform != null
                ? activeTransform.gameObject
                : NewUIObject("ActiveIndicator", root.transform);
            Image activeIndicator = activeObject.GetComponent<Image>();
            if (activeIndicator == null)
                activeIndicator = activeObject.AddComponent<Image>();
            activeIndicator.raycastTarget = false;
            SetAnchors(activeObject.GetComponent<RectTransform>(),
                new Vector2(0.405f, 0.16f), new Vector2(0.425f, 0.84f));

            Transform controlTransform = root.transform.Find("ControlType");
            GameObject controlObject = controlTransform != null
                ? controlTransform.gameObject
                : NewUIObject("ControlType", root.transform);
            TextMeshProUGUI controlLabel = controlObject.GetComponent<TextMeshProUGUI>();
            if (controlLabel == null)
                controlLabel = controlObject.AddComponent<TextMeshProUGUI>();
            controlLabel.text = "HUMAN";
            controlLabel.font = theme.PrimaryFont;
            controlLabel.fontSize = Mathf.Max(11f, theme.CaptionSize - 5f);
            controlLabel.alignment = TextAlignmentOptions.MidlineLeft;
            controlLabel.raycastTarget = false;
            SetAnchors(controlObject.GetComponent<RectTransform>(),
                new Vector2(0.43f, 0.08f), new Vector2(0.82f, 0.40f));

            view.ConfigureStateVisuals(activeIndicator, controlLabel);
            PrefabUtility.SaveAsPrefabAsset(root, InitiativePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpgradeBattleHudPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
        try
        {
            Transform hudLayer = root.transform.Find("HUDLayer");
            Require(hudLayer != null, "BattleHUD prefab is missing HUDLayer.");
            Transform statusPanel = FindDescendant(hudLayer, "SelectedSquadPanel");
            Require(statusPanel != null, "BattleHUD prefab is missing SelectedSquadPanel.");
            SetAnchors(statusPanel.GetComponent<RectTransform>(),
                new Vector2(0f, 0.535f), new Vector2(0.25f, 0.99f));

            BattleSquadStatusView statusView =
                statusPanel.GetComponent<BattleSquadStatusView>();
            Transform content = statusPanel.Find("Content");
            Require(statusView != null && content != null,
                "SelectedSquadPanel is missing its view or Content root.");
            CanvasGroup canvasGroup = content.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = content.gameObject.AddComponent<CanvasGroup>();
            SerializedObject statusSerialized = new SerializedObject(statusView);
            statusSerialized.FindProperty("contentCanvasGroup").objectReferenceValue = canvasGroup;
            statusSerialized.ApplyModifiedPropertiesWithoutUndo();

            AbilityDetailsPanelView abilityDetails =
                root.GetComponentInChildren<AbilityDetailsPanelView>(true);
            Require(abilityDetails != null,
                "BattleHUD prefab is missing its AbilityDetailsPanelView.");
            Transform portraitTransform = abilityDetails.transform.Find("TargetPortrait");
            GameObject portraitObject = portraitTransform != null
                ? portraitTransform.gameObject
                : NewUIObject("TargetPortrait", abilityDetails.transform);
            Image targetPortrait = portraitObject.GetComponent<Image>();
            if (targetPortrait == null)
                targetPortrait = portraitObject.AddComponent<Image>();
            targetPortrait.raycastTarget = false;
            targetPortrait.preserveAspect = true;
            LayoutElement portraitLayout = portraitObject.GetComponent<LayoutElement>();
            if (portraitLayout == null)
                portraitLayout = portraitObject.AddComponent<LayoutElement>();
            portraitLayout.preferredHeight = 52f;
            portraitLayout.minHeight = 44f;
            portraitObject.transform.SetSiblingIndex(1);
            SerializedObject abilitySerialized = new SerializedObject(abilityDetails);
            abilitySerialized.FindProperty("icon").objectReferenceValue = targetPortrait;
            abilitySerialized.ApplyModifiedPropertiesWithoutUndo();

            Transform basicActions = FindDescendant(hudLayer, "BasicActions");
            Transform move = FindDescendant(basicActions, "Move");
            Require(basicActions != null && move != null,
                "BattleHUD prefab is missing BasicActions/Move.");
            Transform endTurn = FindDescendant(basicActions, "EndTurn");
            if (endTurn == null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(move.gameObject, move.parent);
                clone.name = "EndTurn";
                endTurn = clone.transform;
            }

            BattleActionControlView endTurnView = endTurn.GetComponent<BattleActionControlView>();
            Require(endTurnView != null, "Cloned EndTurn action has no BattleActionControlView.");
            endTurnView.RenderPlaceholder("End Turn", "Space", "AP —");
            AppendActionButton(root.GetComponentInChildren<BattleActionBarView>(true),
                endTurnView.Button);

            root.GetComponent<RectTransform>().localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpgradeBattleScene(
        SquadFormationPresentation playerPresentation,
        SquadFormationPresentation enemyPresentation,
        AttackDefinition attack,
        BattleCombatRules rules)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BattleMapBootstrap mapBootstrap = RequireExactlyOne<BattleMapBootstrap>();
        SquadBattleBootstrap squadBootstrap = RequireExactlyOne<SquadBattleBootstrap>();
        MapGenerator mapGenerator = mapBootstrap.mapGenerator;
        MapRenderer mapRenderer = mapBootstrap.mapRenderer;
        Require(mapGenerator != null && mapRenderer != null,
            "BattleMapBootstrap has no canonical serialized MapGenerator/MapRenderer pair.");
        BattleHUDController hud = RequireExactlyOne<BattleHUDController>();
        Camera[] mainCameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include)
            .Where(candidate => candidate.CompareTag("MainCamera"))
            .ToArray();
        Require(mainCameras.Length == 1,
            $"Expected exactly one MainCamera-tagged Camera; found {mainCameras.Length}.");
        Camera camera = mainCameras[0];

        SerializedObject bootstrapSerialized = new SerializedObject(squadBootstrap);
        bootstrapSerialized.FindProperty("playerFormationPresentation").objectReferenceValue =
            playerPresentation;
        bootstrapSerialized.FindProperty("enemyFormationPresentation").objectReferenceValue =
            enemyPresentation;
        ConfigureDevelopmentCombatStats(
            bootstrapSerialized.FindProperty("developmentPlayerSquad"),
            0.10f,
            0.05f,
            0.15f,
            1.5f,
            0.10f);
        ConfigureDevelopmentCombatStats(
            bootstrapSerialized.FindProperty("developmentEnemySquad"),
            0.05f,
            0.05f,
            0.05f,
            1.5f,
            0.15f);
        bootstrapSerialized.ApplyModifiedPropertiesWithoutUndo();

        SquadBattleTacticalBootstrap[] existingTactical =
            UnityEngine.Object.FindObjectsByType<SquadBattleTacticalBootstrap>(
                FindObjectsInactive.Include);
        Require(existingTactical.Length <= 1,
            $"Scene contains {existingTactical.Length} tactical bootstraps; expected at most one.");
        GameObject root = existingTactical.Length == 1
            ? existingTactical[0].gameObject
            : new GameObject("BattleTacticalRuntime");
        if (existingTactical.Length == 0)
            SceneManager.MoveGameObjectToScene(root, scene);

        GridOccupancyService occupancy = GetOrAdd<GridOccupancyService>(root);
        BattleSquadSelectionController selection =
            GetOrAdd<BattleSquadSelectionController>(root);
        BattleTurnController turns = GetOrAdd<BattleTurnController>(root);
        SquadMovementService movement = GetOrAdd<SquadMovementService>(root);
        BattleCommandModeController commandMode =
            GetOrAdd<BattleCommandModeController>(root);
        BattleAttackService attackService = GetOrAdd<BattleAttackService>(root);
        SquadPathPreviewView preview = GetOrAdd<SquadPathPreviewView>(root);
        LineRenderer pathLine = root.GetComponent<LineRenderer>();
        if (pathLine == null)
            pathLine = root.AddComponent<LineRenderer>();
        MovementCommandController commands = GetOrAdd<MovementCommandController>(root);
        AttackCommandController attackCommands = GetOrAdd<AttackCommandController>(root);
        SquadBattleTacticalBootstrap tactical = GetOrAdd<SquadBattleTacticalBootstrap>(root);
        BattleCompletionController completion = root.GetComponent<BattleCompletionController>();
        BattleAbilityService abilityService = root.GetComponent<BattleAbilityService>();
        AbilityCommandController abilityCommands = root.GetComponent<AbilityCommandController>();

        BattleActionControlView moveAction = FindAction(hud, "Move");
        BattleActionControlView attackAction = FindAction(hud, "Attack");
        BattleActionControlView endTurnAction = FindAction(hud, "EndTurn");
        Require(moveAction != null && attackAction != null && endTurnAction != null,
            "Raw scene HUD is missing Move, Attack, or EndTurn.");

        selection.Configure(squadBootstrap, camera);
        turns.Configure(squadBootstrap, true, 0.2f);
        movement.Configure(mapGenerator, mapRenderer, occupancy, turns, true, 0.1f);
        preview.Configure(pathLine);
        commands.Configure(selection, turns, movement, commandMode, preview, mapGenerator,
            mapRenderer, camera, moveAction, endTurnAction);
        attackService.Configure(
            squadBootstrap,
            turns,
            selection,
            movement,
            attack,
            rules,
            movement.AllowDiagonalMovement,
            42);
        attackCommands.Configure(
            squadBootstrap,
            selection,
            turns,
            movement,
            commandMode,
            attackService,
            camera,
            attackAction,
            hud);
        tactical.Configure(
            squadBootstrap,
            occupancy,
            selection,
            turns,
            movement,
            commandMode,
            attackService,
            commands,
            attackCommands,
            completion,
            abilityService,
            abilityCommands);
        hud.ConfigureRuntimeState(selection, turns);

        EditorUtility.SetDirty(squadBootstrap);
        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureDevelopmentCombatStats(
        SerializedProperty squad,
        float accuracy,
        float evasion,
        float criticalChance,
        float criticalDamage,
        float physicalArmor)
    {
        Require(squad != null, "Development squad serialization is missing.");
        SerializedProperty stats = squad.FindPropertyRelative("commander")
            ?.FindPropertyRelative("baseStats");
        Require(stats != null, "Development commander base stats are missing.");
        stats.FindPropertyRelative("accuracy").floatValue = accuracy;
        stats.FindPropertyRelative("evasion").floatValue = evasion;
        stats.FindPropertyRelative("criticalChance").floatValue = criticalChance;
        stats.FindPropertyRelative("criticalDamage").floatValue = criticalDamage;
        stats.FindPropertyRelative("physicalArmor").floatValue = physicalArmor;
    }

    private static void AppendActionButton(BattleActionBarView actionBar, Button button)
    {
        Require(actionBar != null && button != null,
            "BattleActionBarView or EndTurn button is missing.");
        SerializedObject serialized = new SerializedObject(actionBar);
        SerializedProperty buttons = serialized.FindProperty("actionButtons");
        Require(buttons != null && buttons.isArray,
            "BattleActionBarView actionButtons serialization changed unexpectedly.");
        for (int i = 0; i < buttons.arraySize; i++)
        {
            if (buttons.GetArrayElementAtIndex(i).objectReferenceValue == button)
                return;
        }
        int index = buttons.arraySize;
        buttons.InsertArrayElementAtIndex(index);
        buttons.GetArrayElementAtIndex(index).objectReferenceValue = button;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static BattleActionControlView FindAction(BattleHUDController hud, string name)
    {
        return hud.GetComponentsInChildren<BattleActionControlView>(true)
            .FirstOrDefault(action => action.gameObject.name == name);
    }

    private static T RequireExactlyOne<T>() where T : UnityEngine.Object
    {
        T[] found = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include);
        Require(found.Length == 1,
            $"Expected exactly one {typeof(T).Name} in {ScenePath}; found {found.Length}.");
        return found[0];
    }

    private static T GetOrAdd<T>(GameObject root) where T : Component
    {
        T[] existing = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include);
        Require(existing.Length <= 1,
            $"Scene contains {existing.Length} {typeof(T).Name} components; expected at most one.");
        return existing.Length == 1 ? existing[0] : root.AddComponent<T>();
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null)
            return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child;
        }
        return null;
    }

    private static GameObject NewUIObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        Require(rect != null, "Expected a RectTransform while upgrading UI.");
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct WeaponAsset
    {
        public WeaponAsset(string fileStem, string displayName, string stableId)
        {
            FileStem = fileStem;
            DisplayName = displayName;
            StableId = stableId;
        }

        public string FileStem { get; }
        public string DisplayName { get; }
        public string StableId { get; }
    }
}
#endif
