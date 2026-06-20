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
    public void TryRegisterProvider_WhenValidationSucceeds_RegistersProviderAndLeavesManagerValidated()
    {
        var manager = CreateManager();
        var provider = new MutableProvider("player", schemaVersion: 1);

        var registered = manager.TryRegisterProvider(provider, out var error);

        Assert.That(registered, Is.True);
        Assert.That(error, Is.Null);
        Assert.DoesNotThrow(() => manager.SaveToDisk("slot"));
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "player.json")), Is.True);
    }

    [Test]
    public void TryRegisterProvider_WhenValidationFails_DoesNotKeepProvider()
    {
        var manager = CreateManager();
        var provider = new CountingProvider { ThrowOnCapture = true };

        var registered = manager.TryRegisterProvider(provider, out var error);

        Assert.That(registered, Is.False);
        Assert.That(error, Does.Contain("failed while capturing state"));
        Assert.That(manager.CaptureSnapshot().Entries, Is.Empty);
    }

    [Test]
    public void TryRegisterProvider_WhenValidationFails_RestoresPreviousValidationState()
    {
        var manager = CreateManager();
        var existingProvider = new MutableProvider("player", schemaVersion: 1);
        manager.RegisterProvider(existingProvider);
        manager.ValidateRegistrations();
        var invalidProvider = new NullStateProvider();

        var registered = manager.TryRegisterProvider(invalidProvider, out var error);

        Assert.That(registered, Is.False);
        Assert.That(error, Does.Contain("returned null state"));
        Assert.DoesNotThrow(() => manager.SaveToDisk("slot"));
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "player.json")), Is.True);
    }

    [Test]
    public void TryRegisterMemoryProvider_WhenValidationSucceeds_RegistersProviderAndLeavesManagerValidated()
    {
        var manager = CreateManager();
        var provider = new MutableProvider("cache", schemaVersion: 1);

        var registered = manager.TryRegisterMemoryProvider(provider, out var error);

        Assert.That(registered, Is.True);
        Assert.That(error, Is.Null);
        Assert.DoesNotThrow(() => manager.SaveToDisk("slot"));
        Assert.That(manager.CaptureSnapshot().Entries, Has.Count.EqualTo(1));
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

    [Test]
    public void ValidateRegistrations_RejectsNullProviderState()
    {
        var manager = CreateManager();
        manager.RegisterProvider(new NullStateProvider());

        var ex = Assert.Throws<InvalidOperationException>(() => manager.ValidateRegistrations());

        Assert.That(ex!.Message, Does.Contain("returned null state"));
    }

    [Test]
    public void ValidateRegistrations_RejectsDuplicateResolvedProviderFileNames()
    {
        var manager = CreateManager(fileNameResolver: _ => "shared");
        manager.RegisterProvider(new MutableProvider("player", schemaVersion: 1));
        manager.RegisterProvider(new MutableProvider("inventory", schemaVersion: 1));

        var ex = Assert.Throws<InvalidOperationException>(() => manager.ValidateRegistrations());

        Assert.That(ex!.Message, Does.Contain("both resolve"));
        Assert.That(ex.Message, Does.Contain("shared.json"));
    }

    [Test]
    public void ValidateRegistrations_RejectsProviderSaveKeyChangedAfterRegistration()
    {
        var manager = CreateManager();
        var provider = new MutableProvider("player", schemaVersion: 1);
        manager.RegisterProvider(provider);
        provider.SaveKey = "hero";

        var ex = Assert.Throws<InvalidOperationException>(() => manager.ValidateRegistrations());

        Assert.That(ex!.Message, Does.Contain("changed its SaveKey"));
    }

    [Test]
    public void SaveToDisk_RejectsProviderSaveKeyChangedAfterValidation()
    {
        var manager = CreateManager();
        var provider = new MutableProvider("player", schemaVersion: 1);
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        provider.SaveKey = "hero";

        var ex = Assert.Throws<InvalidOperationException>(() => manager.SaveToDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("changed its SaveKey"));
    }

    [Test]
    public void LoadFromDisk_RejectsProviderSchemaVersionChangedAfterValidation()
    {
        var writer = CreateManager();
        var writerProvider = new MutableProvider("player", schemaVersion: 1);
        writer.RegisterProvider(writerProvider);
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var reader = CreateManager();
        var readerProvider = new MutableProvider("player", schemaVersion: 1);
        reader.RegisterProvider(readerProvider);
        reader.ValidateRegistrations();
        readerProvider.SchemaVersion = 2;

        var ex = Assert.Throws<InvalidOperationException>(() => reader.LoadFromDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("changed its SchemaVersion"));
    }

    [Test]
    public void SaveToDisk_RejectsMemoryProviderSchemaVersionChangedAfterValidation()
    {
        var manager = CreateManager();
        var provider = new MutableProvider("cache", schemaVersion: 1);
        manager.RegisterMemoryProvider(provider);
        manager.ValidateRegistrations();
        provider.SchemaVersion = 2;

        var ex = Assert.Throws<InvalidOperationException>(() => manager.SaveToDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("changed its SchemaVersion"));
    }

    private SaveManager<string> CreateManager(
        bool enableBackupSystem = false,
        int backupSystemMaxBackupCount = 0,
        Func<SaveFileContext, string>? fileNameResolver = null)
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                savePathResolver: identity => identity,
                fileNameResolver: fileNameResolver ?? SaveSystemOptions<string>.DefaultFileNameResolver,
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

    private sealed class NullStateProvider : ISaveProvider<TestState>
    {
        public string SaveKey => "null-state";

        public int SchemaVersion => 1;

        public int LoadPriority => 0;

        public TestState CaptureState()
        {
            return null!;
        }

        public void RestoreState(TestState state)
        {
        }
    }

    private sealed class MutableProvider : ISaveProvider<TestState>
    {
        public MutableProvider(string saveKey, int schemaVersion)
        {
            SaveKey = saveKey;
            SchemaVersion = schemaVersion;
        }

        public string SaveKey { get; set; }

        public int SchemaVersion { get; set; }

        public int LoadPriority => 0;

        public TestState Current { get; set; } = new TestState { Value = 1 };

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
