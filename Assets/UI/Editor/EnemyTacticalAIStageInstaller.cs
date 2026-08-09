#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnemyTacticalAIStageInstaller
{
    private const string ScenePath = "Assets/Scenes/Raw_Alpha_BattleMode.unity";

    [MenuItem("Tools/Purgatory Battle/Apply Enemy Tactical AI v0 (Non-Destructive)")]
    public static void ApplyStage()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SquadBattleTacticalBootstrap tactical =
            RequireExactlyOne<SquadBattleTacticalBootstrap>();
        GameObject root = tactical.gameObject;
        SquadBattleBootstrap squads = RequireExactlyOne<SquadBattleBootstrap>();
        BattleMapBootstrap mapBootstrap = RequireExactlyOne<BattleMapBootstrap>();
        Require(mapBootstrap.mapGenerator != null && mapBootstrap.mapRenderer != null,
            "Canonical BattleMapBootstrap map references are missing.");

        GridOccupancyService occupancy = RequireOnRoot<GridOccupancyService>(root);
        BattleSquadSelectionController selection =
            RequireOnRoot<BattleSquadSelectionController>(root);
        BattleTurnController turns = RequireOnRoot<BattleTurnController>(root);
        SquadMovementService movement = RequireOnRoot<SquadMovementService>(root);
        BattleCommandModeController modes =
            RequireOnRoot<BattleCommandModeController>(root);
        BattleAttackService attacks = RequireOnRoot<BattleAttackService>(root);
        MovementCommandController movementCommands =
            RequireOnRoot<MovementCommandController>(root);
        AttackCommandController attackCommands =
            RequireOnRoot<AttackCommandController>(root);
        BattleCompletionController completion =
            RequireOnRoot<BattleCompletionController>(root);
        BattleAbilityService abilities = RequireOnRoot<BattleAbilityService>(root);
        AbilityCommandController abilityCommands =
            RequireOnRoot<AbilityCommandController>(root);
        TacticalCameraController camera =
            RequireExactlyOne<TacticalCameraController>();
        EnemyTacticalAIController enemyAI =
            GetOrAdd<EnemyTacticalAIController>(root);

        turns.Configure(squads, false, 0.2f);
        enemyAI.Configure(
            squads,
            mapBootstrap.mapGenerator,
            turns,
            occupancy,
            movement,
            attacks,
            abilities,
            completion,
            camera,
            8,
            true);
        tactical.Configure(
            squads,
            occupancy,
            selection,
            turns,
            movement,
            modes,
            attacks,
            movementCommands,
            attackCommands,
            completion,
            abilities,
            abilityCommands,
            enemyAI);

        Require(root.GetComponents<EnemyTacticalAIController>().Length == 1,
            "BattleTacticalRuntime must own exactly one EnemyTacticalAIController.");
        EditorUtility.SetDirty(turns);
        EditorUtility.SetDirty(enemyAI);
        EditorUtility.SetDirty(tactical);
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene),
            "Raw battle scene could not be saved after AI wiring.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "EnemyTacticalAIStageInstaller: one event-driven AI controller installed, " +
            "production development auto-skip disabled, and explicit production references saved.");
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
        return values.Single();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
