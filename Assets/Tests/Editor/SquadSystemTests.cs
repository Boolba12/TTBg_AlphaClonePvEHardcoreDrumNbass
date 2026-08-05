using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class SquadSystemTests
{
    [Test]
    public void SquadRequiresCommanderAndOneToEightWarriors()
    {
        Assert.That(new SquadData("s", null, new[] { Warrior("w") }).Validate().IsValid, Is.False);
        Assert.That(new SquadData("s", Commander(), new WarriorData[0]).Validate().IsValid, Is.False);

        for (int count = 1; count <= 8; count++)
            Assert.That(CreateSquad(count).Validate().IsValid, Is.True);

        Assert.That(CreateSquad(9).Validate().IsValid, Is.False);
    }

    [Test]
    public void NinthWarriorAndBattleCompositionChangesAreRejected()
    {
        SquadData full = CreateSquad(8);
        Assert.That(full.TryAddWarrior(Warrior("ninth"), false, out _), Is.False);

        SquadData editable = CreateSquad(2);
        Assert.That(editable.TryAddWarrior(Warrior("extra"), true, out _), Is.False);
        Assert.That(editable.TryRemoveWarrior("w-0", true, out _), Is.False);
    }

    [Test]
    public void CalculatorSumsLivingWarriorsAndKeepsCommanderStats()
    {
        SquadData squad = CreateSquad(3);
        SquadCalculatedStats initial = SquadStatsCalculator.Calculate(squad);

        Assert.That(initial.MaxHP, Is.EqualTo(25));
        Assert.That(initial.Strength, Is.EqualTo(16));
        Assert.That(initial.Dexterity, Is.EqualTo(11));
        Assert.That(initial.ActionPoints, Is.EqualTo(4));
        Assert.That(initial.MagicalMastery, Is.EqualTo(7));

        SquadBattleRuntime runtime = new SquadBattleRuntime(squad);
        runtime.ApplyDamage(5, SquadDamageDistribution.SingleTarget);

        Assert.That(runtime.Stats.MaxHP, Is.EqualTo(20));
        Assert.That(runtime.Stats.Strength, Is.EqualTo(14));
        Assert.That(runtime.Stats.Dexterity, Is.EqualTo(10));
    }

    [Test]
    public void BaseModifiersAndCalculatedValuesRemainSeparate()
    {
        SquadData squad = CreateSquad(1);
        squad.PermanentModifiers.strength = 3;
        float commanderBase = squad.Commander.baseStats.strength;

        SquadCalculatedStats calculated = SquadStatsCalculator.Calculate(squad);

        Assert.That(commanderBase, Is.EqualTo(10));
        Assert.That(squad.PermanentModifiers.strength, Is.EqualTo(3));
        Assert.That(calculated.Strength, Is.EqualTo(15));
    }

    [Test]
    public void SingleTargetDamageStopsAfterOneWarrior()
    {
        SquadBattleRuntime runtime = new SquadBattleRuntime(CreateSquad(2));
        SquadDamageResult result = runtime.ApplyDamage(20, SquadDamageDistribution.SingleTarget);

        Assert.That(runtime.State.warriors[0].defeated, Is.True);
        Assert.That(runtime.State.warriors[1].currentHP, Is.EqualTo(5));
        Assert.That(runtime.State.commander.currentHP, Is.EqualTo(10));
        Assert.That(result.UnusedDamage, Is.EqualTo(15));
    }

    [Test]
    public void CommanderTakesSingleTargetDamageOnlyAfterWarriorsAreGone()
    {
        SquadBattleRuntime runtime = new SquadBattleRuntime(CreateSquad(1));
        runtime.ApplyDamage(99, SquadDamageDistribution.SingleTarget);
        Assert.That(runtime.State.commander.currentHP, Is.EqualTo(10));

        runtime.ApplyDamage(3, SquadDamageDistribution.SingleTarget);
        Assert.That(runtime.State.commander.currentHP, Is.EqualTo(7));
    }

    [Test]
    public void AreaDamageSpillsThroughWarriorsThenCommander()
    {
        SquadBattleRuntime runtime = new SquadBattleRuntime(CreateSquad(2));
        SquadDamageResult result = runtime.ApplyDamage(13, SquadDamageDistribution.Area);

        Assert.That(runtime.State.warriors[0].defeated, Is.True);
        Assert.That(runtime.State.warriors[1].defeated, Is.True);
        Assert.That(runtime.State.commander.currentHP, Is.EqualTo(7));
        Assert.That(result.UnusedDamage, Is.Zero);
    }

    [Test]
    public void PartialAreaDamageStopsOnCurrentWarrior()
    {
        SquadBattleRuntime runtime = new SquadBattleRuntime(CreateSquad(2));
        runtime.ApplyDamage(3, SquadDamageDistribution.Area);

        Assert.That(runtime.State.warriors[0].currentHP, Is.EqualTo(2));
        Assert.That(runtime.State.warriors[1].currentHP, Is.EqualTo(5));
    }

    [Test]
    public void CommanderDeathDefeatsSquadAndEventFiresOnce()
    {
        SquadBattleRuntime runtime = new SquadBattleRuntime(CreateSquad(1));
        int defeatedEvents = 0;
        runtime.OnSquadDefeated += () => defeatedEvents++;

        runtime.ApplyDamage(100, SquadDamageDistribution.Area);
        runtime.ApplyDamage(100, SquadDamageDistribution.Area);

        Assert.That(runtime.State.IsDefeated, Is.True);
        Assert.That(defeatedEvents, Is.EqualTo(1));
    }

    [Test]
    public void ResolveReducesMoraleLossAndActionPointsNeverGoNegative()
    {
        SquadBattleRuntime runtime = new SquadBattleRuntime(CreateSquad(1));
        float applied = runtime.ApplyMoraleLoss(6);

        Assert.That(applied, Is.EqualTo(4));
        Assert.That(runtime.TrySpendActionPoints(5), Is.False);
        Assert.That(runtime.State.currentActionPoints, Is.EqualTo(4));
        Assert.That(runtime.TrySpendActionPoints(4), Is.True);
        Assert.That(runtime.State.currentActionPoints, Is.Zero);
    }

    [Test]
    public void PrimaryStatIncreaseChangesOnlyRequestedStatByOne()
    {
        SquadData squad = CreateSquad(1);
        squad.Commander.baseStats.experienceMultiplier = 1;
        SquadBattleRuntime runtime = new SquadBattleRuntime(squad, randomValue: () => 0);
        float dexterity = squad.Commander.baseStats.dexterity;

        Assert.That(runtime.TryIncreaseUsedPrimaryStat(PrimaryStatType.Strength), Is.True);
        Assert.That(squad.Commander.baseStats.strength, Is.EqualTo(11));
        Assert.That(squad.Commander.baseStats.dexterity, Is.EqualTo(dexterity));
    }

    [Test]
    public void InitiativeContainsOneEntryPerSquad()
    {
        GameObject go = new GameObject("squad");
        try
        {
            SquadBattleController controller = go.AddComponent<SquadBattleController>();
            Assert.That(controller.Initialize(CreateSquad(1)), Is.True);

            SquadInitiativeOrder order = new SquadInitiativeOrder();
            Assert.That(order.Register(controller), Is.True);
            Assert.That(order.Register(controller), Is.False);
            Assert.That(order.Entries.Count, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SaveRoundTripRestoresCompositionAndIndividualHP()
    {
        GameObject sourceObject = new GameObject("source");
        GameObject restoredObject = new GameObject("restored");
        try
        {
            SquadData squad = CreateSquad(2);
            SquadBattleRuntime runtime = new SquadBattleRuntime(squad);
            runtime.ApplyDamage(3, SquadDamageDistribution.SingleTarget);

            SquadSaveParticipant source = sourceObject.AddComponent<SquadSaveParticipant>();
            Assert.That(source.TryAddSquad(squad, out _), Is.True);
            source.RegisterRuntime(runtime);
            source.SetActiveBattleStateSaving(true);
            string json = source.CaptureState();

            SquadSaveParticipant restored = restoredObject.AddComponent<SquadSaveParticipant>();
            restored.RestoreState(json);

            Assert.That(restored.Squads.Count, Is.EqualTo(1));
            Assert.That(restored.Squads[0].Warriors.Count, Is.EqualTo(2));
            Assert.That(restored.GetRestoredBattleState("squad").warriors[0].currentHP, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(restoredObject);
        }
    }

    private static SquadData CreateSquad(int warriorCount)
    {
        List<WarriorData> warriors = new List<WarriorData>();
        for (int i = 0; i < warriorCount; i++)
            warriors.Add(Warrior($"w-{i}"));
        return new SquadData("squad", Commander(), warriors);
    }

    private static CommanderData Commander()
    {
        return new CommanderData
        {
            id = "commander",
            baseStats = new SquadBaseStats
            {
                hp = 10,
                actionPoints = 4,
                strength = 10,
                dexterity = 8,
                magicalMastery = 7,
                morale = 20,
                resolve = 2
            }
        };
    }

    private static WarriorData Warrior(string id)
    {
        return new WarriorData
        {
            id = id,
            maxHP = 5,
            strength = 2,
            dexterity = 1
        };
    }
}
