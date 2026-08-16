#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Explicit, idempotent stage installer for the first ranged attack. It only
/// augments the existing combat service, tactical root, and HUD hierarchy.
/// </summary>
public static class RangedCombatStageInstaller
{
    private const string ScenePath = "Assets/Scenes/Raw_Alpha_BattleMode.unity";
    private const string HudPrefabPath = "Assets/UI/Prefabs/Battle/BattleHUD.prefab";
    private const string RangedAttackPath =
        "Assets/GameData/Combat/DEV_BasicPhysicalRangedAttack.asset";
    private const string MeleeAttackPath =
        "Assets/GameData/Combat/DEV_BasicPhysicalMeleeAttack.asset";
    private const string CombatRulesPath =
        "Assets/GameData/Combat/DEV_BattleCombatRules.asset";
    private const string PreviewPath =
        "Assets/3DModel/Test/WP_Estoc_03_Preview.png";

    [MenuItem("Tools/Purgatory UI/Apply Ranged Combat + LOS + Cover Stage (Non-Destructive)")]
    public static void ApplyStage()
    {
        AttackDefinition ranged = BuildRangedDefinition();
        BattleCombatRules rules = AssetDatabase.LoadAssetAtPath<BattleCombatRules>(
            CombatRulesPath);
        Require(rules != null, $"Combat rules are missing at {CombatRulesPath}.");
        rules.ConfigureDevelopmentCover(-0.20f, -0.40f);
        EditorUtility.SetDirty(rules);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        UpgradeHudPrefab(ReloadPersistent<AttackDefinition>(RangedAttackPath));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        WireScene(
            ReloadPersistent<AttackDefinition>(RangedAttackPath),
            ReloadPersistent<AttackDefinition>(MeleeAttackPath),
            ReloadPersistent<BattleCombatRules>(CombatRulesPath));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "RangedCombatStageInstaller: development ranged attack, deterministic " +
            "grid LOS, directional cover, shared combat pipeline, HUD control, and " +
            "explicit Raw scene references installed.");
    }

    private static AttackDefinition BuildRangedDefinition()
    {
        EnsureFolder("Assets/GameData", "Combat");
        AttackDefinition ranged = LoadOrCreate<AttackDefinition>(RangedAttackPath);
        Sprite preview = AssetDatabase.LoadAssetAtPath<Sprite>(PreviewPath);
        Require(preview != null, $"Ranged development preview is missing at {PreviewPath}.");
        ranged.ConfigureDevelopmentRanged(
            "dev-basic-physical-ranged",
            "Basic Ranged Attack",
            2,
            3,
            2,
            8,
            0.5f,
            preview,
            null);
        EditorUtility.SetDirty(ranged);
        return ranged;
    }

    private static void UpgradeHudPrefab(AttackDefinition ranged)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
        try
        {
            BattleActionControlView attack = root
                .GetComponentsInChildren<BattleActionControlView>(true)
                .SingleOrDefault(control => control.gameObject.name == "Attack");
            Require(attack != null, "Battle HUD Attack control is missing.");
            Transform parent = attack.transform.parent;
            BattleActionControlView rangedControl = parent
                .GetComponentsInChildren<BattleActionControlView>(true)
                .SingleOrDefault(control => control.gameObject.name == "Ranged");
            if (rangedControl == null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(
                    attack.gameObject,
                    parent);
                clone.name = "Ranged";
                rangedControl = clone.GetComponent<BattleActionControlView>();
                rangedControl.transform.SetSiblingIndex(
                    Mathf.Min(attack.transform.GetSiblingIndex() + 1, parent.childCount - 1));
            }

            rangedControl.RenderCommand(
                ranged.DisplayName,
                "R",
                $"{ranged.ActionPointCost} AP",
                false,
                false,
                "Battle not started.",
                ranged.PreviewSprite);
            BattleActionBarView actionBar =
                root.GetComponentInChildren<BattleActionBarView>(true);
            AppendActionButton(actionBar, rangedControl.Button);
            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WireScene(
        AttackDefinition ranged,
        AttackDefinition melee,
        BattleCombatRules rules)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BattleMapBootstrap mapBootstrap = RequireExactlyOne<BattleMapBootstrap>();
        MapGenerator mapGenerator = mapBootstrap.mapGenerator;
        MapRenderer mapRenderer = mapBootstrap.mapRenderer;
        Require(mapGenerator != null && mapRenderer != null,
            "BattleMapBootstrap canonical map references are missing.");

        SquadBattleTacticalBootstrap tactical = RequireExactlyOne<SquadBattleTacticalBootstrap>();
        GameObject root = tactical.gameObject;
        SquadBattleBootstrap squads = RequireExactlyOne<SquadBattleBootstrap>();
        BattleTurnController turns = RequireOnRoot<BattleTurnController>(root);
        BattleSquadSelectionController selection =
            RequireOnRoot<BattleSquadSelectionController>(root);
        GridOccupancyService occupancy = RequireOnRoot<GridOccupancyService>(root);
        SquadMovementService movement = RequireOnRoot<SquadMovementService>(root);
        BattleAttackService attacks = RequireOnRoot<BattleAttackService>(root);
        AttackCommandController attackCommands = RequireOnRoot<AttackCommandController>(root);
        BattleHUDController hud = RequireExactlyOne<BattleHUDController>();

        GridTacticalTerrainService terrain = GetOrAdd<GridTacticalTerrainService>(root);
        terrain.Configure(mapGenerator, BuildDevelopmentTerrain(mapGenerator));
        movement.Configure(
            mapGenerator,
            mapRenderer,
            occupancy,
            turns,
            terrain,
            movement.AllowDiagonalMovement,
            movement.MovementStepDuration);
        attacks.Configure(
            squads,
            turns,
            selection,
            movement,
            melee,
            ranged,
            rules,
            terrain,
            movement.AllowDiagonalMovement,
            42);

        AttackRangePreviewView rangePreview = GetOrAdd<AttackRangePreviewView>(root);
        ConfigureRangePreview(root, rangePreview, mapGenerator, mapRenderer);
        BattleActionControlView rangedControl = hud
            .GetComponentsInChildren<BattleActionControlView>(true)
            .SingleOrDefault(control => control.gameObject.name == "Ranged");
        Require(rangedControl != null, "Raw scene HUD Ranged control is missing.");
        attackCommands.ConfigureRanged(rangedControl, rangePreview);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(terrain);
        EditorUtility.SetDirty(movement);
        EditorUtility.SetDirty(attacks);
        EditorUtility.SetDirty(attackCommands);
        EditorUtility.SetDirty(rangePreview);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static IReadOnlyList<GridTacticalTerrainCellDefinition>
        BuildDevelopmentTerrain(MapGenerator generator)
    {
        int centerX = Mathf.Clamp(generator.width / 2, 2, generator.width - 3);
        int centerY = Mathf.Clamp(generator.height / 2, 2, generator.height - 3);
        return new[]
        {
            new GridTacticalTerrainCellDefinition(
                new Vector2Int(centerX, centerY), true, true, CoverType.Full),
            new GridTacticalTerrainCellDefinition(
                new Vector2Int(centerX + 1, centerY), true, false, CoverType.Half),
            new GridTacticalTerrainCellDefinition(
                new Vector2Int(centerX, centerY + 1), true, false, CoverType.Full)
        };
    }

    private static void ConfigureRangePreview(
        GameObject root,
        AttackRangePreviewView preview,
        MapGenerator generator,
        MapRenderer renderer)
    {
        Transform rangeRoot = root.transform.Find("RangedAttackPreview");
        if (rangeRoot == null)
        {
            GameObject child = new GameObject(
                "RangedAttackPreview",
                typeof(MeshFilter),
                typeof(MeshRenderer));
            child.transform.SetParent(root.transform, false);
            rangeRoot = child.transform;
        }
        MeshFilter filter = rangeRoot.GetComponent<MeshFilter>();
        if (filter == null)
            filter = rangeRoot.gameObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = rangeRoot.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = rangeRoot.gameObject.AddComponent<MeshRenderer>();

        Transform lineRoot = root.transform.Find("RangedLineOfSightPreview");
        if (lineRoot == null)
        {
            GameObject child = new GameObject("RangedLineOfSightPreview");
            child.transform.SetParent(root.transform, false);
            lineRoot = child.transform;
        }
        LineRenderer line = lineRoot.GetComponent<LineRenderer>();
        if (line == null)
            line = lineRoot.gameObject.AddComponent<LineRenderer>();
        LineRenderer movementLine = root.GetComponent<LineRenderer>();
        Material shared = movementLine != null ? movementLine.sharedMaterial : null;
        if (shared != null)
        {
            meshRenderer.sharedMaterial = shared;
            line.sharedMaterial = shared;
        }
        line.useWorldSpace = true;
        line.loop = false;
        line.startWidth = 0.055f;
        line.endWidth = 0.055f;
        line.numCapVertices = 2;
        line.enabled = false;
        preview.Configure(generator, renderer, filter, meshRenderer, line);
        EditorUtility.SetDirty(rangeRoot.gameObject);
        EditorUtility.SetDirty(lineRoot.gameObject);
    }

    private static void AppendActionButton(BattleActionBarView actionBar, Button button)
    {
        Require(actionBar != null && button != null,
            "BattleActionBarView or Ranged button is missing.");
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
        return asset;
    }

    private static T ReloadPersistent<T>(string path) where T : UnityEngine.Object
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
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
