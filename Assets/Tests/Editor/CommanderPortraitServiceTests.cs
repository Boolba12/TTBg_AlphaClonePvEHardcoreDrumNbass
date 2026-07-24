using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CommanderPortraitServiceTests
{
    private CommanderPortraitDatabase database;

    [SetUp]
    public void SetUp()
    {
        database = ScriptableObject.CreateInstance<CommanderPortraitDatabase>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(database);
    }

    [Test]
    public void PortraitsDoNotRepeatUntilRacePoolIsExhausted()
    {
        SetEntries(
            Entry("human-a", CommanderRace.Human),
            Entry("human-b", CommanderRace.Human),
            Entry("human-c", CommanderRace.Human));
        CommanderPortraitService service = new CommanderPortraitService(database, 7);

        HashSet<string> firstCycle = new HashSet<string>
        {
            service.GetRandomPortrait(CommanderRace.Human).Id,
            service.GetRandomPortrait(CommanderRace.Human).Id,
            service.GetRandomPortrait(CommanderRace.Human).Id
        };

        Assert.That(firstCycle.Count, Is.EqualTo(3));
        Assert.That(service.GetRandomPortrait(CommanderRace.Human), Is.Not.Null);
    }

    [Test]
    public void RacePoolsAreIndependent()
    {
        SetEntries(
            Entry("human", CommanderRace.Human),
            Entry("elf", CommanderRace.Elf));
        CommanderPortraitService service = new CommanderPortraitService(database, 3);

        Assert.That(service.GetRandomPortrait(CommanderRace.Human).Id, Is.EqualTo("human"));
        Assert.That(service.GetRandomPortrait(CommanderRace.Elf).Id, Is.EqualTo("elf"));
    }

    [Test]
    public void AssignPortraitIfMissing_DoesNotReplaceValidPortrait()
    {
        SetEntries(Entry("orc", CommanderRace.Orc));
        CommanderPortraitService service = new CommanderPortraitService(database, 1);
        PortraitTarget target = new PortraitTarget();

        CommanderPortraitEntry first = service.AssignPortraitIfMissing(target, CommanderRace.Orc);
        CommanderPortraitEntry second = service.AssignPortraitIfMissing(target, CommanderRace.Orc);

        Assert.That(second.Id, Is.EqualTo(first.Id));
        Assert.That(target.CommanderPortraitId, Is.EqualTo("orc"));
    }

    [Test]
    public void RestoredPoolContinuesWithoutReusingConsumedPortrait()
    {
        SetEntries(
            Entry("dwarf-a", CommanderRace.Dwarf),
            Entry("dwarf-b", CommanderRace.Dwarf),
            Entry("dwarf-c", CommanderRace.Dwarf));
        CommanderPortraitService original = new CommanderPortraitService(database, 11);
        string consumed = original.GetRandomPortrait(CommanderRace.Dwarf).Id;
        CommanderPortraitPoolState state = original.CaptureState();

        CommanderPortraitService restored = new CommanderPortraitService(database, 99);
        restored.RestoreState(state);
        string next = restored.GetRandomPortrait(CommanderRace.Dwarf).Id;

        Assert.That(next, Is.Not.EqualTo(consumed));
    }

    [Test]
    public void AddedPortraitJoinsCurrentPoolWithoutResettingUsedEntries()
    {
        SetEntries(
            Entry("tiefling-a", CommanderRace.Tiefling),
            Entry("tiefling-b", CommanderRace.Tiefling));
        CommanderPortraitService service = new CommanderPortraitService(database, 5);
        string used = service.GetRandomPortrait(CommanderRace.Tiefling).Id;

        SetEntries(
            Entry("tiefling-a", CommanderRace.Tiefling),
            Entry("tiefling-b", CommanderRace.Tiefling),
            Entry("tiefling-new", CommanderRace.Tiefling));

        HashSet<string> remainder = new HashSet<string>
        {
            service.GetRandomPortrait(CommanderRace.Tiefling).Id,
            service.GetRandomPortrait(CommanderRace.Tiefling).Id
        };

        Assert.That(remainder, Does.Contain("tiefling-new"));
        Assert.That(remainder, Does.Not.Contain(used));
    }

    [Test]
    public void MissingRaceReturnsControlledNull()
    {
        SetEntries(Entry("human", CommanderRace.Human));
        CommanderPortraitService service = new CommanderPortraitService(database, 2);
        Assert.That(service.GetRandomPortrait(CommanderRace.Elf), Is.Null);
    }

    private void SetEntries(params CommanderPortraitEntry[] entries)
    {
        database.ReplaceEntries(new List<CommanderPortraitEntry>(entries));
    }

    private static CommanderPortraitEntry Entry(string id, CommanderRace race)
    {
        return new CommanderPortraitEntry(id, null, race, id);
    }

    private sealed class PortraitTarget : ICommanderPortraitTarget
    {
        public string CommanderPortraitId { get; set; }
    }
}
