using System;
using System.Collections.Generic;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class EngineNeutralDefaultsTests
{
    private string _tempRoot = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "Workes.SaveSystem.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public void CreateDefault_WithExplicitRoot_SavesAndLoadsFromProvidedPath()
    {
        var serializer = new JsonSaveSerializer();
        var manager = SaveManager<string>.CreateDefault(serializer, _tempRoot);
        var provider = new TestProvider("player", new TestState { Name = "Saved", Level = 4 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot");
        provider.Current = new TestState { Name = "Changed", Level = 1 };

        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Name, Is.EqualTo("Saved"));
        Assert.That(provider.Current.Level, Is.EqualTo(4));
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "player.json")), Is.True);
    }

    [Test]
    public void SaveManager_AllowsPlainStringIdentities()
    {
        var manager = new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                saveNameResolver: identity => identity,
                fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver));
        var provider = new TestProvider("player", new TestState { Name = "Saved", Level = 4 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot");
        provider.Current = new TestState { Name = "Changed", Level = 1 };

        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Name, Is.EqualTo("Saved"));
        Assert.That(provider.Current.Level, Is.EqualTo(4));
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "player.json")), Is.True);
    }

    [Test]
    public void SaveManager_AllowsCustomValueIdentitiesWithoutMarkerInterface()
    {
        var manager = new SaveManager<ProfileSlotIdentity>(
            new SaveSystemOptions<ProfileSlotIdentity>(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<ProfileSlotIdentity>.DefaultTempFolderName(),
                saveNameResolver: identity => identity.ProfileId + "-" + identity.SlotId,
                fileNameResolver: SaveSystemOptions<ProfileSlotIdentity>.DefaultFileNameResolver));
        var provider = new TestProvider("player", new TestState { Name = "Saved", Level = 4 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        manager.SaveToDisk(new ProfileSlotIdentity("profile-a", "slot-1"));
        provider.Current = new TestState { Name = "Changed", Level = 1 };

        var loaded = manager.LoadFromDisk(new ProfileSlotIdentity("profile-a", "slot-1"));

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Name, Is.EqualTo("Saved"));
        Assert.That(provider.Current.Level, Is.EqualTo(4));
        Assert.That(File.Exists(Path.Combine(_tempRoot, "profile-a-slot-1", "player.json")), Is.True);
    }

    [Test]
    public void SaveSystemOptionsCreate_ForStringIdentityUsesDefaultNaming()
    {
        var warnings = new List<string>();
        Action<string> warningSink = warnings.Add;
        var options = SaveSystemOptions.Create(
            saveRootPath: _tempRoot,
            serializer: new JsonSaveSerializer(),
            warningSink: warningSink);

        Assert.That(options.SaveRootPath, Is.EqualTo(_tempRoot));
        Assert.That(options.SaveNameResolver("slot"), Is.EqualTo("slot"));
        Assert.That(options.TempFolderName, Is.EqualTo("_tmp"));
        Assert.That(options.FileNameResolver(new SaveFileContext("player", 1, typeof(JsonSaveSerializer))), Is.EqualTo("player"));
        Assert.That(options.MissingProviderFileBehavior, Is.EqualTo(MissingProviderFileBehavior.Throw));
        Assert.That(options.WarningSink, Is.SameAs(warningSink));
        Assert.That(options.EnableBackupSystem, Is.False);
    }

    [Test]
    public void SaveSystemOptionsCreate_ForCustomIdentityUsesProvidedSaveNameResolver()
    {
        var options = SaveSystemOptions.Create<ProfileSlotIdentity>(
            saveRootPath: _tempRoot,
            serializer: new JsonSaveSerializer(),
            saveNameResolver: identity => identity.ProfileId + "-" + identity.SlotId);

        var saveName = options.SaveNameResolver(new ProfileSlotIdentity("profile-a", "slot-1"));

        Assert.That(saveName, Is.EqualTo("profile-a-slot-1"));
        Assert.That(options.TempFolderName, Is.EqualTo("_tmp"));
        Assert.That(options.EnableBackupSystem, Is.False);
    }

    [Test]
    public void SaveSystemOptionsCreateWithBackups_EnablesBackups()
    {
        var options = SaveSystemOptions.CreateWithBackups(
            saveRootPath: _tempRoot,
            serializer: new JsonSaveSerializer(),
            backupSystemMaxBackupCount: 3,
            missingProviderFileBehavior: MissingProviderFileBehavior.Skip);

        Assert.That(options.EnableBackupSystem, Is.True);
        Assert.That(options.BackupSystemMaxBackupCount, Is.EqualTo(3));
        Assert.That(options.MissingProviderFileBehavior, Is.EqualTo(MissingProviderFileBehavior.Skip));
    }

    [Test]
    public void SaveSystemOptions_RejectsEmptySaveRootPath()
    {
        var ex = Assert.Throws<ArgumentException>(() => CreateOptions(string.Empty));

        Assert.That(ex!.ParamName, Is.EqualTo("saveRootPath"));
    }

    [Test]
    public void SaveSystemOptions_RejectsNullSerializer()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: null!,
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                saveNameResolver: id => id,
                fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver));

        Assert.That(ex!.ParamName, Is.EqualTo("serializer"));
    }

    [Test]
    public void SaveSystemOptions_RejectsNullSaveNameResolver()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                saveNameResolver: null!,
                fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver));

        Assert.That(ex!.ParamName, Is.EqualTo("saveNameResolver"));
    }

    [Test]
    public void SaveManager_RejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new SaveManager<string>(null!));

        Assert.That(ex!.ParamName, Is.EqualTo("options"));
    }

    [Test]
    public void SaveManager_RejectsNullIdentityForSave()
    {
        var manager = CreateStringManager();
        manager.RegisterProvider(new TestProvider("player", new TestState { Name = "Saved", Level = 4 }));

        var ex = Assert.Throws<ArgumentNullException>(() => manager.SaveToDisk(null!));

        Assert.That(ex!.ParamName, Is.EqualTo("identity"));
    }

    [Test]
    public void SaveManager_RejectsNullIdentityForLoad()
    {
        var manager = CreateStringManager();

        var ex = Assert.Throws<ArgumentNullException>(() => manager.LoadFromDisk(null!));

        Assert.That(ex!.ParamName, Is.EqualTo("identity"));
    }

    [Test]
    public void SaveManager_RejectsNullIdentityForBackupLoad()
    {
        var manager = CreateStringManager(enableBackupSystem: true, backupSystemMaxBackupCount: 1);

        var ex = Assert.Throws<ArgumentNullException>(() => manager.LoadBackupSlotFromDisk(null!, slotNumber: 1));

        Assert.That(ex!.ParamName, Is.EqualTo("identity"));
    }

    [Test]
    public void SaveManager_RejectsNullIdentityForRecovery()
    {
        var manager = CreateStringManager();

        var ex = Assert.Throws<ArgumentNullException>(() => manager.RecoverSave(null!));

        Assert.That(ex!.ParamName, Is.EqualTo("identity"));
    }

    private SaveSystemOptions<string> CreateOptions(string saveRootPath)
    {
        return new SaveSystemOptions<string>(
            saveRootPath: saveRootPath,
            serializer: new JsonSaveSerializer(),
            tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
            saveNameResolver: id => id,
            fileNameResolver: null);
    }

    private SaveManager<string> CreateStringManager(
        bool enableBackupSystem = false,
        int backupSystemMaxBackupCount = 0)
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                saveNameResolver: id => id,
                fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver,
                enableBackupSystem: enableBackupSystem,
                backupSystemMaxBackupCount: backupSystemMaxBackupCount));
    }

    private readonly struct ProfileSlotIdentity
    {
        public ProfileSlotIdentity(string profileId, string slotId)
        {
            ProfileId = profileId;
            SlotId = slotId;
        }

        public string ProfileId { get; }

        public string SlotId { get; }
    }

    private sealed class TestState
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
    }

    private sealed class TestProvider : ISaveProvider<TestState>
    {
        public TestProvider(string saveKey, TestState current)
        {
            SaveKey = saveKey;
            Current = current;
        }

        public string SaveKey { get; }
        public int SchemaVersion => 1;
        public int LoadPriority => 0;
        public TestState Current { get; set; }

        public TestState CaptureState()
        {
            return Current;
        }

        public void RestoreState(TestState state)
        {
            Current = state;
        }
    }
}
