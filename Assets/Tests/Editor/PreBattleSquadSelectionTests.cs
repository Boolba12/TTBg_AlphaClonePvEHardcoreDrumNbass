using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PreBattleSquadSelectionTests
{
    private readonly List<GameObject> cleanup = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        BattleSquadSelectionContext.Clear();
        BattleEncounterContext.Clear();
        PendingSaveLoadContext.Clear();
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        }
        cleanup.Clear();
    }

    [Test]
    public void OptionsUseStableIdsAndDeterministicOrder()
    {
        SquadData zeta = CreateSquad("zeta", 2);
        SquadData alpha = CreateSquad("alpha", 1);

        IReadOnlyList<PreBattleSquadOption> options =
            PreBattleSquadSelectionService.BuildOptions(new[] { zeta, alpha });

        Assert.That(options.Select(option => option.SquadId),
            Is.EqualTo(new[] { "alpha", "zeta" }));
        Assert.That(options.All(option => option.IsAvailable), Is.True);
        Assert.That(options[0].LivingWarriors, Is.EqualTo(1));
        Assert.That(options[0].MaximumWarriors, Is.EqualTo(SquadData.MaximumWarriors));
    }

    [TestCase(PersistentSquadStatus.CommanderLost, PreBattleSquadUnavailableReason.CommanderLost)]
    [TestCase(PersistentSquadStatus.InactiveNoWarriors, PreBattleSquadUnavailableReason.InactiveNoWarriors)]
    public void PersistentStatusProducesExplicitUnavailableReason(
        PersistentSquadStatus status,
        PreBattleSquadUnavailableReason expected)
    {
        SquadData squad = CreateSquad("unavailable", 1);
        SetStatus(squad, status);

        bool eligible = PreBattleSquadSelectionService.Evaluate(
            squad,
            out PreBattleSquadUnavailableReason reason,
            out string message);

        Assert.That(eligible, Is.False);
        Assert.That(reason, Is.EqualTo(expected));
        Assert.That(message, Is.Not.Empty);
    }

    [Test]
    public void EmptyPersistentRosterHasNoSelectableOption()
    {
        IReadOnlyList<PreBattleSquadOption> options =
            PreBattleSquadSelectionService.BuildOptions(new List<SquadData>());
        Assert.That(options, Is.Empty);
    }

    [Test]
    public void PersistentContextCarriesStableIdAndEncounterIdentityWithoutDirectSquadReference()
    {
        BattleEncounterContext.SetEncounterData(
            7,
            new Vector2Int(4, 5),
            new Vector2Int(5, 5),
            default,
            default,
            EncounterInitiator.Player,
            10);

        Assert.That(BattleSquadSelectionContext.SetPersistentEncounterSelection(
            "persistent-player", BattleEncounterContext.EncounterId, true), Is.True);

        Assert.That(BattleSquadSelectionContext.Kind,
            Is.EqualTo(BattleSquadSelectionKind.PersistentEncounter));
        Assert.That(BattleSquadSelectionContext.PlayerSquadIds,
            Is.EqualTo(new[] { "persistent-player" }));
        Assert.That(BattleSquadSelectionContext.PlayerSquads, Is.Empty);
        Assert.That(BattleSquadSelectionContext.EncounterId,
            Is.EqualTo(BattleEncounterContext.EncounterId));
        Assert.That(BattleSquadSelectionContext.AllowConfiguredEncounterEnemy, Is.True);
    }

    [Test]
    public void ConfirmationRevalidationRejectsSquadThatBecameUnavailable()
    {
        GameObject repositoryObject = Track(new GameObject("Repository"));
        SquadSaveParticipant repository = repositoryObject.AddComponent<SquadSaveParticipant>();
        SquadData squad = CreateSquad("selected", 1);
        Assert.That(repository.TryAddSquad(squad, out string addError), Is.True, addError);
        Assert.That(PreBattleSquadSelectionService.TryResolveEligible(
            repository, squad.Id, out SquadData resolved, out _), Is.True);
        Assert.That(resolved, Is.SameAs(squad));

        SetStatus(squad, PersistentSquadStatus.CommanderLost);

        Assert.That(PreBattleSquadSelectionService.TryResolveEligible(
            repository, squad.Id, out _, out string reason), Is.False);
        Assert.That(reason, Does.Contain("Commander lost"));
    }

    [Test]
    public void CancelClearsTransientContextsWithoutMutatingPersistentSquad()
    {
        SquadData squad = CreateSquad("persistent", 2);
        string commanderId = squad.Commander.id;
        string portraitId = squad.CommanderPortraitId;
        string[] warriorIds = squad.Warriors.Select(warrior => warrior.id).ToArray();
        BattleEncounterContext.SetEncounterData(
            11, Vector2Int.zero, Vector2Int.right, default, default,
            EncounterInitiator.Player, 10);
        BattleSquadSelectionContext.SetPersistentEncounterSelection(
            squad.Id, BattleEncounterContext.EncounterId, true);
        GameObject turnObject = Track(new GameObject("TurnSystem"));
        TurnSystem turn = turnObject.AddComponent<TurnSystem>();
        SetPrivate(turn, "preBattlePreparationOpen", true);

        turn.CancelPreBattlePreparation();

        Assert.That(BattleEncounterContext.HasEncounterData, Is.False);
        Assert.That(BattleSquadSelectionContext.HasSelection, Is.False);
        Assert.That(squad.Commander.id, Is.EqualTo(commanderId));
        Assert.That(squad.CommanderPortraitId, Is.EqualTo(portraitId));
        Assert.That(squad.Warriors.Select(warrior => warrior.id), Is.EqualTo(warriorIds));
        Assert.That(squad.Status, Is.EqualTo(PersistentSquadStatus.Active));
    }

    [Test]
    public void PortraitLookupUsesStoredIdWithoutReassignment()
    {
        CommanderPortraitDatabase database = AssetDatabase.LoadAssetAtPath<CommanderPortraitDatabase>(
            "Assets/Art/CommanderPortraits/CommanderPortraitDatabase.asset");
        Assert.That(database, Is.Not.Null);
        CommanderPortraitEntry entry = database.Entries.First(candidate =>
            candidate != null && candidate.Sprite != null);
        SquadData squad = CreateSquad("portrait-squad", 1, entry.Id);
        CommanderPortraitService service = new CommanderPortraitService(database, 1);
        string storedId = squad.CommanderPortraitId;

        Sprite sprite = service.GetDisplaySprite(squad.CommanderPortraitId);

        Assert.That(sprite, Is.SameAs(entry.Sprite));
        Assert.That(squad.CommanderPortraitId, Is.EqualTo(storedId));
    }

    [Test]
    public void FirstTryContainsOneResponsivePreBattleOwnerAndOneEventSystem()
    {
        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/first_try.unity", OpenSceneMode.Single);
        PreBattlePreparationController[] controllers =
            Object.FindObjectsByType<PreBattlePreparationController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        PreBattlePreparationView[] views = Object.FindObjectsByType<PreBattlePreparationView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Assert.That(controllers, Has.Length.EqualTo(1));
        Assert.That(views, Has.Length.EqualTo(1));
        Assert.That(eventSystems, Has.Length.EqualTo(1));
        CanvasScaler scaler = controllers[0].GetComponent<CanvasScaler>();
        Assert.That(scaler, Is.Not.Null);
        Assert.That(scaler.uiScaleMode,
            Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
        Assert.That(views[0].IsVisible, Is.False);
        SquadSaveParticipant repository = Object.FindFirstObjectByType<SquadSaveParticipant>(
            FindObjectsInactive.Include);
        Assert.That(repository, Is.Not.Null);
        Assert.That(repository.Squads.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(repository.Squads.Any(squad =>
            squad != null && squad.IsBattleEligible &&
            !string.IsNullOrWhiteSpace(squad.Id)), Is.True);

        TurnSystem turn = Object.FindFirstObjectByType<TurnSystem>(FindObjectsInactive.Include);
        SerializedObject serializedTurn = new SerializedObject(turn);
        Assert.That(serializedTurn.FindProperty("preBattlePreparationController")
            .objectReferenceValue, Is.SameAs(controllers[0]));
        Assert.That(scene.isDirty, Is.False);
    }

    [TestCase(1920, 1080)]
    [TestCase(2560, 1440)]
    [TestCase(1366, 768)]
    public void PreBattleThreeColumnLayoutFitsSupportedResolution(int width, int height)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/first_try.unity", OpenSceneMode.Single);
        PreBattlePreparationController controller =
            Object.FindFirstObjectByType<PreBattlePreparationController>(
                FindObjectsInactive.Include);
        Assert.That(controller, Is.Not.Null);
        Transform frame = controller.transform.Find(
            "PreBattlePreparationPanel/PreparationFrame");
        Assert.That(frame, Is.Not.Null);
        RectTransform rect = frame.GetComponent<RectTransform>();
        Vector2 normalizedSize = rect.anchorMax - rect.anchorMin;
        float frameWidth = width * normalizedSize.x;
        float frameHeight = height * normalizedSize.y;
        Assert.That(frameWidth, Is.GreaterThanOrEqualTo(1270f));
        Assert.That(frameHeight, Is.GreaterThanOrEqualTo(680f));
        Assert.That(frame.Find("AvailableSquads"), Is.Not.Null);
        Assert.That(frame.Find("SelectedSquad"), Is.Not.Null);
        Assert.That(frame.Find("BattleSummary"), Is.Not.Null);
        Assert.That(frame.Find("CancelButton"), Is.Not.Null);
        Assert.That(frame.Find("ConfirmButton"), Is.Not.Null);
    }

    private static SquadData CreateSquad(string id, int warriors, string portraitId = "portrait-id")
    {
        List<WarriorData> members = new List<WarriorData>();
        for (int i = 0; i < warriors; i++)
        {
            members.Add(new WarriorData
            {
                id = $"{id}-warrior-{i}",
                maxHP = 6,
                strength = 2,
                dexterity = 1
            });
        }
        return new SquadData(
            id,
            new CommanderData
            {
                id = $"{id}-commander",
                race = CommanderRace.Human,
                commanderPortraitId = portraitId,
                baseStats = new SquadBaseStats
                {
                    hp = 12,
                    actionPoints = 6,
                    initiative = 8,
                    strength = 5,
                    dexterity = 4,
                    morale = 40
                }
            },
            members);
    }

    private static void SetStatus(SquadData squad, PersistentSquadStatus status)
    {
        FieldInfo field = typeof(SquadData).GetField(
            "status", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(squad, status);
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private GameObject Track(GameObject gameObject)
    {
        cleanup.Add(gameObject);
        return gameObject;
    }
}
