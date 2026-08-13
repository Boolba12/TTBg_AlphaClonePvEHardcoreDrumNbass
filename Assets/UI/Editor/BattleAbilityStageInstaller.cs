#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BattleAbilityStageInstaller
{
    private const string ScenePath = "Assets/Scenes/Raw_Alpha_BattleMode.unity";
    private const string HudPrefabPath = "Assets/UI/Prefabs/Battle/BattleHUD.prefab";
    private const string ThemePath = "Assets/UI/Themes/PurgatoryUITheme.asset";
    private const string DataRoot = "Assets/GameData/Abilities";
    private const string PreviewPath = "Assets/3DModel/Test/WP_Sword_01_Preview.png";
    private const string ModelPath = "Assets/3DModel/Test/WP_Sword_01.fbx";
    private const string PowerPath = DataRoot + "/DEV_PowerStrike.asset";
    private const string SweepPath = DataRoot + "/DEV_SweepingBlow.asset";
    private const string RallyPath = DataRoot + "/DEV_Rally.asset";

    [MenuItem("Tools/Purgatory UI/Apply Battle Ability Stage (Non-Destructive)")]
    public static void ApplyStage()
    {
        EnsureFolder("Assets/GameData", "Abilities");
        PurgatoryUITheme theme = AssetDatabase.LoadAssetAtPath<PurgatoryUITheme>(ThemePath);
        Sprite swordPreview = AssetDatabase.LoadAssetAtPath<Sprite>(PreviewPath);
        GameObject swordModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Require(theme != null && swordPreview != null && swordModel != null,
            "Ability stage presentation dependencies are missing.");

        AttackDefinition powerEffect = LoadOrCreate<AttackDefinition>(
            DataRoot + "/DEV_PowerStrike_Attack.asset");
        powerEffect.ConfigureDevelopmentAbilityEffect(
            "dev-power-strike-attack",
            "Power Strike Attack",
            4,
            3,
            0.85f,
            SquadDamageDistribution.SingleTarget,
            swordPreview,
            swordModel,
            EquipmentSlotKind.CommanderWeapon);
        AttackDefinition sweepEffect = LoadOrCreate<AttackDefinition>(
            DataRoot + "/DEV_SweepingBlow_Attack.asset");
        sweepEffect.ConfigureDevelopmentAbilityEffect(
            "dev-sweeping-blow-attack",
            "Sweeping Blow Attack",
            5,
            4,
            0.75f,
            SquadDamageDistribution.Area,
            swordPreview,
            swordModel);

        AbilityDefinition power = LoadOrCreate<AbilityDefinition>(
            PowerPath);
        power.ConfigureDevelopmentAttack(
            "DEV_PowerStrike",
            "Power Strike",
            "A committed heavy physical strike against one formation member.",
            3,
            1,
            Key.Digit1,
            powerEffect,
            swordPreview);
        AbilityDefinition sweep = LoadOrCreate<AbilityDefinition>(
            SweepPath);
        sweep.ConfigureDevelopmentAttack(
            "DEV_SweepingBlow",
            "Sweeping Blow",
            "Physical damage propagates through members of one target formation.",
            4,
            2,
            Key.Digit2,
            sweepEffect,
            swordPreview);
        AbilityDefinition rally = LoadOrCreate<AbilityDefinition>(
            RallyPath);
        rally.ConfigureDevelopmentRally(
            "DEV_Rally",
            "Rally",
            "Restore morale to the active squad without changing health.",
            2,
            2,
            20f,
            Key.Digit3,
            theme.IconPlaceholderSprite);
        foreach (UnityEngine.Object asset in new UnityEngine.Object[]
                 { powerEffect, sweepEffect, power, sweep, rally })
            EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        UpgradeHudPrefab(theme, new[] { power, sweep, rally });
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        power = ReloadPersistent<AbilityDefinition>(PowerPath);
        sweep = ReloadPersistent<AbilityDefinition>(SweepPath);
        rally = ReloadPersistent<AbilityDefinition>(RallyPath);
        WireScene(new[] { power, sweep, rally });
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "BattleAbilityStageInstaller: Power Strike, Sweeping Blow, Rally, " +
            "shared command mode, runtime cooldowns, and production HUD wiring installed.");
    }

    private static void UpgradeHudPrefab(
        PurgatoryUITheme theme,
        IReadOnlyList<AbilityDefinition> definitions)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
        try
        {
            Transform perks = FindDescendant(root.transform, "CommanderPerks");
            Transform controls = perks != null ? perks.Find("Controls") : null;
            Require(controls != null, "Battle HUD CommanderPerks/Controls is missing.");
            List<BattleActionControlView> existing = controls
                .GetComponentsInChildren<BattleActionControlView>(true)
                .OrderBy(control => control.transform.GetSiblingIndex())
                .ToList();
            Require(existing.Count >= 2,
                "CommanderPerks requires two existing staged controls.");
            existing[0].gameObject.name = "PowerStrike";
            existing[1].gameObject.name = "SweepingBlow";
            BattleActionControlView rally = existing.FirstOrDefault(
                control => control.gameObject.name == "Rally");
            if (rally == null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(
                    existing[1].gameObject,
                    controls);
                clone.name = "Rally";
                rally = clone.GetComponent<BattleActionControlView>();
            }
            BattleActionControlView[] abilityControls =
                { existing[0], existing[1], rally };
            for (int i = 0; i < abilityControls.Length; i++)
            {
                AbilityDefinition definition = definitions[i];
                abilityControls[i].RenderCommand(
                    definition.DisplayName,
                    (i + 1).ToString(),
                    $"{definition.ActionPointCost} AP",
                    false,
                    false,
                    "Battle not started.",
                    definition.Icon ?? theme.IconPlaceholderSprite);
            }

            BattleActionBarView actionBar =
                root.GetComponentInChildren<BattleActionBarView>(true);
            AppendActionButton(actionBar, rally.Button);
            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WireScene(IReadOnlyList<AbilityDefinition> definitions)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        definitions = new[]
        {
            ReloadPersistent<AbilityDefinition>(PowerPath),
            ReloadPersistent<AbilityDefinition>(SweepPath),
            ReloadPersistent<AbilityDefinition>(RallyPath)
        };
        SquadBattleTacticalBootstrap tactical = RequireExactlyOne<SquadBattleTacticalBootstrap>();
        GameObject root = tactical.gameObject;
        SquadBattleBootstrap bootstrap = RequireExactlyOne<SquadBattleBootstrap>();
        BattleHUDController hud = RequireExactlyOne<BattleHUDController>();
        GridOccupancyService occupancy = RequireOnRoot<GridOccupancyService>(root);
        BattleSquadSelectionController selection = RequireOnRoot<BattleSquadSelectionController>(root);
        BattleTurnController turns = RequireOnRoot<BattleTurnController>(root);
        SquadMovementService movement = RequireOnRoot<SquadMovementService>(root);
        BattleCommandModeController modes = RequireOnRoot<BattleCommandModeController>(root);
        BattleAttackService attacks = RequireOnRoot<BattleAttackService>(root);
        MovementCommandController movementCommands = RequireOnRoot<MovementCommandController>(root);
        AttackCommandController attackCommands = RequireOnRoot<AttackCommandController>(root);
        BattleCompletionController completion = RequireOnRoot<BattleCompletionController>(root);
        BattleAbilityService abilityService = GetOrAdd<BattleAbilityService>(root);
        AbilityCommandController abilityCommands = GetOrAdd<AbilityCommandController>(root);
        Camera camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)
            .Single(candidate => candidate.CompareTag("MainCamera"));
        BattleActionControlView[] controls = definitions
            .Select(definition => FindAction(hud, definition.StableId switch
            {
                "DEV_PowerStrike" => "PowerStrike",
                "DEV_SweepingBlow" => "SweepingBlow",
                _ => "Rally"
            }))
            .ToArray();
        Require(controls.All(control => control != null),
            "Raw scene is missing one or more serialized ability controls.");

        abilityService.Configure(
            bootstrap,
            turns,
            selection,
            movement,
            attacks,
            completion,
            definitions);
        SerializedObject abilityServiceSerialized = new SerializedObject(abilityService);
        SerializedProperty abilityArray = abilityServiceSerialized.FindProperty("abilities");
        abilityArray.arraySize = definitions.Count;
        for (int i = 0; i < definitions.Count; i++)
        {
            abilityArray.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
        }
        abilityServiceSerialized.ApplyModifiedPropertiesWithoutUndo();
        abilityCommands.Configure(
            bootstrap,
            selection,
            turns,
            movement,
            modes,
            abilityService,
            camera,
            hud,
            controls);
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
        PersistAbilityReferences(abilityService, definitions);
        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(abilityService);
        EditorUtility.SetDirty(abilityCommands);
        EditorUtility.SetDirty(completion);
        EditorUtility.SetDirty(tactical);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void PersistAbilityReferences(
        BattleAbilityService service,
        IReadOnlyList<AbilityDefinition> definitions)
    {
        SerializedObject serialized = new SerializedObject(service);
        serialized.Update();
        SerializedProperty array = serialized.FindProperty("abilities");
        array.arraySize = definitions.Count;
        for (int i = 0; i < definitions.Count; i++)
        {
            Require(definitions[i] != null && EditorUtility.IsPersistent(definitions[i]),
                $"Ability definition {i} is not a persistent Unity asset.");
            array.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        serialized.Update();
        for (int i = 0; i < definitions.Count; i++)
        {
            Require(array.GetArrayElementAtIndex(i).objectReferenceValue == definitions[i],
                $"Ability definition {i} did not persist in the scene service.");
        }
        EditorUtility.SetDirty(service);
    }

    private static void AppendActionButton(BattleActionBarView actionBar, Button button)
    {
        Require(actionBar != null && button != null,
            "BattleActionBarView or Rally button is missing.");
        SerializedObject serialized = new SerializedObject(actionBar);
        SerializedProperty buttons = serialized.FindProperty("actionButtons");
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

    private static BattleActionControlView FindAction(BattleHUDController hud, string name) =>
        hud.GetComponentsInChildren<BattleActionControlView>(true)
            .SingleOrDefault(control => control.gameObject.name == name);

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

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null && File.Exists(path))
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            asset = AssetDatabase.LoadMainAssetAtPath(path) as T;
        }
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssetIfDirty(asset);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        T persistent = AssetDatabase.LoadMainAssetAtPath(path) as T;
        Require(persistent != null,
            $"Could not create or load persistent {typeof(T).Name} at {path}.");
        return persistent;
    }

    private static T ReloadPersistent<T>(string path) where T : ScriptableObject
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        T asset = AssetDatabase.LoadMainAssetAtPath(path) as T;
        Require(asset != null && EditorUtility.IsPersistent(asset),
            $"Could not reload persistent {typeof(T).Name} at {path}.");
        return asset;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component =>
        target.GetComponent<T>() ?? target.AddComponent<T>();

    private static T RequireOnRoot<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        Require(component != null, $"{target.name} is missing {typeof(T).Name}.");
        return component;
    }

    private static T RequireExactlyOne<T>() where T : UnityEngine.Object
    {
        T[] values = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        Require(values.Length == 1,
            $"Expected exactly one {typeof(T).Name}; found {values.Length}.");
        return values[0];
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
