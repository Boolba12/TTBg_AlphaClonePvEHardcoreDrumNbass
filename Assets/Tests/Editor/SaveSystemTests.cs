using System;
using System.IO;
using NUnit.Framework;

public sealed class SaveSystemTests
{
    private string testRoot;
    private JsonSaveFileStorage storage;
    private SaveService service;

    [SetUp]
    public void SetUp()
    {
        testRoot = Path.Combine(Path.GetTempPath(), "TTBgSaveTests", Guid.NewGuid().ToString("N"));
        storage = new JsonSaveFileStorage(testRoot);
        service = new SaveService(storage);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(testRoot))
            Directory.Delete(testRoot, true);
    }

    [Test]
    public void CreateNew_ProducesValidIdentityAndVersion()
    {
        GameSaveData data = service.CreateNew("first_try");
        Assert.That(data.saveId, Is.Not.Empty);
        Assert.That(data.formatVersion, Is.EqualTo(SaveService.CurrentFormatVersion));
        Assert.That(data.sceneName, Is.EqualTo("first_try"));
    }

    [Test]
    public void SaveAndRead_PreservesUnicodeAndOptionalFields()
    {
        GameSaveData data = service.CreateNew("Світ_α");
        data.playerProgress.mapSeed = 42;

        Assert.That(service.Save("slot", data).Success, Is.True);
        SaveReadResult result = service.Read("slot");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.sceneName, Is.EqualTo("Світ_α"));
        Assert.That(result.Data.playerProgress.mapSeed, Is.EqualTo(42));
        Assert.That(result.Data.systems, Is.Not.Null);
    }

    [Test]
    public void Read_WhenFileIsMissing_ReturnsFailure()
    {
        Assert.That(service.Read("missing").Success, Is.False);
    }

    [Test]
    public void Read_WhenMainIsCorrupt_RecoversBackup()
    {
        GameSaveData first = service.CreateNew("first_try");
        first.playerProgress.mapSeed = 10;
        Assert.That(service.Save("slot", first).Success, Is.True);

        first.playerProgress.mapSeed = 20;
        Assert.That(service.Save("slot", first).Success, Is.True);
        File.WriteAllText(storage.GetSavePath("slot"), "{broken");

        SaveReadResult result = service.Read("slot");
        Assert.That(result.Success, Is.True);
        Assert.That(result.RecoveredFromBackup, Is.True);
        Assert.That(result.Data.playerProgress.mapSeed, Is.EqualTo(10));
    }

    [Test]
    public void Save_WhenCaptureReenters_IsRejectedWithoutParallelWrite()
    {
        ReentrantSaveable saveable = new ReentrantSaveable();
        service.Register(saveable);
        GameSaveData data = service.CreateNew("first_try");
        saveable.OnCapture = () => service.Save("nested", data);

        SaveOperationResult outer = service.Save("slot", data);

        Assert.That(outer.Success, Is.True);
        Assert.That(saveable.NestedResult.Success, Is.False);
        Assert.That(File.Exists(storage.GetSavePath("nested")), Is.False);
    }

    private sealed class ReentrantSaveable : ISaveable
    {
        public string SaveKey => "reentrant";
        public Func<SaveOperationResult> OnCapture;
        public SaveOperationResult NestedResult;

        public string CaptureState()
        {
            NestedResult = OnCapture();
            return "{}";
        }

        public void RestoreState(string json)
        {
        }
    }
}
