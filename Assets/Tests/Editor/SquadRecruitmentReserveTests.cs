using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class SquadRecruitmentReserveTests
{
    private readonly List<Object> cleanup = new();

    [TearDown]
    public void TearDown()
    {
        BattleSquadSelectionContext.Clear();
        for (int i = cleanup.Count - 1; i >= 0; i--)
            if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
        cleanup.Clear();
    }

    [Test]
    public void ReserveWarriorMovesToSquadWithoutCopyingIdentity()
    {
        Setup setup = CreateSetup(1, 1);
        WarriorData reserve = setup.Repository.ReserveWarriors.Single();

        SquadRosterOperationResult result = setup.Service.TryAddWarrior(
            setup.Squad.Id, reserve.id);

        Assert.That(result.Success, Is.True, result.Reason);
        Assert.That(setup.Repository.ReserveWarriors, Is.Empty);
        Assert.That(setup.Squad.Warriors.Last(), Is.SameAs(reserve));
        Assert.That(setup.Repository.ValidateRosterInvariants(out string reason),
            Is.True, reason);
    }

    [Test]
    public void LastAssignedWarriorCanReturnToReserveAndSquadBecomesNotReady()
    {
        Setup setup = CreateSetup(1, 0);
        string warriorId = setup.Squad.Warriors[0].id;

        SquadRosterOperationResult result = setup.Service.TryRemoveWarrior(
            setup.Squad.Id, warriorId);

        Assert.That(result.Success, Is.True, result.Reason);
        Assert.That(setup.Squad.Warriors, Is.Empty);
        Assert.That(setup.Repository.GetReserveWarrior(warriorId), Is.Not.Null);
        Assert.That(setup.Squad.Status,
            Is.EqualTo(PersistentSquadStatus.InactiveNoWarriors));
        Assert.That(setup.Squad.IsCompositionValid, Is.True);
        Assert.That(setup.Squad.IsBattleEligible, Is.False);
        Assert.That(PreBattleSquadSelectionService.Evaluate(
            setup.Squad, out PreBattleSquadUnavailableReason reason, out _), Is.False);
        Assert.That(reason, Is.EqualTo(PreBattleSquadUnavailableReason.InactiveNoWarriors));
    }

    [Test]
    public void AddingFirstReserveWarriorReactivatesInactiveSquad()
    {
        Setup setup = CreateSetup(0, 1);

        SquadRosterOperationResult result = setup.Service.TryAddWarrior(
            setup.Squad.Id, setup.Repository.ReserveWarriors[0].id);

        Assert.That(result.Success, Is.True, result.Reason);
        Assert.That(setup.Squad.Status, Is.EqualTo(PersistentSquadStatus.Active));
        Assert.That(setup.Squad.IsBattleEligible, Is.True);
    }

    [Test]
    public void RotationIsAtomicAndPreservesBothStableEntities()
    {
        Setup setup = CreateSetup(2, 1);
        WarriorData outgoing = setup.Squad.Warriors[0];
        WarriorData incoming = setup.Repository.ReserveWarriors[0];

        SquadRosterOperationResult result = setup.Service.TryRotateWarrior(
            setup.Squad.Id, outgoing.id, incoming.id);

        Assert.That(result.Success, Is.True, result.Reason);
        Assert.That(setup.Squad.Warriors[0], Is.SameAs(incoming));
        Assert.That(setup.Repository.GetReserveWarrior(outgoing.id), Is.SameAs(outgoing));
        Assert.That(setup.Repository.GetReserveWarrior(incoming.id), Is.Null);
    }

    [Test]
    public void InvalidRotationDoesNotMutateEitherContainer()
    {
        Setup setup = CreateSetup(2, 1);
        string before = setup.Repository.CaptureState();

        SquadRosterOperationResult result = setup.Service.TryRotateWarrior(
            setup.Squad.Id, "missing-assigned", setup.Repository.ReserveWarriors[0].id);

        Assert.That(result.Success, Is.False);
        Assert.That(setup.Repository.CaptureState(), Is.EqualTo(before));
    }

    [Test]
    public void SameWarriorCannotBelongToTwoSquadsOrReserve()
    {
        GameObject owner = Track(new GameObject("repository"));
        SquadSaveParticipant repository = owner.AddComponent<SquadSaveParticipant>();
        Assert.That(repository.TryAddSquad(CreateSquad("alpha", 1, "shared"),
            out string first), Is.True, first);
        Assert.That(repository.TryAddSquad(CreateSquad("beta", 1, "shared"),
            out string second), Is.False, second);
        Assert.That(repository.TryAddReserveWarrior(Warrior("shared", 3),
            out string reserve), Is.False, reserve);
    }

    [Test]
    public void NinthWarriorIsRejectedByDomainEvenWhenUiIsAbsent()
    {
        Setup setup = CreateSetup(SquadData.MaximumWarriors, 1);
        string reserveId = setup.Repository.ReserveWarriors[0].id;

        SquadRosterOperationResult result = setup.Service.TryAddWarrior(
            setup.Squad.Id, reserveId);

        Assert.That(result.Failure, Is.EqualTo(SquadRosterOperationFailure.SquadFull));
        Assert.That(setup.Squad.Warriors.Count, Is.EqualTo(8));
        Assert.That(setup.Repository.GetReserveWarrior(reserveId), Is.Not.Null);
    }

    [Test]
    public void CompositionPreviewUsesCanonicalCalculatorWithoutMutation()
    {
        Setup setup = CreateSetup(1, 1);
        string before = setup.Repository.CaptureState();
        WarriorData incoming = setup.Repository.ReserveWarriors[0];

        SquadRosterOperationResult result = setup.Service.PreviewAdd(
            setup.Squad.Id, incoming.id, out SquadCompositionStatPreview preview);

        Assert.That(result.Success, Is.True, result.Reason);
        Assert.That(preview.Candidate.MaxHP - preview.Current.MaxHP,
            Is.EqualTo(incoming.maxHP));
        Assert.That(preview.Candidate.Strength - preview.Current.Strength,
            Is.EqualTo(incoming.strength).Within(.0001f));
        Assert.That(setup.Repository.CaptureState(), Is.EqualTo(before));
    }

    [Test]
    public void SaveRoundTripPreservesExactReserveAndAssignments()
    {
        Setup setup = CreateSetup(2, 2);
        string outgoing = setup.Squad.Warriors[0].id;
        string incoming = setup.Repository.ReserveWarriors[0].id;
        Assert.That(setup.Service.TryRotateWarrior(
            setup.Squad.Id, outgoing, incoming).Success, Is.True);

        GameObject targetObject = Track(new GameObject("restored"));
        SquadSaveParticipant restored = targetObject.AddComponent<SquadSaveParticipant>();
        restored.RestoreState(setup.Repository.CaptureState());

        Assert.That(restored.GetSquad(setup.Squad.Id).Warriors.Select(w => w.id),
            Is.EqualTo(setup.Squad.Warriors.Select(w => w.id)));
        Assert.That(restored.ReserveWarriors.Select(w => w.id),
            Is.EqualTo(setup.Repository.ReserveWarriors.Select(w => w.id)));
        Assert.That(restored.ValidateRosterInvariants(out string reason), Is.True, reason);
    }

    [Test]
    public void ReserveNeverEntersBattleRuntimeSnapshot()
    {
        Setup setup = CreateSetup(2, 3);
        SquadBattleRuntime runtime = new SquadBattleRuntime(setup.Squad);

        Assert.That(runtime.State.warriors.Select(w => w.warriorId),
            Is.EqualTo(setup.Squad.Warriors.Select(w => w.id)));
        Assert.That(runtime.State.warriors.Select(w => w.warriorId)
            .Intersect(setup.Repository.ReserveWarriors.Select(w => w.id)), Is.Empty);
    }

    [Test]
    public void ActiveBattleRuntimeLocksAllCompositionMutations()
    {
        Setup setup = CreateSetup(2, 1);
        SquadBattleRuntime runtime = new SquadBattleRuntime(setup.Squad);
        setup.Repository.RegisterRuntime(runtime);

        SquadRosterOperationResult add = setup.Service.TryAddWarrior(
            setup.Squad.Id, setup.Repository.ReserveWarriors[0].id);
        SquadRosterOperationResult remove = setup.Service.TryRemoveWarrior(
            setup.Squad.Id, setup.Squad.Warriors[0].id);

        Assert.That(add.Failure, Is.EqualTo(SquadRosterOperationFailure.BattleLocked));
        Assert.That(remove.Failure, Is.EqualTo(SquadRosterOperationFailure.BattleLocked));
    }

    [Test]
    public void DevelopmentRosterInitializationIsDeterministicAndIdempotent()
    {
        GameObject owner = Track(new GameObject("repository"));
        SquadSaveParticipant repository = owner.AddComponent<SquadSaveParticipant>();

        repository.ConfigureDevelopmentReserve(true, 8);
        string first = repository.CaptureState();
        repository.ConfigureDevelopmentReserve(true, 8);

        Assert.That(repository.ReserveWarriors.Count, Is.EqualTo(8));
        Assert.That(repository.ReserveWarriors.Select(w => w.id).Distinct().Count(),
            Is.EqualTo(8));
        Assert.That(repository.CaptureState(), Is.EqualTo(first));
    }

    [Test]
    public void PostBattleCasualtyBecomesDeceasedAndNeverReserve()
    {
        Setup setup = CreateSetup(1, 0);
        setup.Repository.ConfigureDevelopmentReserve(true, 1);
        string developmentWarrior = setup.Repository.ReserveWarriors[0].id;
        Assert.That(setup.Service.TryAddWarrior(
            setup.Squad.Id, developmentWarrior).Success, Is.True);
        string survivor = setup.Squad.Warriors[0].id;
        string defeated = developmentWarrior;
        BattleOutcome outcome = new BattleOutcome
        {
            battleId = "reserve-casualty-battle",
            participantResults = new List<SquadBattleResult>
            {
                new SquadBattleResult
                {
                    squadId = setup.Squad.Id,
                    commanderId = setup.Squad.Commander.id,
                    side = BattleSide.Player,
                    initialWarriorIds = new List<string> { survivor, defeated },
                    survivingWarriorIds = new List<string> { survivor },
                    defeatedWarriorIds = new List<string> { defeated },
                    initialCommanderHP = 20,
                    finalCommanderHP = 20,
                    initialMorale = 20,
                    finalMorale = 20
                }
            }
        };
        PersistentDebuffDefinition scar = Track(
            ScriptableObject.CreateInstance<PersistentDebuffDefinition>());
        scar.ConfigureDevelopment("DEV_BattleScar", "Battle Scar", "Resolve -1", -1f);
        PostBattleRules rules = Track(ScriptableObject.CreateInstance<PostBattleRules>());
        rules.ConfigureDevelopment(.2f, scar);

        BattleResultApplicationResult result = new BattleResultApplier(
            setup.Repository, rules, _ => new FixedPostBattleRandom()).Apply(outcome);

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(setup.Squad.GetWarrior(defeated), Is.Null);
        Assert.That(setup.Repository.GetReserveWarrior(defeated), Is.Null);
        Assert.That(setup.Repository.DeceasedWarriorIds, Does.Contain(defeated));
        setup.Repository.ConfigureDevelopmentReserve(true, 8);
        Assert.That(setup.Repository.GetReserveWarrior(defeated), Is.Null);
    }

    [Test]
    public void FirstTryContainsPersistentDevelopmentReserveAndRosterUi()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/first_try.unity");
        SquadSaveParticipant repository = Object.FindAnyObjectByType<SquadSaveParticipant>(
            FindObjectsInactive.Include);
        SquadManagementView management = Object.FindAnyObjectByType<SquadManagementView>(
            FindObjectsInactive.Include);

        Assert.That(repository, Is.Not.Null);
        Assert.That(repository.ReserveWarriors.Count, Is.GreaterThanOrEqualTo(8));
        Assert.That(repository.ValidateRosterInvariants(out string reason), Is.True, reason);
        Assert.That(management, Is.Not.Null);
        Assert.That(management.AddWarriorButton, Is.Not.Null);
        Assert.That(management.RemoveWarriorButton, Is.Not.Null);
        Assert.That(management.RotateWarriorButton, Is.Not.Null);
    }

    private Setup CreateSetup(int assignedCount, int reserveCount)
    {
        GameObject owner = Track(new GameObject("repository"));
        SquadSaveParticipant repository = owner.AddComponent<SquadSaveParticipant>();
        SquadData squad = CreateSquad("player", assignedCount);
        Assert.That(repository.TryAddSquad(squad, out string addError), Is.True, addError);
        for (int i = 0; i < reserveCount; i++)
            Assert.That(repository.TryAddReserveWarrior(
                Warrior($"reserve-{i:00}", i + 2), out string error), Is.True, error);
        return new Setup
        {
            Repository = repository,
            Squad = squad,
            Service = new SquadRosterService(repository)
        };
    }

    private static SquadData CreateSquad(string id, int warriorCount, string forcedId = null)
    {
        List<WarriorData> warriors = new();
        for (int i = 0; i < warriorCount; i++)
            warriors.Add(Warrior(forcedId ?? $"{id}-warrior-{i:00}", i + 1));
        return new SquadData(id, new CommanderData
        {
            id = id + "-commander",
            baseStats = new SquadBaseStats
            {
                hp = 20,
                actionPoints = 8,
                strength = 5,
                dexterity = 4,
                morale = 20,
                resolve = 2
            }
        }, warriors);
    }

    private static WarriorData Warrior(string id, int seed) => new WarriorData
    {
        id = id,
        displayName = "Warrior " + id,
        maxHP = 7 + seed,
        strength = 1 + seed,
        dexterity = .5f + seed
    };

    private T Track<T>(T value) where T : Object
    {
        cleanup.Add(value);
        return value;
    }

    private sealed class FixedPostBattleRandom : IPostBattleRandomSource
    {
        public float Next01() => 0f;
    }

    private sealed class Setup
    {
        public SquadSaveParticipant Repository;
        public SquadData Squad;
        public SquadRosterService Service;
    }
}
