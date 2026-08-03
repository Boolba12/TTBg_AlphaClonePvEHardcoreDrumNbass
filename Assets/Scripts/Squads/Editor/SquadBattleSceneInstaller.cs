#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SquadBattleSceneInstaller
{
    private const string ScenePath = "Assets/Scenes/Raw_Alpha_BattleMode.unity";
    private const string PrefabFolder = "Assets/Prefabs/Squads";
    private const string PlaceholderPrefabPath =
        PrefabFolder + "/DevelopmentSquadMemberPlaceholder.prefab";
    private const string PlaceholderMaterialPath =
        PrefabFolder + "/DevelopmentSquadMemberPlaceholder.mat";
    private const string SquadPrefabPath = PrefabFolder + "/SquadBattle.prefab";
    private const string PortraitDatabasePath =
        "Assets/Art/CommanderPortraits/CommanderPortraitDatabase.asset";
    [MenuItem("Tools/Squads/Install Raw Alpha Battle Integration %#&g")]
    public static void InstallFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Install();
    }

    public static void InstallFromCommandLine()
    {
        Install();
    }

    private static void Install()
    {
        EnsureFolder("Assets/Prefabs", "Squads");
        GameObject placeholderPrefab = BuildPlaceholderPrefab();
        SquadBattleController squadPrefab = BuildSquadPrefab(placeholderPrefab);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BattleMapBootstrap mapBootstrap = FindSingleInScene<BattleMapBootstrap>(scene);
        BattleContextMenuUI setupUI = FindSingleInScene<BattleContextMenuUI>(scene);
        SaveSystemBehaviour saveSystem = FindSingleInScene<SaveSystemBehaviour>(scene);
        if (mapBootstrap == null || setupUI == null || saveSystem == null)
            throw new MissingReferenceException(
                "Raw_Alpha_BattleMode requires one BattleMapBootstrap, " +
                "BattleContextMenuUI, and SaveSystemBehaviour.");

        GameObject compositionRoot = GetOrCreateCompositionRoot(scene);
        SquadSaveParticipant squadRepository =
            GetOrAddComponent<SquadSaveParticipant>(compositionRoot);
        SquadBattleBootstrap squadBootstrap =
            GetOrAddComponent<SquadBattleBootstrap>(compositionRoot);
        Transform squadContainer = GetOrCreateChild(
            compositionRoot.transform,
            "SpawnedSquads");

        SquadData playerFallback = CreateDevelopmentSquad(
            "dev-player-squad",
            "dev-player-commander",
            "dev-player-warrior",
            18f);
        SquadData enemyFallback = CreateDevelopmentSquad(
            "dev-enemy-squad",
            "dev-enemy-commander",
            "dev-enemy-warrior",
            14f);
        squadBootstrap.Configure(
            squadPrefab,
            squadContainer,
            squadRepository,
            true,
            playerFallback,
            enemyFallback,
            true);

        GameObject canonicalPlayerRoot = mapBootstrap.playerController?.gameObject;
        GameObject canonicalEnemyRoot = mapBootstrap.enemyController?.gameObject;
        if (canonicalPlayerRoot == null || canonicalEnemyRoot == null)
            throw new MissingReferenceException(
                "BattleMapBootstrap requires its canonical legacy player/enemy controllers.");
        GameObject[] obsoleteLegacyRoots = GetObsoleteLegacyCombatRoots(
            scene,
            canonicalPlayerRoot,
            canonicalEnemyRoot);
        mapBootstrap.ConfigureSquadMode(
            squadBootstrap,
            setupUI,
            canonicalPlayerRoot,
            canonicalEnemyRoot,
            obsoleteLegacyRoots,
            true);

        CommanderPortraitSaveParticipant portraitParticipant =
            ConfigurePortraitParticipant(scene);
        ConfigureSaveParticipants(
            saveSystem,
            squadRepository,
            portraitParticipant);

        EditorUtility.SetDirty(mapBootstrap);
        EditorUtility.SetDirty(squadBootstrap);
        EditorUtility.SetDirty(squadRepository);
        EditorUtility.SetDirty(saveSystem);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Squad battle integration installed in {ScenePath}. " +
            $"Squad prefab: {SquadPrefabPath}; canonical legacy pair configured; " +
            $"obsolete legacy roots: {obsoleteLegacyRoots.Length}.");
    }

    private static GameObject BuildPlaceholderPrefab()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            PlaceholderMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Standard");
            material = new Material(shader)
            {
                name = "DevelopmentSquadMemberPlaceholder",
                color = new Color(0.95f, 0.75f, 0.2f, 1f)
            };
            AssetDatabase.CreateAsset(material, PlaceholderMaterialPath);
        }

        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        source.name = "DevelopmentSquadMemberPlaceholder";
        source.transform.localScale = new Vector3(0.18f, 0.2f, 0.18f);
        Object.DestroyImmediate(source.GetComponent<Collider>());
        source.GetComponent<MeshRenderer>().sharedMaterial = material;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
            source,
            PlaceholderPrefabPath);
        Object.DestroyImmediate(source);
        return prefab;
    }

    private static SquadBattleController BuildSquadPrefab(GameObject placeholderPrefab)
    {
        GameObject root = new GameObject("SquadBattle");
        SquadGridAnchor anchor = root.AddComponent<SquadGridAnchor>();
        SquadFormationView formation = root.AddComponent<SquadFormationView>();
        SquadBattleController controller = root.AddComponent<SquadBattleController>();

        Transform models = GetOrCreateChild(root.transform, "Models");
        Transform commanderSlot = CreateSlot(models, "CommanderSlot", Vector3.zero);
        List<Transform> warriorSlots = new List<Transform>();
        Vector3[] positions =
        {
            new Vector3(-0.28f, 0f, -0.28f),
            new Vector3(0f, 0f, -0.28f),
            new Vector3(0.28f, 0f, -0.28f),
            new Vector3(-0.28f, 0f, 0f),
            new Vector3(0.28f, 0f, 0f),
            new Vector3(-0.28f, 0f, 0.28f),
            new Vector3(0f, 0f, 0.28f),
            new Vector3(0.28f, 0f, 0.28f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            warriorSlots.Add(CreateSlot(
                models,
                $"WarriorSlot_{i + 1:00}",
                positions[i]));
        }

        formation.Configure(
            models,
            commanderSlot,
            warriorSlots,
            placeholderPrefab,
            placeholderPrefab);
        controller.Configure(anchor, formation);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SquadPrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<SquadBattleController>();
    }

    private static CommanderPortraitSaveParticipant ConfigurePortraitParticipant(
        Scene scene)
    {
        CommanderPortraitSaveParticipant participant =
            FindSingleInScene<CommanderPortraitSaveParticipant>(scene);
        if (participant == null)
        {
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(
                candidate => candidate.name == "CommanderPortraitSaveParticipant");
            if (root == null)
            {
                root = new GameObject("CommanderPortraitSaveParticipant");
                SceneManager.MoveGameObjectToScene(root, scene);
            }
            participant = root.AddComponent<CommanderPortraitSaveParticipant>();
        }

        CommanderPortraitDatabase database =
            AssetDatabase.LoadAssetAtPath<CommanderPortraitDatabase>(
                PortraitDatabasePath);
        if (database == null)
            throw new MissingReferenceException(
                $"Commander portrait database is missing at {PortraitDatabasePath}.");

        SerializedObject serializedParticipant = new SerializedObject(participant);
        serializedParticipant.FindProperty("database").objectReferenceValue = database;
        serializedParticipant.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(participant);
        return participant;
    }

    private static void ConfigureSaveParticipants(
        SaveSystemBehaviour saveSystem,
        SquadSaveParticipant squadParticipant,
        CommanderPortraitSaveParticipant portraitParticipant)
    {
        SerializedObject serializedSaveSystem = new SerializedObject(saveSystem);
        SerializedProperty participants = serializedSaveSystem.FindProperty("participants");
        List<MonoBehaviour> configured = new List<MonoBehaviour>();
        for (int i = 0; i < participants.arraySize; i++)
        {
            MonoBehaviour existing =
                participants.GetArrayElementAtIndex(i).objectReferenceValue as MonoBehaviour;
            if (existing != null && !configured.Contains(existing))
                configured.Add(existing);
        }

        AddUnique(configured, squadParticipant);
        AddUnique(configured, portraitParticipant);

        participants.arraySize = configured.Count;
        for (int i = 0; i < configured.Count; i++)
        {
            participants.GetArrayElementAtIndex(i).objectReferenceValue = configured[i];
        }
        serializedSaveSystem.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject[] GetObsoleteLegacyCombatRoots(
        Scene scene,
        GameObject canonicalPlayerRoot,
        GameObject canonicalEnemyRoot)
    {
        List<GameObject> roots = new List<GameObject>();
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            foreach (PlayerController controller in
                     sceneRoot.GetComponentsInChildren<PlayerController>(true))
            {
                if (controller.gameObject != canonicalPlayerRoot)
                    AddUnique(roots, controller.gameObject);
            }

            foreach (EnemyController controller in
                     sceneRoot.GetComponentsInChildren<EnemyController>(true))
            {
                if (controller.gameObject != canonicalEnemyRoot)
                    AddUnique(roots, controller.gameObject);
            }
        }

        return roots.ToArray();
    }

    private static GameObject GetOrCreateCompositionRoot(Scene scene)
    {
        SquadBattleBootstrap existing = FindSingleInScene<SquadBattleBootstrap>(scene);
        if (existing != null)
            return existing.gameObject;

        GameObject root = new GameObject("SquadBattleCompositionRoot");
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    private static SquadData CreateDevelopmentSquad(
        string squadId,
        string commanderId,
        string warriorIdPrefix,
        float initiative)
    {
        CommanderData commander = new CommanderData
        {
            id = commanderId,
            baseStats = new SquadBaseStats
            {
                hp = 20,
                actionPoints = 4,
                initiative = initiative,
                physicalSpeed = 6,
                magicalSpeed = 4,
                strength = 8,
                dexterity = 7,
                magicalMastery = 5,
                morale = 20,
                resolve = 3,
                visionRange = 5
            }
        };

        List<WarriorData> warriors = new List<WarriorData>();
        for (int i = 0; i < 4; i++)
        {
            warriors.Add(new WarriorData
            {
                id = $"{warriorIdPrefix}-{i + 1}",
                maxHP = 8,
                strength = 2,
                dexterity = 1
            });
        }

        return new SquadData(squadId, commander, warriors);
    }

    private static T FindSingleInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static Transform GetOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            return child;

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static Transform CreateSlot(
        Transform parent,
        string slotName,
        Vector3 localPosition)
    {
        Transform slot = GetOrCreateChild(parent, slotName);
        slot.localPosition = localPosition;
        slot.localRotation = Quaternion.identity;
        slot.localScale = Vector3.one;
        return slot;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static void AddUnique<T>(List<T> list, T value) where T : class
    {
        if (value != null && !list.Contains(value))
            list.Add(value);
    }
}
#endif
