using System;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class FileSystemSafetyTests
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
    public void SaveToDisk_RejectsInvalidResolvedSaveNames()
    {
        var manager = CreateManager(identity => identity.SaveName);
        manager.RegisterProvider<TestState>(new TestProvider("player", new TestState { Value = 1 }));

        var ex = Assert.Throws<InvalidOperationException>(() => manager.SaveToDisk(new StringSaveIdentity("bad/name")));

        Assert.That(ex!.Message, Does.Contain("invalid path characters"));
    }

    [Test]
    public void SaveToDisk_RejectsInvalidResolvedFileNames()
    {
        var manager = CreateManager(fileNameResolver: _ => "bad/name");
        manager.RegisterProvider<TestState>(new TestProvider("player", new TestState { Value = 1 }));

        var ex = Assert.Throws<InvalidOperationException>(() => manager.SaveToDisk(new StringSaveIdentity("slot")));

        Assert.That(ex!.Message, Does.Contain("invalid characters"));
    }

    [Test]
    public void Options_RejectTempFolderNamesThatContainPathCharacters()
    {
        var ex = Assert.Throws<ArgumentException>(() => CreateOptions(tempFolderName: "../tmp"));

        Assert.That(ex!.ParamName, Is.EqualTo("tempFolderName"));
    }

    [Test]
    public void BackupRotation_DoesNotTreatSimilarSaveNamesAsTheSameSave()
    {
        var alphaManager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 3);
        var alphaProvider = new TestProvider("player", new TestState { Value = 1 });
        alphaManager.RegisterProvider<TestState>(alphaProvider);

        var alpha2Manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 3);
        var alpha2Provider = new TestProvider("player", new TestState { Value = 10 });
        alpha2Manager.RegisterProvider<TestState>(alpha2Provider);

        alphaManager.SaveToDisk(new StringSaveIdentity("alpha"));
        alphaProvider.Current = new TestState { Value = 2 };
        alphaManager.SaveToDisk(new StringSaveIdentity("alpha"));

        alpha2Manager.SaveToDisk(new StringSaveIdentity("alpha2"));
        alpha2Provider.Current = new TestState { Value = 11 };
        alpha2Manager.SaveToDisk(new StringSaveIdentity("alpha2"));

        alphaProvider.Current = new TestState { Value = 3 };
        alphaManager.SaveToDisk(new StringSaveIdentity("alpha"));

        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "alpha_0001")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "alpha_0002")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "alpha2_0001")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "alpha2_0002")), Is.False);
    }

    private SaveManager<StringSaveIdentity> CreateManager(
        Func<StringSaveIdentity, string>? saveNameResolver = null,
        Func<SaveFileContext, string>? fileNameResolver = null,
        bool enableBackupSystem = false,
        int backupSystemMaxBackupCount = 0)
    {
        return new SaveManager<StringSaveIdentity>(
            CreateOptions(
                saveNameResolver: saveNameResolver,
                fileNameResolver: fileNameResolver,
                enableBackupSystem: enableBackupSystem,
                backupSystemMaxBackupCount: backupSystemMaxBackupCount));
    }

    private SaveSystemOptions<StringSaveIdentity> CreateOptions(
        string? tempFolderName = null,
        Func<StringSaveIdentity, string>? saveNameResolver = null,
        Func<SaveFileContext, string>? fileNameResolver = null,
        bool enableBackupSystem = false,
        int backupSystemMaxBackupCount = 0)
    {
        return new SaveSystemOptions<StringSaveIdentity>(
            saveRootPath: _tempRoot,
            serializer: new JsonSaveSerializer(),
            tempFolderName: tempFolderName ?? SaveSystemOptions<StringSaveIdentity>.DefaultTempFolderName(),
            saveNameResolver: saveNameResolver ?? (identity => identity.SaveName),
            fileNameResolver: fileNameResolver ?? SaveSystemOptions<StringSaveIdentity>.DefaultFileNameResolver,
            enableBackupSystem: enableBackupSystem,
            backupSystemMaxBackupCount: backupSystemMaxBackupCount);
    }

    public sealed class TestState
    {
        public int Value { get; set; }
    }

    private sealed class TestProvider : ISaveProvider
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

        public object CaptureState()
        {
            return Current;
        }

        public void RestoreState(object state)
        {
            Current = (TestState)state;
        }
    }
}
