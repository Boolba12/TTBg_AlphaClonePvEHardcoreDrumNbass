using NUnit.Framework;

public sealed class CommanderPortraitDatabaseBuilderTests
{
    [TestCase("Assets/Art/CommanderPortraits/Humans/A.png", CommanderRace.Human)]
    [TestCase("Assets/Art/CommanderPortraits/Elves/A.png", CommanderRace.Elf)]
    [TestCase("Assets/Art/CommanderPortraits/Dwarves/A.png", CommanderRace.Dwarf)]
    [TestCase("Assets/Art/CommanderPortraits/Orcs/A.png", CommanderRace.Orc)]
    [TestCase("Assets/Art/CommanderPortraits/Tieflings/A.png", CommanderRace.Tiefling)]
    [TestCase("Assets/Scripts/CommanderPortraits/CommanderPortraitHuman/A.png", CommanderRace.Human)]
    [TestCase("Assets/Scripts/CommanderPortraits/CommanderPortraitElf/A.png", CommanderRace.Elf)]
    public void RaceComesFromFolder(string path, CommanderRace expected)
    {
        Assert.That(
            CommanderPortraitDatabaseBuilder.TryGetRaceFromAssetPath(path, out CommanderRace actual),
            Is.True);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void UnrelatedFolderIsRejected()
    {
        Assert.That(
            CommanderPortraitDatabaseBuilder.TryGetRaceFromAssetPath(
                "Assets/Materials/Humans/not-a-portrait.png",
                out _),
            Is.False);
    }

    [Test]
    public void UnicodeFilenameIsAccepted()
    {
        Assert.That(
            CommanderPortraitDatabaseBuilder.IsPortraitImagePath(
                "Assets/Art/CommanderPortraits/Humans/Командир №1.png"),
            Is.True);
    }
}
