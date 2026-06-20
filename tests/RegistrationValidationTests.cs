using System;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class RegistrationValidationTests
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
    public void RegisterProvider_DoesNotCaptureState()
    {
        var manager = CreateManager();
        var provider = new CountingProvider();

        manager.RegisterProvider(provider);

        Assert.That(provider.CaptureCount, Is.Zero);
    }

    [Test]
    public void SaveToDisk_RequiresValidatedRegistrations()
    {
        var manager = CreateManager();
        manager.RegisterProvider(new CountingProvider());

        var ex = Assert.Throws<InvalidOperationException>(() => manager.SaveToDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("ValidateRegistrations"));
    }

    [Test]
    public void LoadFromDisk_RequiresValidatedRegistrations()
    {
        var manager = CreateManager();
        manager.RegisterProvider(new CountingProvider());

        var ex = Assert.Throws<InvalidOperationException>(() => manager.LoadFromDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("ValidateRegistrations"));
    }

    [Test]
    public void LoadBackupSlotFromDisk_RequiresValidatedRegistrations()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 1);
        manager.RegisterProvider(new CountingProvider());

        var ex = Assert.Throws<InvalidOperationException>(() => manager.LoadBackupSlotFromDisk("slot", slotNumber: 1));

        Assert.That(ex!.Message, Does.Contain("ValidateRegistrations"));
    }

    [Test]
    public void ValidateRegistrations_CapturesProviderStateAtCallerChosenTime()
    {
        var manager = CreateManager();
        var provider = new CountingProvider();
        manager.RegisterProvider(provider);

        manager.ValidateRegistrations();

        Assert.That(provider.CaptureCount, Is.EqualTo(1));
    }

    [Test]
    public void ValidateRegistrations_RejectsProviderCaptureFailures()
    {
        var manager = CreateManager();
        var provider = new CountingProvider { ThrowOnCapture = true };
        manager.RegisterProvider(provider);

        var ex = Assert.Throws<InvalidOperationException>(() => manager.ValidateRegistrations());

        Assert.That(ex!.Message, Does.Contain("failed while capturing state"));
    }

    [Test]
    public void ValidateRegistrations_AllowsNewtonsoftCompatibleStateWithoutParameterlessConstructor()
    {
        var manager = CreateManager();
        var provider = new ConstructorStateProvider(new ConstructorState("Rook", 5));
        manager.RegisterProvider(provider);

        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");
        provider.Current = new ConstructorState("Changed", 1);
        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Name, Is.EqualTo("Rook"));
        Assert.That(provider.Current.Level, Is.EqualTo(5));
    }

    [Test]
    public void ValidateRegistrations_RejectsStateThatCannotSerialize()
    {
        var manager = CreateManager();
        manager.RegisterProvider(new UnserializableProvider());

        var ex = Assert.Throws<InvalidOperationException>(() => manager.ValidateRegistrations());

        Assert.That(ex!.Message, Does.Contain("incompatible state"));
    }

    private SaveManager<string> CreateManager(
        bool enableBackupSystem = false,
        int backupSystemMaxBackupCount = 0)
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                saveNameResolver: identity => identity,
                fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver,
                enableBackupSystem: enableBackupSystem,
                backupSystemMaxBackupCount: backupSystemMaxBackupCount));
    }

    public sealed class TestState
    {
        public int Value { get; set; }
    }

    public sealed class ConstructorState
    {
        public ConstructorState(string name, int level)
        {
            Name = name;
            Level = level;
        }

        public string Name { get; }

        public int Level { get; }
    }

    public sealed class UnserializableState
    {
        public Stream Stream { get; } = Stream.Null;
    }

    private sealed class CountingProvider : ISaveProvider<TestState>
    {
        public string SaveKey => "player";

        public int SchemaVersion => 1;

        public int LoadPriority => 0;

        public int CaptureCount { get; private set; }

        public bool ThrowOnCapture { get; set; }

        public TestState CaptureState()
        {
            CaptureCount++;
            if (ThrowOnCapture)
                throw new InvalidOperationException("capture failed");

            return new TestState { Value = 1 };
        }

        public void RestoreState(TestState state)
        {
        }
    }

    private sealed class ConstructorStateProvider : ISaveProvider<ConstructorState>
    {
        public ConstructorStateProvider(ConstructorState current)
        {
            Current = current;
        }

        public string SaveKey => "constructor";

        public int SchemaVersion => 1;

        public int LoadPriority => 0;

        public ConstructorState Current { get; set; }

        public ConstructorState CaptureState()
        {
            return Current;
        }

        public void RestoreState(ConstructorState state)
        {
            Current = state;
        }
    }

    private sealed class UnserializableProvider : ISaveProvider<UnserializableState>
    {
        public string SaveKey => "unserializable";

        public int SchemaVersion => 1;

        public int LoadPriority => 0;

        public UnserializableState CaptureState()
        {
            return new UnserializableState();
        }

        public void RestoreState(UnserializableState state)
        {
        }
    }
}
