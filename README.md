# Workes.SaveSystem

`Workes.SaveSystem` is an engine-neutral .NET save system for registering state providers, capturing save snapshots, writing them to disk, loading them back, rotating backups, and migrating saved data between schema versions.

The package is not tied to Unity, Godot, or any game engine. Engine projects choose the save root path and pass it to the save manager.

## Installation

NuGet publishing is planned after release versioning and licensing are finalized. Until then, reference the project directly:

```xml
<ItemGroup>
  <ProjectReference Include="..\Workes.SaveSystem\src\Workes.SaveSystem.csproj" />
</ItemGroup>
```

The main project is `src/Workes.SaveSystem.csproj`. The project already includes NuGet package metadata and README packaging for `Workes.SaveSystem`.

## Dependency Notes

`Workes.SaveSystem` currently has a direct dependency on `Newtonsoft.Json` 13.0.3.

That dependency is intentional for the current package shape:

- `JsonSaveSerializer` is the built-in serializer.
- JSON schematics wrap provider state in a versioned payload.
- JSON migration support parses and writes JSON through Newtonsoft while exposing package-owned migration data nodes.
- save metadata is serialized through the active serializer.

`System.Text.Json` is not part of the core package for the first reusable version. Under the current `netstandard2.1` target, using it requires an additional package reference, so adding a parallel `System.Text.Json` serializer would not make the package dependency-free. Replacing Newtonsoft would still require replacing the built-in JSON serializer and metadata persistence behavior together. For now, consumers should treat Newtonsoft as part of the package contract.

A future `System.Text.Json` adapter is still possible, especially for applications that do not need migration data nodes or that target newer frameworks directly. Treat it as a separate compatibility decision rather than a drop-in replacement for existing saves.

GZip compression is available through the .NET platform libraries and does not require another NuGet dependency. MessagePack support is intended to be provided by the optional `Workes.SaveSystem.MessagePack` package because it brings its own serializer dependency. The package shape is:

```text
Workes.SaveSystem
Workes.SaveSystem.MessagePack
```

The core `Workes.SaveSystem` package does not reference MessagePack. Applications that want MessagePack saves should install the companion package when it is available:

```xml
<ItemGroup>
  <PackageReference Include="Workes.SaveSystem.MessagePack" Version="0.1.0" />
</ItemGroup>
```

After installation, usage should look like normal serializer usage:

```csharp
var serializer = new MessagePackSaveSerializer();
var manager = SaveManager<string>.CreateDefault(
    serializer,
    saveRootPath: "Saves");
```

or, for compressed JSON using only the core package and platform compression:

```csharp
var serializer = new CompressedSaveSerializer(
    new JsonSaveSerializer(JsonSaveFormatting.Compact));
```

MessagePack is intended for compact production saves through the companion package. JSON remains the built-in readable serializer and the recommended default while the package is still converging.

## Quick Start

Create a save manager, register providers, then save and load a slot.

```csharp
using Workes.SaveSystem;

var serializer = new JsonSaveSerializer();
var manager = SaveManager<string>.CreateDefault(
    serializer,
    saveRootPath: "Saves");

var playerProvider = new PlayerSaveProvider();
manager.RegisterProvider(playerProvider);
manager.ValidateRegistrations();

manager.SaveToDisk("slot-1");

playerProvider.Current = new PlayerState();
manager.LoadFromDisk("slot-1");
```

```csharp
public sealed class PlayerState
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
}

public sealed class PlayerSaveProvider : ISaveProvider<PlayerState>
{
    public string SaveKey => "player";
    public int SchemaVersion => 1;
    public int LoadPriority => 0;

    public PlayerState Current { get; set; } = new PlayerState
    {
        Name = "Rook",
        Level = 5
    };

    public PlayerState CaptureState()
    {
        return Current;
    }

    public void RestoreState(PlayerState state)
    {
        Current = state;
    }
}
```

## Normal Usage

`SaveManager<TIdentity>` is the main coordinator. It owns provider registration, snapshot creation, disk persistence, loading, recovery, backups, and migration coordination.

Use `string` identities for simple slot names unless the application needs a custom identity type.

```csharp
var manager = new SaveManager<string>(
    SaveSystemOptions.Create(
        saveRootPath: "Saves",
        serializer: new JsonSaveSerializer()));
```

`CreateDefault(ISaveSerializer)` is a convenience factory for plain .NET applications and writes under the current user's application data folder. Engine integrations should prefer `CreateDefault(ISaveSerializer, string)`, `SaveSystemOptions.Create(...)`, or the options constructor so the engine owns the persistent data path.

Use `JsonSaveSerializer` for directly readable save files. The default constructor writes indented JSON, and `JsonSaveFormatting.Compact` writes the same `.json` payload shape without formatting whitespace.

```csharp
var manager = new SaveManager<string>(
    SaveSystemOptions.Create(
        saveRootPath: "Saves",
        serializer: new JsonSaveSerializer()));
```

```csharp
var compactManager = new SaveManager<string>(
    SaveSystemOptions.Create(
        saveRootPath: "Saves",
        serializer: new JsonSaveSerializer(JsonSaveFormatting.Compact)));
```

Save metadata uses the active serializer too. JSON saves write `metadata.json`.

Use `CompressedSaveSerializer` when you want smaller files without adding a NuGet dependency:

```csharp
var compressedManager = new SaveManager<string>(
    SaveSystemOptions.Create(
        saveRootPath: "Saves",
        serializer: new CompressedSaveSerializer(
            new JsonSaveSerializer(JsonSaveFormatting.Compact))));
```

Compressed JSON files use composed extensions such as `player.json.gz` and `metadata.json.gz`.

Payload transforms wrap any serializer with reversible byte encoding. Use this extension point for custom obfuscation or encryption.

```csharp
var transformedSerializer = new TransformedSaveSerializer(
    new JsonSaveSerializer(JsonSaveFormatting.Compact),
    new XorToyTransform());
```

```csharp
public sealed class XorToyTransform : ISavePayloadTransform
{
    public string FileExtensionSuffix => ".xor";

    public byte[] Encode(byte[] data) => Apply(data);
    public byte[] Decode(byte[] data) => Apply(data);

    private static byte[] Apply(byte[] data)
    {
        var copy = new byte[data.Length];
        for (var i = 0; i < data.Length; i++)
            copy[i] = (byte)(data[i] ^ 0x5A);
        return copy;
    }
}
```

The test suite includes `SerializerOutputExampleTests`, which writes pretty JSON, compact JSON, and compressed compact JSON example saves to `tests/obj/SerializerOutputExamples` for inspection.

After registering providers, call `ValidateRegistrations()` before disk save/load operations. Registration is intentionally lightweight; validation captures provider state, checks serializer write compatibility, validates migration policy, verifies file-name behavior, and rejects provider file-name collisions at the setup point you choose. Validation is an early compatibility check, not a full future-load proof: issues that only appear while deserializing real saved data can still surface during load.

`TryLoad...` statuses are the stable result contract for load failures. Exception messages are diagnostics for humans and should not be parsed by callers.

Use `ListSaveSlots()` to populate save/load menus or tooling with the saves currently present under the configured save root.

```csharp
IReadOnlyList<string> slots = manager.ListSaveSlots();
```

The returned values are resolved relative save paths, not `TIdentity` values, because custom identity resolvers may not be reversible. The list is sorted with ordinal string ordering, uses `/` separators, and ignores backup folders, temp folders, to-delete folders, and directories that do not contain save metadata.

Use `DeleteSave(...)` and `DeleteBackupSlot(...)` for save-menu cleanup or debug tooling.

```csharp
bool saveDeleted = manager.DeleteSave("slot-1");
bool backupDeleted = manager.DeleteBackupSlot("slot-1", slotNumber: 1);
```

`DeleteSave(...)` removes the main save and any temp or to-delete artifacts for the same resolved save path. It does not remove backups. `DeleteBackupSlot(...)` removes only the numbered backup folder and can be used even when backup creation is currently disabled.

Use `SaveExists(...)` and `BackupSlotExists(...)` for lightweight UI checks such as enabling load buttons or showing overwrite prompts.

```csharp
bool canLoad = manager.SaveExists("slot-1");
bool canRestoreBackup = manager.BackupSlotExists("slot-1", slotNumber: 1);
```

Existence checks inspect the raw disk layout without loading provider data, recovering temp folders, or requiring registration validation. A save or backup exists only when its folder contains save metadata.

Use `ReadSaveMetadata(...)` and `ReadBackupSlotMetadata(...)` when a menu or tool needs save-system-owned metadata.

```csharp
SaveMetadataInfo? metadata = manager.ReadSaveMetadata("slot-1");
if (metadata != null)
{
    DateTimeOffset lastWritten = metadata.LastWrittenAtUtc;
}
```

Metadata reads return `null` when no metadata file exists and throw when a metadata file is present but invalid. The current metadata contract exposes the stable save id used for recovery validation, plus created and last-written UTC timestamps. Application-owned display metadata such as character name, playtime, difficulty, or screenshot references should live in a provider for now.

Serializers that need format metadata can implement `ISaveSerializerMetadataHandler`. That metadata is stored inside the save-system metadata file as serializer-owned string key/value data, with both global and per-provider buckets. This is intended for serializer implementation details such as field maps or codec settings; it is not exposed through `SaveMetadataInfo` and should not be used for game/application display metadata.

Use `ForceSaveToDisk(...)` only when intentionally repairing or replacing a save whose existing metadata or serializer format cannot be trusted. Normal `SaveToDisk(...)` preserves readable core metadata and rotates backups when enabled. `ForceSaveToDisk(...)` writes a fresh main save with a new save id and timestamps, ignores unreadable existing metadata, does not rotate the replaced main folder into backups, and leaves existing backup folders untouched.

Use `TryLoadFromDisk(...)` or `TryLoadBackupSlotFromDisk(...)` when a UI or repair tool needs a structured outcome instead of exceptions.

```csharp
SaveLoadResult result = manager.TryLoadFromDisk("slot-1");
if (!result.Succeeded)
{
    switch (result.Status)
    {
        case SaveLoadStatus.NotFound:
            break;
        case SaveLoadStatus.CorruptData:
        case SaveLoadStatus.MigrationFailed:
            logger.Warn(result.Message);
            break;
    }
}
```

The try-load APIs use the same load path as `LoadFromDisk(...)` and `LoadBackupSlotFromDisk(...)`. Successful loads restore providers normally. Missing saves, disabled backups, registration validation failures, missing provider files, migration failures, recovery failures, corrupt data, and other load failures are reported through `SaveLoadResult.Status`; failed error cases keep the captured exception on `SaveLoadResult.Exception`. Provider payloads with a valid envelope but null `Data` are treated as corrupt data because provider state must be non-null.

For `TryLoadBackupSlotFromDisk(...)`, disabled backups are reported as `SaveLoadStatus.BackupSystemDisabled` before request or registration validation. This makes backup-disabled UI checks cheap and non-throwing even when the caller has not prepared provider registrations.

## Scopes And Provider Sets

The core package does not have a provider-group API. In the first reusable version, scope is modeled through save identities, save roots, and manager composition.

Use a scoped identity when the same registered providers are saved per profile, character, world, or slot.

```csharp
public readonly struct ProfileSlotIdentity
{
    public ProfileSlotIdentity(string profileId, string slotId)
    {
        ProfileId = profileId;
        SlotId = slotId;
    }

    public string ProfileId { get; }
    public string SlotId { get; }
}

var profileSaves = new SaveManager<ProfileSlotIdentity>(
    SaveSystemOptions.Create<ProfileSlotIdentity>(
        saveRootPath: "ProfileSaves",
        serializer: new JsonSaveSerializer(),
        savePathResolver: identity => Path.Combine(identity.ProfileId, identity.SlotId)));

profileSaves.SaveToDisk(new ProfileSlotIdentity("profile-a", "slot-1"));
```

Resolved save paths are safe relative paths under the manager's save root. They may contain path separators for hierarchy, but they cannot be absolute paths, contain `.` or `..` segments, use empty segments, or collide with save-system artifact folders.

Use separate managers when different provider sets have different lifecycles.

```csharp
var settingsSaves = new SaveManager<string>(
    SaveSystemOptions.Create(
        saveRootPath: "SettingsSaves",
        serializer: new JsonSaveSerializer()));
settingsSaves.RegisterProvider(settingsProvider);

var gameplaySaves = new SaveManager<ProfileSlotIdentity>(
    SaveSystemOptions.Create<ProfileSlotIdentity>(
        saveRootPath: "GameplaySaves",
        serializer: new JsonSaveSerializer(),
        savePathResolver: identity => Path.Combine(identity.ProfileId, identity.SlotId)));
gameplaySaves.RegisterProvider(playerProvider);
gameplaySaves.RegisterProvider(inventoryProvider);
```

This keeps provider ownership explicit: settings can be saved globally while gameplay providers are saved per profile slot. If a future application repeatedly needs partial-save domains inside one manager, that can be revisited as a separate provider-group feature.

## Providers

Each `ISaveProvider` owns one stable save key and one schema version.

Save keys are persistent identity. Changing a provider key changes the filename and breaks loading of existing provider data unless the application handles that compatibility.

`SaveKey` must remain stable after provider registration. `SchemaVersion` must remain stable after registration validation. The manager checks these values before disk save/load operations and throws a clear error if a provider changes its persistence contract after setup. If you intentionally change a provider schema during setup, call `ValidateRegistrations()` again before saving or loading.

Custom `FileNameResolver` values must also resolve every persisted provider to a unique file name. The default resolver uses `SaveKey`, so uniqueness follows from unique provider keys. A custom resolver that maps multiple providers to the same file is rejected during registration validation. The provider file base name `metadata` is reserved for save-system metadata, so providers must not resolve to `metadata.json`, `metadata.bin`, or the active serializer's equivalent metadata file.

Provider state must be non-null and compatible with the provider's `ISaveProvider<TState>` state type. Persisted providers must also be compatible with the serializer. If a provider has no data to save, return an explicit empty state object rather than `null`.
The built-in JSON serializer does not require a public parameterless constructor during registration; constructor-based DTOs are supported when Newtonsoft.Json can serialize and deserialize the real captured state.

```csharp
manager.RegisterProvider(playerProvider);
manager.ValidateRegistrations();
```

Use `TryRegisterProvider(...)` when setup code wants to add one provider and immediately reject it without throwing if registration validation fails.

```csharp
if (!manager.TryRegisterProvider(playerProvider, out var registrationError))
{
    logger.Warn(registrationError);
}
```

`TryRegisterProvider(...)` and `TryRegisterMemoryProvider(...)` tentatively register the provider, run the same global `ValidateRegistrations()` path, and remove the provider again if registration or validation fails. Memory-only providers are also captured during validation so null state is rejected before the first snapshot or disk operation. Successful try-registration leaves the manager validated for disk save/load operations.

Providers can optionally implement `ISaveLifecycle` to receive `OnBeforeSave()` before capture and `OnAfterLoad()` after a successful restore. Providers can also be registered without a schematic through `RegisterMemoryProvider(provider)` when they should participate in snapshots but not write their state to disk.

Providers can be removed with `UnregisterProvider(provider)` when you still own the registered instance, or with `UnregisterProvider("player")` when removal by key is intentional. The instance overload removes only the same object that was registered, using the original registration key even if the provider key has since drifted. Another provider instance with the same key will not remove it. If a provider is removed, call `ValidateRegistrations()` again before the next disk save or load.

When loading from disk, registered persisted providers are strict by default: if a provider is registered and its save file is missing from the save folder or backup folder, load throws and providers are not restored. Unknown extra files are ignored. For deliberate partial-load scenarios, configure `missingProviderFileBehavior: MissingProviderFileBehavior.Skip`; missing providers are skipped and keep their current runtime state.

Diagnostics are silent by default. To receive warning messages for recoverable issues such as disabled backup loads, backup normalization conflicts, or migration paths that cannot be applied, pass a warning sink through options:

```csharp
var options = SaveSystemOptions.Create(
    saveRootPath: "Saves",
    serializer: new JsonSaveSerializer(),
    warningSink: message => logger.Warn(message));
```

`RestoreSnapshot(...)` validates a snapshot before mutating providers. Validation rejects duplicate provider keys, unknown provider keys, schema mismatches, and persisted-provider state that does not match the registered schematic. Registered providers that are absent from the snapshot are skipped. If validation passes but a provider throws from `RestoreState(...)`, earlier providers may already have been restored.

## Backups

Backups are configured through `SaveSystemOptions<TIdentity>`.

```csharp
var options = SaveSystemOptions.CreateWithBackups(
    saveRootPath: "Saves",
    serializer: new JsonSaveSerializer(),
    backupSystemMaxBackupCount: 3);

var manager = new SaveManager<string>(options);
manager.RegisterProvider(playerProvider);
manager.ValidateRegistrations();
```

Backup slot `1` is the most recent previous save. Older backups rotate to higher slot numbers.

```csharp
manager.LoadBackupSlotFromDisk("slot-1", slotNumber: 1);
```

## Recovery

`LoadFromDisk(...)` automatically calls `RecoverSave(...)` before loading provider data. Recovery handles interrupted atomic save swaps involving the main save folder, the temp folder, and the to-delete folder for the same resolved save path.

Direct calls to `RecoverSave(...)` require the same successful `ValidateRegistrations()` setup as disk load operations.

Recovery validates candidate save folders without running provider migrations. It checks metadata, required provider files, schema-version extraction, current schema-version compatibility, and current-schema deserialization. A temp or `_toDelete` recovery candidate written with an older or newer provider schema is rejected rather than migrated during recovery; migrations still run during normal loads after a current-schema save folder has been recovered.

Recovery candidate validation is stricter than deliberate partial-load behavior. Even when `missingProviderFileBehavior: MissingProviderFileBehavior.Skip` is configured for normal loads, recovery will not promote a temp or `_toDelete` candidate that is missing a registered persisted provider file.

If both a temp folder and a previous `_toDelete` folder exist while the main save is missing, the valid temp save is preferred. If the temp save is invalid but the previous save is valid, recovery falls back to the previous save and emits a warning through the configured warning sink. If neither candidate is valid, recovery fails and preserves the recovery artifacts for inspection or manual repair.

## Migration

Providers that need to load older schema versions implement `ISaveMigratable`.

Each `SaveMigrationStep` migrates from version `x` to version `x + 1`. To migrate from version 1 to version 3, provide a step from 1 to 2 and another from 2 to 3.

```csharp
public sealed class PlayerSaveProvider : ISaveProvider<PlayerStateV2>, ISaveMigratable
{
    public string SaveKey => "player";
    public int SchemaVersion => 2;
    public int LoadPriority => 0;

    public PlayerStateV2 CaptureState() => Current;
    public void RestoreState(PlayerStateV2 state) => Current = state;

    public PlayerStateV2 Current { get; set; } = new PlayerStateV2();

    public ISaveMigrationSource CreateMigrationSource()
    {
        return new PlayerMigrationSource();
    }
}

public sealed class PlayerMigrationSource : ISaveMigrationSource
{
    public IReadOnlyList<SaveMigrationStep> Migrations { get; } =
        new[]
        {
            SaveMigrationStep.AddIntDefault(1, "Level", 1)
        };
}
```

When one schema-version step needs several simple edits, compose them into one step:

```csharp
public IReadOnlyList<SaveMigrationStep> Migrations { get; } =
    new[]
    {
        SaveMigrationStep.From(
            1,
            SaveMigrationStep.Rename("XP", "Experience"),
            SaveMigrationStep.AddIntDefault("Level", 1),
            SaveMigrationStep.Remove("LegacyName"))
    };
```

For advanced changes, use the full data-node action:

```csharp
new SaveMigrationStep(2, (data, factory) =>
{
    var inventory = data.Get("Inventory");
    inventory.Add(factory.CreateString("starter-sword"));
});
```

Downgrades are not supported. A save written with a newer schema version than the current provider expects fails to load.

## Extending The System

Most application code should use `SaveManager<TIdentity>`, `ISaveProvider<TState>`, `JsonSaveSerializer`, and plain state DTOs. The interfaces below are extension contracts for projects that need custom persistence formats, migration behavior, or serializer-backed data nodes.

### Provider Contracts

Implement `ISaveProvider<TState>` for each subsystem that owns saveable state.

| Member | Contract |
|---|---|
| `SaveKey` | Stable provider identity. It must be unique within a manager and must not change after provider registration. |
| `SchemaVersion` | Stable integer version for the provider state shape. Increase it when older payloads need migration. It must not change after registration validation. |
| `LoadPriority` | Lower values restore first. Use it when one provider must exist before another restores. |
| `CaptureState()` | Return a non-null, serializer-compatible state object of type `TState`. The manager calls lifecycle `OnBeforeSave()` before capture. |
| `RestoreState(TState)` | Accept the object shape produced by the registered schematic. |

Use `RegisterProvider(provider)` for disk persistence. The provider's `ISaveProvider<TState>` implementation supplies the state type.

Use `RegisterMemoryProvider(provider)` only for memory-only providers that should participate in snapshots but not be written to disk.

Implement `ISaveLifecycle` when a provider needs deterministic setup around save/load:

- `OnBeforeSave()` runs before each provider capture, including manual `CaptureSnapshot()`.
- `OnAfterLoad()` runs after all snapshot entries have been restored.
- `OnAfterLoad()` is not called when `LoadFromDisk(...)` returns false for a missing save.

### Migration Contracts

Implement `ISaveMigratable` only when the provider needs to load older schema versions.

Migration rules:

- one `SaveMigrationStep` migrates from `FromVersion` to `FromVersion + 1`;
- every version gap must have exactly one step;
- duplicate `FromVersion` steps and null migration entries are rejected during registration validation;
- migration steps mutate the provider payload's `Data` node, not the full envelope;
- simple helper methods such as `AddIntDefault`, `Rename`, `Move`, `Remove`, and `SetString` cover common top-level field edits;
- use `SaveMigrationStep.From(...)` to compose several helper actions into one schema-version step;
- after successful migration, the manager updates the envelope schema version before deserializing;
- downgrades are rejected.

Duplicate migration steps are rejected during registration validation. Missing migration gaps are detected during load and are reported by `TryLoad...` as `SaveLoadStatus.MigrationFailed`.

Migration steps should be deterministic. Avoid reading live game state, random values, current time, engine services, or other providers while mutating old payloads. A migration should be able to transform the same old payload into the same new payload every time.

### Serializer Contracts

Implement `ISaveSerializer` only when the built-in JSON serializer does not fit the application's persistence format.

A custom serializer must provide these pieces as one coherent format:

- a file extension, including the leading dot;
- schematic creation for provider state types;
- byte-based serialization and deserialization through those schematics;
- schema-version extraction without fully restoring provider state.

Schematic creation should be lightweight where possible. Provider state write compatibility is validated through real provider state during `ValidateRegistrations()`. Read compatibility is validated when real save data is deserialized during load, so custom serializers should still fail clearly for deserialize-only problems.

If the serializer needs metadata stored with a save, implement `ISaveSerializerMetadataHandler` and return it from `ISaveSerializer.Metadata`. The manager calls `WriteMetadata(...)` before writing the metadata file and `ValidateMetadata(...)` when temp-save and recovery-candidate metadata is validated. Missing serializer metadata is treated as empty metadata for compatibility with older saves.

Use `TransformedSaveSerializer` with `ISavePayloadTransform` when an existing serializer format should be encoded after serialization and decoded before deserialization. The decorator composes file extensions, so wrapping JSON with a transform whose suffix is `.enc` writes provider and metadata files such as `player.json.enc` and `metadata.json.enc`. Migration is routed through the decorator by decoding before `DeserializeToNode(...)` and encoding after `SerializeFromNode(...)`. Serializer metadata is delegated from the inner serializer.

`CompressedSaveSerializer` is the intended public compression API. Its internal compression transform should remain an implementation detail unless a concrete use case appears for exposing GZip as a standalone payload transform.

MessagePack support is intended to be provided by the optional `Workes.SaveSystem.MessagePack` companion package for dependency reasons. The core package does not reference MessagePack directly. Once installed, the companion serializer should be usable anywhere an `ISaveSerializer` is accepted:

```xml
<ItemGroup>
  <PackageReference Include="Workes.SaveSystem.MessagePack" Version="0.1.0" />
</ItemGroup>
```

```csharp
var manager = new SaveManager<string>(
    SaveSystemOptions.Create(
        saveRootPath: "Saves",
        serializer: new MessagePackSaveSerializer()));
```

If providers using the serializer implement `ISaveMigratable`, the serializer must return an `ISaveMigrationCapableSerializer` from `ISaveSerializer.Migration`. That adapter must parse serialized payloads into editable `ISaveDataNode` trees, serialize edited node trees back to the payload format, and expose a matching `NodeFactory` that creates new object, array, and primitive nodes for migration steps.

The migration-capable serializer, its `NodeFactory`, and its data-node trees are coupled. Do not mix data nodes from different serializer or factory instances.

### Data Node Contracts

`ISaveDataNode` is the format-neutral edit surface used by migrations. Normal application code should not need to implement or manipulate data nodes outside migration steps.

Concrete data-node implementations and built-in data-node factories are implementation details. Migration code should use the `ISaveMigrationCapableSerializer.NodeFactory` supplied by the serializer that is reading or writing the migrated payload.

Data-node implementations should:

- report node type consistently;
- preserve object keys and array order;
- fail clearly when callers use object operations on arrays or primitive operations on objects;
- keep mutations local to the represented serialized tree;
- support the primitive and null node types exposed by `ISaveDataNodeFactory`;
- reject attempts to combine nodes created by another serializer or factory instance.

For the built-in JSON serializer, JSON payloads are converted into package-owned migration nodes before migration steps run, then converted back to JSON after migration. The Newtonsoft JSON model remains an implementation detail of JSON parsing and writing, not the migration edit surface.

## Unity And Godot

The package targets `netstandard2.1`, which is suitable for modern Unity and Godot C# projects that can consume compatible .NET libraries.

Unity projects should make sure Newtonsoft.Json is available through the project's package setup or assembly references. Use Unity's persistent data path when constructing the manager:

```csharp
var manager = SaveManager<string>.CreateDefault(
    new JsonSaveSerializer(),
    saveRootPath: UnityEngine.Application.persistentDataPath);
```

Godot C# projects should restore the package dependency through normal .NET/NuGet restore. Use a Godot-owned user data path for saves:

```csharp
var manager = SaveManager<string>.CreateDefault(
    new JsonSaveSerializer(),
    saveRootPath: ProjectSettings.GlobalizePath("user://saves"));
```

The core package does not reference Unity or Godot assemblies. Engine-specific setup should stay in the application or in a future adapter package.

## Compatibility Notes

There are two different versioning concerns:

- package versions describe the `Workes.SaveSystem` library API and implementation;
- provider schema versions describe an application's persisted provider payload shape.

Do not use the package version as a replacement for provider `SchemaVersion`. A package update may contain no save-format changes, and a provider DTO change may require a schema bump even when the package version stays the same.

Existing saves depend on stable provider keys, schema versions, file-name resolver behavior, serializer behavior, and state DTO shape.

Before changing any of these, decide whether the change is intentionally breaking or whether a migration path is needed:

- provider key changes affect provider filenames;
- schema version changes require migration steps for older saves;
- file-name resolver changes affect where provider files are found;
- serializer changes affect payload shape and migration node compatibility;
- state DTO changes may need schema version bumps.

### Provider Keys And File Names

`SaveKey` is persistent identity for a provider. With the default file-name resolver, a provider with key `player` is stored as `player.json`.

Changing `SaveKey` is effectively a persisted file rename. If an application must rename a provider, it should either keep reading the old key during a transition or perform an external save-folder migration before loading with the new key.

The default `FileNameResolver` uses only `SaveKey`. This is intentional. Including `SchemaVersion` in file names is not recommended because a v2 provider would look for a different filename than the v1 save wrote, preventing the migration system from seeing the old payload.

### Schema Versions

Provider schema versions should increase only when the provider payload shape changes in a way that existing saves cannot load directly.

Good reasons to increase `SchemaVersion`:

- a required state property was added;
- a property changed meaning or type;
- old payload data needs transformation before deserialization;
- multiple old fields were collapsed into one new field.

Usually safe without a schema bump:

- adding optional nullable data that deserializes with a valid default;
- changing runtime-only provider logic without changing captured state;
- refactoring code while the serialized payload shape stays the same.

When in doubt, write a load test with an old payload. If the current provider cannot load the old payload without special handling, bump the provider schema and add migration steps.

### Package Versions

Package versioning should communicate API and behavior compatibility for consumers of `Workes.SaveSystem`.

Recommended package-version expectations:

- patch version: bug fixes and documentation that do not intentionally change public behavior;
- minor version: additive APIs or compatible behavior improvements;
- major version: breaking public API changes or persistence behavior changes that may require application action.

Package version changes do not migrate saves by themselves. Save compatibility stays owned by provider `SchemaVersion`, migration steps, stable keys, and stable serializer behavior.

### Serializer Swaps

Changing serializers is a save-format change. Even if two serializers both write JSON, their envelope shape, type handling, formatting, and migration node model may differ.

Before swapping serializers for existing saves, decide on one of these strategies:

- keep the old serializer for existing saves and use the new serializer only for new save roots;
- write an external conversion tool that reads old files and writes the new format;
- create a serializer that can read the old format during a transition;
- intentionally break old saves and document the break.

The current built-in migration system migrates provider payload schema inside one serializer format. It is not a general cross-serializer conversion tool.

### Long-Lived Save Checklist

For saves expected to survive package and application updates:

- keep provider keys stable and human-readable;
- use the default save path and file-name resolvers unless there is a strong reason not to;
- keep state DTOs simple and serializer-friendly;
- bump provider schema versions deliberately;
- keep migration steps deterministic;
- add tests that load representative old payloads;
- avoid using current time, random values, engine state, or other providers during migrations;
- document intentionally breaking save-format changes in application release notes.

## Suggested Next Steps

1. Choose the package license and final release version before publishing.
2. Consider a `System.Text.Json` adapter only if a clear consumer need appears and its compatibility limits are acceptable.
3. Add engine-specific adapter packages only if repeated Unity or Godot setup code becomes noisy enough to justify them.
