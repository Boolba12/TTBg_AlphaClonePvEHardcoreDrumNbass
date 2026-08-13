#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class PreBattleSceneSmokeRunner
{
    private const string OverworldScenePath = "Assets/Scenes/first_try.unity";
    private const string BattleSceneName = "Raw_Alpha_BattleMode";
    private const string RequestPath =
        "Assets/Scripts/Squads/Editor/PreBattleSceneSmoke.run-request";
    private const string ResultPath = "Logs/PreBattleSceneSmokeResults_2026-08-13.xml";
    private const string SelectedSquadId = "smoke-persistent-beta";
    private const string SelectedWeaponId = "test-wp-greatsword-02";
    private const string SelectedArmorId = "DEV_BastionArmor";
    private const string SelectedAccessoryId = "DEV_HawkeyeCharm";
    private const string StartedKey = "PreBattleSmoke.Started";
    private const string FinishedKey = "PreBattleSmoke.Finished";
    private const string PassedKey = "PreBattleSmoke.Passed";
    private const string PhaseKey = "PreBattleSmoke.Phase";
    private const string AttemptsKey = "PreBattleSmoke.Attempts";
    private const string StartTimeKey = "PreBattleSmoke.StartTime";
    private const string RosterSnapshotKey = "PreBattleSmoke.RosterSnapshot";
    private const string RuntimeErrorKey = "PreBattleSmoke.RuntimeError";
    private const string ManagementVerifiedKey = "PreBattleSmoke.ManagementVerified";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PreparePersistentRoster()
    {
        if (!File.Exists(RequestPath))
            return;

        BattleSquadSelectionContext.Clear();
        BattleEncounterContext.Clear();
        BattleReturnContext.Clear();
        PendingSaveLoadContext.Clear();
        ResolvedEncounterRegistry.Clear();
        Application.logMessageReceived -= CaptureRuntimeError;
        Application.logMessageReceived += CaptureRuntimeError;

        CommanderPortraitDatabase portraits =
            AssetDatabase.LoadAssetAtPath<CommanderPortraitDatabase>(
                "Assets/Art/CommanderPortraits/CommanderPortraitDatabase.asset");
        string portraitId = portraits?.Entries.FirstOrDefault(entry =>
            entry != null && entry.Race == CommanderRace.Human && entry.Sprite != null)?.Id;
        EquipmentDefinitionCatalog equipmentCatalog =
            AssetDatabase.LoadAssetAtPath<EquipmentDefinitionCatalog>(
                "Assets/GameData/Equipment/DEV_EquipmentDefinitionCatalog.asset");
        SquadData alpha = CreateSquad("smoke-persistent-alpha", portraitId, 1, 6);
        SquadData beta = CreateSquad(SelectedSquadId, portraitId, 3, 14);
        ConfigureSmokeEquipment(alpha, equipmentCatalog);
        ConfigureSmokeEquipment(beta, equipmentCatalog);
        SquadSavePayload payload = new SquadSavePayload
        {
            squads = new List<SquadData>
            {
                alpha,
                beta
            }
        };
        GameSaveData data = new GameSaveData
        {
            saveId = "prebattle-smoke",
            sceneName = "first_try",
            systems = new List<SystemSaveData>
            {
                new SystemSaveData { key = "squads", json = JsonUtility.ToJson(payload) }
            }
        };
        PendingSaveLoadContext.Set(data);
    }

    [InitializeOnLoadMethod]
    private static void ContinueRequestedRun()
    {
        if (File.Exists(RequestPath))
            EditorApplication.delayCall += RegisterUpdate;
    }

    [MenuItem("Tools/Squads/Run Pre-Battle Production Smoke")]
    public static void RunFromMenu()
    {
        if (!File.Exists(RequestPath))
            File.WriteAllText(RequestPath, "Run Pre-Battle production-equivalent smoke.");
        SessionState.EraseBool(StartedKey);
        SessionState.EraseBool(FinishedKey);
        SessionState.EraseBool(PassedKey);
        SessionState.EraseString(RuntimeErrorKey);
        SessionState.EraseString(RosterSnapshotKey);
        SessionState.EraseBool(ManagementVerifiedKey);
        SessionState.SetInt(PhaseKey, 0);
        SessionState.SetInt(AttemptsKey, 0);
        RegisterUpdate();
    }

    private static void RegisterUpdate()
    {
        EditorApplication.update -= UpdateRun;
        EditorApplication.update += UpdateRun;
    }

    private static void UpdateRun()
    {
        if (SessionState.GetBool(FinishedKey, false))
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Cleanup();
            return;
        }
        if (!SessionState.GetBool(StartedKey, false))
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            Scene active = SceneManager.GetActiveScene();
            if (active.isDirty && active.path != OverworldScenePath)
            {
                Finish(false, "Another modified scene is active; smoke refused to discard it.");
                return;
            }
            if (active.path != OverworldScenePath)
                EditorSceneManager.OpenScene(OverworldScenePath, OpenSceneMode.Single);
            SessionState.SetBool(StartedKey, true);
            SessionState.SetString(StartTimeKey,
                EditorApplication.timeSinceStartup.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            EditorApplication.isPlaying = true;
            return;
        }
        if (!EditorApplication.isPlaying)
            return;

        try
        {
            if (SceneManager.GetActiveScene().name == BattleSceneName)
                ValidateBattleScene();
            else
                DriveOverworldPreparation();
            CheckTimeout();
        }
        catch (Exception exception)
        {
            Finish(false, exception.Message);
        }
    }

    private static void DriveOverworldPreparation()
    {
        SquadSaveParticipant repository = UnityEngine.Object.FindAnyObjectByType<SquadSaveParticipant>();
        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        EnemyController enemy = UnityEngine.Object.FindAnyObjectByType<EnemyController>();
        TurnSystem turn = UnityEngine.Object.FindAnyObjectByType<TurnSystem>();
        PreBattlePreparationController preparation =
            UnityEngine.Object.FindAnyObjectByType<PreBattlePreparationController>();
        PreBattlePreparationView view =
            UnityEngine.Object.FindAnyObjectByType<PreBattlePreparationView>();
        if (repository == null || player == null || enemy == null || turn == null ||
            preparation == null || view == null || player.mapGenerator == null ||
            !player.mapGenerator.HasGeneratedData || repository.Squads.Count != 2)
        {
            return;
        }

        int phase = SessionState.GetInt(PhaseKey, 0);
        if (!SessionState.GetBool(ManagementVerifiedKey, false) && !preparation.IsOpen)
        {
            if (turn.IsEnemyTurnRunning || player.IsMovementInProgress ||
                turn.IsBattleLoadingTriggered)
                return;
            ValidateManagementProductionFlow(repository);
            SessionState.SetBool(ManagementVerifiedKey, true);
            return;
        }
        if (preparation.IsOpen)
        {
            Require(view.IsVisible, "Pre-Battle controller opened without its production view.");
            Require(preparation.Options.Count == 2, "Persistent roster did not render two squad options.");
            Require(UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "first_try does not contain exactly one EventSystem.");

            if (phase <= 1)
            {
                Require(!view.ConfirmButton.interactable,
                    "Confirm must be disabled before explicit squad selection.");
                string snapshot = repository.CaptureState();
                SessionState.SetString(RosterSnapshotKey, snapshot);
                view.CancelButton.onClick.Invoke();
                Require(!preparation.IsOpen && !view.IsVisible,
                    "Cancel did not close Pre-Battle preparation.");
                Require(!BattleEncounterContext.HasEncounterData &&
                        !BattleSquadSelectionContext.HasSelection,
                    "Cancel left stale battle contexts.");
                Require(repository.CaptureState() == snapshot,
                    "Cancel mutated the persistent squad roster.");
                SessionState.SetInt(PhaseKey, 2);
                SessionState.SetInt(AttemptsKey, 0);
                return;
            }

            PreBattleSquadCardView card = UnityEngine.Object
                .FindObjectsByType<PreBattleSquadCardView>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.SquadId == SelectedSquadId &&
                    candidate.gameObject.activeInHierarchy &&
                    candidate.GetComponentInParent<PreBattlePreparationView>() != null);
            Require(card != null && card.gameObject.activeInHierarchy,
                "Selected persistent squad card is unavailable.");
            card.SelectButton.onClick.Invoke();
            Require(preparation.SelectedSquadId == SelectedSquadId &&
                    view.ConfirmButton.interactable,
                "Production card selection did not enable Confirm for the exact stable ID.");
            Require(view.EquipmentCardCount == 12,
                "Pre-Battle Equipment v2 did not render all twelve owned weapon instances.");
            ItemPreviewCardView weaponCard = UnityEngine.Object
                .FindObjectsByType<ItemPreviewCardView>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.activeInHierarchy &&
                    candidate.DisplayedId == SelectedWeaponId);
            Require(weaponCard?.Button != null && weaponCard.Button.interactable,
                "Selected development weapon is not commandable through production UI.");
            weaponCard.Button.onClick.Invoke();
            SquadData selected = repository.GetSquad(SelectedSquadId);
            EquipmentItemInstance equipped = selected.Equipment.OwnedItems.FirstOrDefault(item =>
                item != null && item.InstanceId == selected.Equipment.SquadWeaponInstanceId);
            Require(equipped?.DefinitionId == SelectedWeaponId,
                "Production equipment click did not atomically update persistent Squad Weapon.");
            view.ConfirmButton.onClick.Invoke();
            SessionState.SetInt(PhaseKey, 4);
            return;
        }

        if (turn.IsEnemyTurnRunning || player.IsMovementInProgress ||
            turn.IsBattleLoadingTriggered)
        {
            return;
        }
        if (phase == 0 || phase == 2 || phase == 1 || phase == 3)
        {
            Require(CommitOneStepTowardEnemy(player, enemy),
                "Could not issue a production overworld path request.");
            SessionState.SetInt(PhaseKey, phase < 2 ? 1 : 3);
            SessionState.SetInt(AttemptsKey, SessionState.GetInt(AttemptsKey, 0) + 1);
            Require(SessionState.GetInt(AttemptsKey, 0) <= 20,
                "Encounter did not reach Pre-Battle preparation within 20 production turns.");
        }
    }

    private static bool CommitOneStepTowardEnemy(PlayerController player, EnemyController enemy)
    {
        Vector2Int origin = player.CurrentCell;
        Vector2Int[] directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
            new Vector2Int(1, 1), new Vector2Int(1, -1),
            new Vector2Int(-1, -1), new Vector2Int(-1, 1)
        };
        Vector2Int destination = default;
        int bestDistance = int.MaxValue;
        bool found = false;
        for (int i = 0; i < directions.Length; i++)
        {
            Vector2Int candidate = origin + directions[i];
            if (candidate == enemy.CurrentCell || candidate.x < 0 || candidate.y < 0 ||
                candidate.x >= player.mapGenerator.width ||
                candidate.y >= player.mapGenerator.height ||
                !player.mapGenerator.GetIsPlayable(candidate.x, candidate.y))
            {
                continue;
            }
            int distance = Mathf.Max(
                Mathf.Abs(candidate.x - enemy.CurrentCell.x),
                Mathf.Abs(candidate.y - enemy.CurrentCell.y));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                destination = candidate;
                found = true;
            }
        }
        return found && player.TryRequestPathToCell(destination) &&
               player.TryRequestPathToCell(destination);
    }

    private static void ValidateBattleScene()
    {
        BattleMapBootstrap map = UnityEngine.Object.FindAnyObjectByType<BattleMapBootstrap>();
        SquadBattleBootstrap squads = UnityEngine.Object.FindAnyObjectByType<SquadBattleBootstrap>();
        SaveSystemBehaviour save = UnityEngine.Object.FindAnyObjectByType<SaveSystemBehaviour>();
        if (map == null || squads == null || save == null || !map.HasBootstrapped)
            return;

        Require(save.StartupRestoreCompleted, "Battle startup did not wait for persistent restore.");
        Require(map.mapGenerator.Width == 32 && map.mapGenerator.Height == 32,
            "Pre-Battle transition changed the 32x32 production map.");
        Require(map.UsedConfirmedPreBattleAutoConfirm && !map.UsedDevelopmentAutoConfirm,
            "Battle did not use the confirmed production Pre-Battle startup pathway.");
        Require(squads.SpawnedControllers.Count == 2,
            "Battle did not create exactly two squad controllers.");
        SquadBattleController player = squads.SpawnedControllers.Single(controller =>
            controller.Side == BattleSide.Player);
        Require(player.SquadId == SelectedSquadId,
            $"Battle spawned '{player.SquadId}' instead of selected '{SelectedSquadId}'.");
        Require(player.Runtime.Data.Id == SelectedSquadId,
            "Battle runtime is not backed by the selected persistent SquadData identity.");
        Require(player.Runtime.Equipment.SquadWeapon?.DefinitionId == SelectedWeaponId,
            "Battle runtime did not snapshot the Squad Weapon selected in Pre-Battle UI.");
        Require(player.Runtime.Equipment.ArmorDefinitionId == SelectedArmorId,
            "Battle runtime did not preserve the Armor selected in Squad Management.");
        Require(player.Runtime.Equipment.AccessoryDefinitionId == SelectedAccessoryId,
            "Battle runtime did not preserve the Accessory selected in Squad Management.");
        Require(player.Runtime.Stats.PhysicalArmor > 0f &&
                player.Runtime.Stats.Accuracy > .1f,
            "Management equipment modifiers were not present in battle calculated stats.");
        BattleAttackService attackService =
            UnityEngine.Object.FindAnyObjectByType<BattleAttackService>();
        Require(attackService != null && attackService.BasicAttack != null &&
                player.Runtime.Equipment.GetWeaponForAttack(attackService.BasicAttack)
                    ?.DefinitionId == SelectedWeaponId,
            "Basic Attack does not resolve the Pre-Battle Squad Weapon snapshot.");
        Require(!BattleSquadSelectionContext.HasSelection,
            "Successful battle bootstrap did not consume selection context.");
        Require(UnityEngine.Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "Raw battle does not contain exactly one EventSystem.");
        RequireNoRuntimeErrors();
        Finish(true,
            "first_try SQUADS button opened persistent Management UI; production slot/item/" +
            "Equip controls applied weapon, Armor and Accessory, Save succeeded, close/reopen " +
            "preserved state; Pre-Battle then entered the 32x32 battle with the same immutable " +
            "equipment snapshot and calculated modifiers.");
    }

    private static void ValidateManagementProductionFlow(SquadSaveParticipant repository)
    {
        SquadManagementController controller =
            UnityEngine.Object.FindAnyObjectByType<SquadManagementController>();
        SquadManagementView management =
            UnityEngine.Object.FindAnyObjectByType<SquadManagementView>(FindObjectsInactive.Include);
        SaveSystemBehaviour save = UnityEngine.Object.FindAnyObjectByType<SaveSystemBehaviour>();
        Require(controller?.OpenButton != null && management != null && save != null,
            "Production Squad Management owner, view or SQUADS button is missing.");
        Require(UnityEngine.Object.FindObjectsByType<SquadManagementController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "first_try has duplicate Squad Management owners.");
        Require(UnityEngine.Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "first_try does not contain exactly one EventSystem.");

        controller.OpenButton.onClick.Invoke();
        Require(controller.IsOpen && management.IsVisible && management.SquadCardCount == 2,
            "SQUADS production button did not render both persistent squads.");
        PreBattleSquadCardView selectedCard = UnityEngine.Object
            .FindObjectsByType<PreBattleSquadCardView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(card => card.gameObject.activeInHierarchy &&
                card.SquadId == SelectedSquadId);
        Require(selectedCard?.SelectButton != null,
            "Management persistent squad card is unavailable.");
        selectedCard.SelectButton.onClick.Invoke();
        Require(controller.SelectedSquadId == SelectedSquadId,
            "Management squad selection did not use the persistent stable ID.");

        EquipThroughManagement(controller, management, "SquadWeaponSlot", SelectedWeaponId);
        EquipThroughManagement(controller, management, "ArmorSlot", SelectedArmorId);
        EquipThroughManagement(controller, management, "AccessorySlot", SelectedAccessoryId);

        string smokeSaveRoot = Path.GetFullPath("Logs/SquadManagementSmokeSave");
        Require(save.ConfigureStorageRootForTests(smokeSaveRoot),
            "Management smoke save root could not be configured.");
        management.SaveButton.onClick.Invoke();
        Require(save.LastOperationResult.Success,
            $"Production Save button failed: {save.LastOperationResult.Error}");
        management.CloseButton.onClick.Invoke();
        Require(!controller.IsOpen && !management.IsVisible,
            "Management Close did not restore overworld input state.");

        controller.OpenButton.onClick.Invoke();
        Require(controller.IsOpen, "Management did not reopen through SQUADS.");
        SquadData selected = repository.GetSquad(SelectedSquadId);
        Require(ResolveDefinitionId(selected, EquipmentSlotKind.SquadWeapon) == SelectedWeaponId &&
                ResolveDefinitionId(selected, EquipmentSlotKind.Armor) == SelectedArmorId &&
                ResolveDefinitionId(selected, EquipmentSlotKind.Accessory) == SelectedAccessoryId,
            "Close/reopen did not preserve persistent management equipment state.");
        management.CloseButton.onClick.Invoke();
    }

    private static void EquipThroughManagement(SquadManagementController controller,
        SquadManagementView management, string slotObjectName, string definitionId)
    {
        EquipmentSlotView slot = UnityEngine.Object.FindObjectsByType<EquipmentSlotView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.gameObject.activeInHierarchy &&
                candidate.gameObject.name == slotObjectName &&
                candidate.GetComponentInParent<SquadManagementView>() != null);
        Require(slot?.Button != null, $"Management slot '{slotObjectName}' is missing.");
        slot.Button.onClick.Invoke();
        ItemPreviewCardView item = UnityEngine.Object.FindObjectsByType<ItemPreviewCardView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.gameObject.activeInHierarchy &&
                candidate.DisplayedId == definitionId && candidate.Button != null &&
                candidate.Button.interactable &&
                candidate.InstanceId.StartsWith(SelectedSquadId,
                    StringComparison.Ordinal) &&
                candidate.GetComponentInParent<SquadManagementView>() != null);
        Require(item != null, $"Owned management item '{definitionId}' is not commandable.");
        item.Button.onClick.Invoke();
        Require(controller.SelectedItemInstanceId == item.InstanceId,
            "Item preview selection did not reach the management controller.");
        management.EquipButton.onClick.Invoke();
        string resolved = ResolveDefinitionId(
            UnityEngine.Object.FindAnyObjectByType<SquadSaveParticipant>()
                .GetSquad(SelectedSquadId), controller.SelectedSlot);
        Require(resolved == definitionId,
            $"Production Equip did not commit '{definitionId}'. " +
            $"slot={controller.SelectedSlot}, selected={controller.SelectedItemInstanceId}, " +
            $"resolved={resolved}, ui={management.OperationMessage}");
    }

    private static string ResolveDefinitionId(SquadData squad, EquipmentSlotKind slot)
    {
        string instanceId = squad?.Equipment.GetEquippedInstanceId(slot);
        return squad?.Equipment.OwnedItems.FirstOrDefault(item => item != null &&
            item.InstanceId == instanceId)?.DefinitionId;
    }

    private static SquadData CreateSquad(
        string id, string portraitId, int warriorCount, float initiative)
    {
        List<WarriorData> warriors = new List<WarriorData>();
        for (int i = 0; i < warriorCount; i++)
        {
            warriors.Add(new WarriorData
            {
                id = $"{id}-warrior-{i}", maxHP = 8, strength = 2, dexterity = 2
            });
        }
        return new SquadData(id, new CommanderData
        {
            id = $"{id}-commander",
            race = CommanderRace.Human,
            commanderPortraitId = portraitId,
            baseStats = new SquadBaseStats
            {
                hp = 18, actionPoints = 8, initiative = initiative,
                strength = 6, dexterity = 5, morale = 60, accuracy = 0.1f,
                criticalChance = 0.1f, criticalDamage = 1.5f
            }
        }, warriors);
    }

    private static void ConfigureSmokeEquipment(
        SquadData squad,
        EquipmentDefinitionCatalog catalog)
    {
        Require(catalog != null && catalog.Weapons.Count == 12,
            "Equipment smoke catalog is unavailable.");
        SquadEquipmentService service = new SquadEquipmentService(catalog);
        foreach (EquipmentItemDefinition definition in catalog.EnumerateDefinitions())
            Require(service.GrantOwnedItem(squad,
                $"{squad.Id}-smoke-{definition.StableId}", definition.StableId).Success,
                $"Could not grant {definition.StableId} to smoke squad.");
        Require(service.TryEquip(squad,
            $"{squad.Id}-smoke-test-wp-sword-01",
            EquipmentSlotKind.SquadWeapon).Success,
            "Could not configure smoke Squad Weapon.");
        Require(service.TryEquip(squad,
            $"{squad.Id}-smoke-test-wp-dagger-01",
            EquipmentSlotKind.CommanderWeapon).Success,
            "Could not configure smoke Commander Weapon.");
        squad.MarkEquipmentSchemaCurrent();
    }

    private static void CheckTimeout()
    {
        double started = double.Parse(SessionState.GetString(StartTimeKey, "0"),
            System.Globalization.CultureInfo.InvariantCulture);
        if (EditorApplication.timeSinceStartup - started > 120)
        {
            Finish(false,
                $"Timed out in phase {SessionState.GetInt(PhaseKey, -1)}; " +
                $"scene={SceneManager.GetActiveScene().name}.");
        }
    }

    private static void CaptureRuntimeError(string condition, string stack, LogType type)
    {
        if (type != LogType.Exception && !condition.Contains("NullReferenceException") &&
            !condition.Contains("MissingReferenceException") &&
            !condition.Contains("MissingComponentException"))
        {
            return;
        }
        if (string.IsNullOrEmpty(SessionState.GetString(RuntimeErrorKey, string.Empty)))
            SessionState.SetString(RuntimeErrorKey, $"{condition}\n{stack}");
    }

    private static void RequireNoRuntimeErrors()
    {
        string error = SessionState.GetString(RuntimeErrorKey, string.Empty);
        Require(string.IsNullOrEmpty(error), error);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Finish(bool passed, string message)
    {
        if (passed && !string.IsNullOrEmpty(SessionState.GetString(RuntimeErrorKey, string.Empty)))
        {
            passed = false;
            message = SessionState.GetString(RuntimeErrorKey, string.Empty);
        }
        WriteResult(passed, message);
        Debug.Log($"Pre-Battle scene smoke: {(passed ? "PASSED" : "FAILED")} - {message}");
        SessionState.SetBool(FinishedKey, true);
        SessionState.SetBool(PassedKey, passed);
        if (EditorApplication.isPlaying)
            EditorApplication.isPlaying = false;
    }

    private static void Cleanup()
    {
        bool passed = SessionState.GetBool(PassedKey, false);
        EditorApplication.update -= UpdateRun;
        AssetDatabase.DeleteAsset(RequestPath);
        Application.logMessageReceived -= CaptureRuntimeError;
        SessionState.EraseBool(StartedKey);
        SessionState.EraseBool(FinishedKey);
        SessionState.EraseBool(PassedKey);
        SessionState.EraseInt(PhaseKey);
        SessionState.EraseInt(AttemptsKey);
        SessionState.EraseString(StartTimeKey);
        SessionState.EraseString(RosterSnapshotKey);
        SessionState.EraseString(RuntimeErrorKey);
        if (Application.isBatchMode)
            EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void WriteResult(bool passed, string message)
    {
        Directory.CreateDirectory("Logs");
        using XmlWriter writer = XmlWriter.Create(ResultPath, new XmlWriterSettings { Indent = true });
        writer.WriteStartDocument();
        writer.WriteStartElement("test-run");
        writer.WriteAttributeString("total", "1");
        writer.WriteAttributeString("passed", passed ? "1" : "0");
        writer.WriteAttributeString("failed", passed ? "0" : "1");
        writer.WriteAttributeString("result", passed ? "Passed" : "Failed");
        writer.WriteStartElement("test-case");
        writer.WriteAttributeString("name", "PreBattleProductionFlowSmoke");
        writer.WriteAttributeString("result", passed ? "Passed" : "Failed");
        writer.WriteElementString("message", message ?? string.Empty);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }
}
#endif
