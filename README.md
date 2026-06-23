# Workes.SaveSystem

`Workes.SaveSystem` is an engine-neutral .NET save system for registering state providers, capturing save snapshots, writing them to disk, loading them back, rotating backups, and migrating saved data between schema versions.

The package is not tied to Unity, Godot, or any game engine. Engine projects choose the save root path and pass it to the save manager.

## Installation

Install the preview package from NuGet:

```xml
<ItemGroup>
  <PackageReference Include="Workes.SaveSystem" Version="1.0.0-preview.1" />
</ItemGroup>
```

When working from source, reference the project directly:

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

GZip compression is available through the .NET platform libraries and does not require another NuGet dependency. MessagePack support is intended for the optional companion package, `Workes.SaveSystem.MessagePack`, because it brings its own serializer dependency. The intended package shape is:

```text
Workes.SaveSystem
Workes.SaveSystem.MessagePack
```

The core `Workes.SaveSystem` preview does not reference MessagePack and does not ship a MessagePack serializer. It provides contextual serializer APIs so metadata-backed companion serializers can use field maps during payload reads, writes, validation, and migration. Until the companion package is published, use JSON or compressed JSON:

```csharp
var serializer = new CompressedSaveSerializer(
    new JsonSaveSerializer(JsonSaveFormatting.Compact));
```

MessagePack is intended for compact production saves once the companion package is ready. JSON remains the built-in readable serializer and the recommended default for this preview.

## Quick Start

Create a save manager, register providers, then save and load a slot.

```csharp
using Workes.SaveSystem;

var serializer = new JsonSaveSerializer();
var manager = SaveManager<string>.CreateDefault( // SaveManager is generic over identity type
    serializer,
    saveRootPath: "Saves");

var playerProvider = new PlayerSaveProvider();
manager.RegisterProvider(playerProvider);
manager.ValidateRegistrations(); // Necessary for using the system after provider registration or unregistration

manager.SaveToDisk("slot-1"); // "slot-1" is simply an identifier. A string is used because var manager is SaveManager<string>

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

Use `SaveSystemOptions.Create(...)` when the manager needs custom path, file, warning, or missing-file behavior.

```csharp
var options = SaveSystemOptions.Create<ProfileSlotIdentity>(
    saveRootPath: "Saves",
    serializer: new JsonSaveSerializer(JsonSaveFormatting.Compact),
    savePathResolver: identity => Path.Combine(identity.ProfileId, identity.SlotId),
    fileNameResolver: context => context.SaveKey,
    missingProviderFileBehavior: MissingProviderFileBehavior.Throw,
    warningSink: message => logger.Warn(message));

var manager = new SaveManager<ProfileSlotIdentity>(options);
```

For simple string slots, omit `savePathResolver`; string identities resolve directly to relative save paths. Keep custom `fileNameResolver` output stable over time, and do not include `SchemaVersion` in file names unless intentionally breaking old saves. Provider schema versions belong inside the provider payload so the migration system can find older files.

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

Use `DeleteSave(...)`, `DeleteBackupSlot(...)`, and `DeleteAllBackupSlots(...)` for save-menu cleanup or debug tooling.

```csharp
bool saveDeleted = manager.DeleteSave("slot-1");
bool backupDeleted = manager.DeleteBackupSlot("slot-1", slotNumber: 1);
int backupsDeleted = manager.DeleteAllBackupSlots("slot-1");
```

`DeleteSave(...)` removes the main save and any temp or to-delete artifacts for the same resolved save path. It does not remove backups. `DeleteBackupSlot(...)` removes only the requested numbered backup folder, while `DeleteAllBackupSlots(...)` removes all numbered backup folders for the save identity and returns the number deleted. Backup deletion can be used even when backup creation is currently disabled.

Use `SaveExists(...)` and `BackupSlotExists(...)` for lightweight UI checks such as enabling load buttons or showing overwrite prompts.

```csharp
bool canLoad = manager.SaveExists("slot-1");
bool canRestoreBackup = manager.BackupSlotExists("slot-1", slotNumber: 1);
```

Existence checks inspect the raw disk layout without loading provider data, recovering temp folders, or requiring registration validation. A save or backup exists only when its folder contains save metadata.

Use `ValidateSave(...)` or `ValidateBackupSlot(...)` when a save menu needs to check loadability without restoring providers or mutating disk. Validation requires validated registrations, reads save-system and serializer metadata, checks provider files, validates schema extraction and migration paths, and deserializes provider data in memory. It does not run recovery, call lifecycle hooks, write migrated data, or restore provider state.

```csharp
SaveValidationResult validation = manager.ValidateSave("slot-1");
if (validation.IsValid)
{
    ShowLastWritten(validation.Metadata!.LastWrittenAtUtc);
}
```

## Save Metadata

Use `ReadSaveMetadata(...)` and `ReadBackupSlotMetadata(...)` when a menu or tool needs save-system-owned metadata.

```csharp
SaveMetadataInfo? metadata = manager.ReadSaveMetadata("slot-1");
if (metadata != null)
{
    DateTimeOffset lastWritten = metadata.LastWrittenAtUtc;
}
```

Metadata reads return `null` when no metadata file exists and throw when a metadata file is present but invalid. `SaveMetadataInfo` exposes:

| Property | Meaning |
|---|---|
| `SaveId` | Stable id for this save folder. It is used internally to validate recovery candidates and should not be treated as application display data. |
| `CreatedAtUtc` | UTC timestamp for when this save identity was first written, preserved by normal saves. |
| `LastWrittenAtUtc` | UTC timestamp for the most recent successful write. |

Serializers that need format metadata can implement `ISaveSerializerMetadataHandler`. That metadata is stored inside the save-system metadata file as serializer-owned string key/value data, with both global and per-provider buckets. This is intended for serializer implementation details such as field maps or codec settings; it is not exposed through `SaveMetadataInfo` and should not be used for game/application display metadata.

Serializers whose provider payload format depends on that metadata can also implement contextual extension interfaces such as `IContextualSaveSerializer` and, for migration support, `IContextualSaveMigrationCapableSerializer`. The manager passes a `SaveSerializerContext` containing the provider save key, schema version, state type, schematic, and saved serializer metadata when serializing, extracting schema versions, deserializing, validating, and migrating provider payloads. When these interfaces are present, `SaveManager` uses the contextual path for provider payloads. Direct calls to the base `ISaveSerializer` methods may not produce the same provider payload shape as manager-managed saves. Plain JSON-style serializers can ignore these optional interfaces.

Application-owned display metadata such as character name, playtime, difficulty, or screenshot references should live in a normal provider for this preview. A dedicated application metadata provider API is planned separately so application metadata can have its own ownership and migration story instead of being mixed with required save-system metadata or serializer metadata.

Advanced custom serializers must support the public `SaveMetadata` payload type because save-system metadata is serialized through the active serializer. `SaveMetadata` is a property-based serializer contract with stable names for `SaveId`, `CreatedAtUtc`, `LastWrittenAtUtc`, and `SerializerMetadata`; manager-owned creation and timestamp update helpers are intentionally not public. Application code that only wants to read menu metadata should use `SaveMetadataInfo` from `ReadSaveMetadata(...)`, `ReadBackupSlotMetadata(...)`, or successful `ValidateSave(...)` results.

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

`SchemaVersion` is the version of that provider's persisted state shape. It is stored inside the provider payload, not in the file name, so the manager can read an old payload, see its saved version, run migration steps, and only then deserialize it into the current state type.

`SaveKey` must remain stable after provider registration. `SchemaVersion` must remain stable after registration validation. The manager checks these values before disk save/load operations and throws a clear error if a provider changes its persistence contract after setup. If you intentionally change a provider schema during setup, call `ValidateRegistrations()` again before saving or loading.

Bump `SchemaVersion` when old payloads need help to become the current state shape. Good examples include adding required data, renaming a persisted property, changing a property's type or meaning, or splitting/collapsing fields. You usually do not need a bump for runtime-only code changes, adding optional nullable data that deserializes safely, or refactoring code while the serialized state shape remains compatible.

Custom `FileNameResolver` values must also resolve every persisted provider to a unique file name. The default resolver uses `SaveKey`, so uniqueness follows from unique provider keys. A custom resolver that maps multiple providers to the same file is rejected during registration validation. The provider file base name `metadata` is reserved for save-system metadata, so providers must not resolve to `metadata.json`, `metadata.json.gz`, or the active serializer's equivalent metadata file.

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

## Default Save-State Model

The default save-state model is the portable contract that built-in serializers and serializer companion packages are expected to support. It is also the shape that works best with migration, validation, and serializer swaps.

Supported by default:

- provider state should be a concrete, non-polymorphic type;
- public properties are the primary serialization surface;
- nested concrete POCO/object state is supported;
- common scalar values are expected to work, such as strings, bools, integers, floating-point numbers, decimals, enums, and serializer-supported values such as `DateTime`, `DateTimeOffset`, `Guid`, and byte arrays;
- collections are expected to work when they are ordinary arrays or lists of supported element types;
- dictionaries are expected to work when they use string keys and supported value types.

Example of supported nested state:

```csharp
public sealed class PlayerState
{
    public string Name { get; set; } = "";
    public WeaponState Weapon { get; set; } = new();
}

public sealed class WeaponState
{
    public int Damage { get; set; }
    public bool IsMagic { get; set; }
}
```

Out of scope for the default model:

- runtime polymorphism;
- interface-typed state members, such as `IQuestState CurrentQuest`;
- abstract or base-type members where the runtime value may be a derived type;
- `object`-typed members intended to hold arbitrary runtime values;
- type-name metadata or automatic concrete-type restoration;
- private fields or private properties as the default serialization surface;
- arbitrary object graphs with cycles;
- serializer-specific custom converters or resolvers as part of the default contract.

These are general serialization and migration constraints, not limitations of one specific serializer. A custom serializer, converter, or resolver may support broader shapes, but those shapes are outside the default save-system contract and should be documented and tested by the application or serializer package that enables them. Companion serializers, such as MessagePack, should aim to match this default model unless their README documents a specific difference.

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

`CreateWithBackups(...)` accepts the same resolver, warning, and missing-provider-file options as `Create(...)`, plus `backupSystemMaxBackupCount`.

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

Providers that need to load older schema versions implement `ISaveMigratable`. Migration is provider-local: each provider owns its own `SchemaVersion`, migration source, and migration steps.

Provider files may be written as a serializer-owned envelope containing `SchemaVersion` and provider data. Migration reads the saved schema version from the serialized payload, converts the provider data root into editable save data nodes, mutates that root node, and then asks the serializer to write the current schema version back into its payload format. Migration steps never edit serializer envelopes directly.

```json
{
  "SchemaVersion": 1,
  "Data": {
    "Name": "Rook"
  }
}
```

Migration steps edit the serialized provider data root before the serializer deserializes it into the current state type. For an object-root provider, that root is an object node. For a list-root provider, it is an array node. For a string-key dictionary root, it is an object/map node whose keys are data keys. For a primitive root, it is a primitive node. That means application code should normally keep only the current DTO shape in runtime code. Old shapes can live in tests or documentation fixtures, but they do not need to remain as production state classes just so migration can work.

For example, imagine version 1 of `PlayerState` only had `Name`. Version 2 adds required `Level` data. The current code can simply use the current shape:

```csharp
public sealed class PlayerState
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
}
```

The provider reports the current schema version and supplies a migration that adds `Level` to older serialized payloads:

```csharp
public sealed class PlayerSaveProvider : ISaveProvider<PlayerState>, ISaveMigratable
{
    public string SaveKey => "player";
    public int SchemaVersion => 2;
    public int LoadPriority => 0;

    public PlayerState CaptureState() => Current;
    public void RestoreState(PlayerState state) => Current = state;

    public PlayerState Current { get; set; } = new PlayerState();

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

`SaveMigrationStep.AddIntDefault(1, "Level", 1)` means: when loading a version 1 payload, add a top-level `Level` value before deserializing it as the current `PlayerState`.

### Migration Flow

The load path applies migrations only when the saved provider schema version differs from the registered provider schema version.

1. The manager extracts `SchemaVersion` from the provider file.
2. If the provider implements `ISaveMigratable`, the manager asks it for an `ISaveMigrationSource`.
3. The migration engine requires one step for every version gap, such as `1 -> 2` and `2 -> 3`.
4. The active serializer's `Migration` adapter parses bytes into an editable `ISaveDataNode` provider root.
5. Each migration step mutates that provider root node, not the full serializer envelope.
6. The serializer converts the edited root node back to bytes using the current provider schema version.
7. The serializer deserializes those bytes into the current provider state type.

Downgrades are not supported. A save written with a newer schema version than the current provider expects fails to load.

### Migration API

When one schema-version step needs several simple edits, compose them into one step. This is still one migration from version 1 to version 2:

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

The migration API is intentionally split into provider-facing contracts, helper steps, and direct data-node editing.

| API | Purpose |
|---|---|
| `ISaveMigratable.CreateMigrationSource()` | Implement on a provider that can load older schema versions. |
| `ISaveMigrationSource.Migrations` | Returns the ordered or unordered list of available `SaveMigrationStep` entries. |
| `SaveMigrationStep.FromVersion` | The schema version this step migrates from. Each step always migrates to `FromVersion + 1`. |
| `SaveMigrationStep.Migrate` | The action that mutates the provider data root node. |
| `new SaveMigrationStep(fromVersion, action)` | Creates a custom migration step with full data-node access. |
| `SaveMigrationStep.From(fromVersion, actions...)` | Combines several migration actions into one version step. |

Helper methods come in two forms. Methods with a `fromVersion` return a complete `SaveMigrationStep`. Methods without `fromVersion` return a reusable action for `SaveMigrationStep.From(...)`.

| Helper family | Complete step example | Action example | Behavior |
|---|---|---|---|
| `AddDefault` | `AddIntDefault(1, "Level", 1)` | `AddIntDefault("Level", 1)` | Adds a field only when the field is missing. |
| `Set` | `SetString(1, "Name", "Rook")` | `SetString("Name", "Rook")` | Writes or replaces a field. |
| `Remove` | `Remove(1, "LegacyName")` | `Remove("LegacyName")` | Removes a field if it exists. |
| `Rename` | `Rename(1, "XP", "Experience")` | `Rename("XP", "Experience")` | Moves a field to a new key and fails if the target exists unless `overwrite: true`. |
| `Move` | `Move(1, "OldName", "NewName")` | `Move("OldName", "NewName")` | Alias for `Rename(...)`, useful when the intent is moving data. |

Primitive helper variants are available for common values:

```csharp
SaveMigrationStep.AddIntDefault(1, "Level", 1);
SaveMigrationStep.AddLongDefault(1, "TotalGold", 9000000000L);
SaveMigrationStep.AddFloatDefault(1, "Speed", 4.5f);
SaveMigrationStep.AddDoubleDefault(1, "Precision", 4.56789d);
SaveMigrationStep.AddDecimalDefault(1, "Cost", 123.45m);
SaveMigrationStep.AddStringDefault(1, "Title", "Unknown");
SaveMigrationStep.AddBoolDefault(1, "Unlocked", false);
SaveMigrationStep.AddBytesDefault(1, "Thumbnail", bytes);
SaveMigrationStep.AddDateTimeDefault(1, "LastSeenAt", DateTime.UtcNow);
SaveMigrationStep.AddNullDefault(1, "DeletedAt");

SaveMigrationStep.SetInt(1, "Level", 1);
SaveMigrationStep.SetLong(1, "TotalGold", 9000000000L);
SaveMigrationStep.SetFloat(1, "Speed", 4.5f);
SaveMigrationStep.SetDouble(1, "Precision", 4.56789d);
SaveMigrationStep.SetDecimal(1, "Cost", 123.45m);
SaveMigrationStep.SetString(1, "Title", "Unknown");
SaveMigrationStep.SetBool(1, "Unlocked", false);
SaveMigrationStep.SetBytes(1, "Thumbnail", bytes);
SaveMigrationStep.SetDateTime(1, "LastSeenAt", DateTime.UtcNow);
SaveMigrationStep.SetNull(1, "DeletedAt");
```

Use helper steps for simple top-level object edits. Use direct data-node access when a migration needs nested objects, arrays, root collections, conditionals, value conversion, or multi-field logic.

```csharp
new SaveMigrationStep(2, (data, factory) =>
{
    var inventory = data.Get("Inventory");
    inventory.Add(factory.CreateString("starter-sword"));
});
```

The `data` parameter is an `ISaveDataNode` for the provider data root. The factory parameter creates new nodes that belong to the same serializer-owned node tree. Do not create nodes with another serializer or factory instance and insert them here.

```csharp
new SaveMigrationStep(2, (data, factory) =>
{
    if (!data.Has("Inventory"))
        data.Set("Inventory", factory.CreateArray());

    data.Get("Inventory").Add(factory.CreateString("starter-sword"));
});
```

Root collection and primitive migrations use the same node model:

```csharp
new SaveMigrationStep(1, (root, factory) =>
{
    for (var i = 0; i < root.Count; i++)
    {
        var item = root.GetAt(i);
        if (!item.Has("Count"))
            item.Set("Count", factory.CreateInt(1));
    }
});

new SaveMigrationStep(2, (root, factory) =>
{
    root.ReplaceWith(factory.CreateString("level-" + root.AsInt()));
});
```

### Save Data Nodes

`ISaveDataNode` is a package-owned, format-neutral edit tree for migration. It is not a normal provider state DTO, and it is not Newtonsoft.Json's `JToken` exposed through the public API. The built-in JSON serializer converts JSON into package-owned data nodes before migration, then converts the edited nodes back into JSON afterward.

Supported node types are `Object`, `Array`, `Int`, `Long`, `Float`, `Double`, `Decimal`, `String`, `Bool`, `Bytes`, `DateTime`, and `Null`.

| Node API | Applies to | Purpose |
|---|---|---|
| `NodeType` | all nodes | Gets the current `SaveDataNodeType`. |
| `IsObject()`, `IsArray()`, `IsNull()` | all nodes | Checks the current node shape. |
| `Count` | object/array nodes | Gets the child count. |
| `Keys` | object nodes | Enumerates object keys. |
| `Has(key)`, `Get(key)`, `Set(key, value)`, `Remove(key)` | object nodes | Reads and mutates object fields. |
| `GetAt(index)`, `SetAt(index, value)`, `InsertAt(index, value)`, `RemoveAt(index)`, `Add(value)` | array nodes | Reads and mutates array entries. |
| `AsInt()`, `AsLong()`, `AsFloat()`, `AsDouble()`, `AsDecimal()` | numeric nodes | Reads numeric values. |
| `AsString()`, `AsBool()`, `AsBytes()`, `AsDateTime()` | primitive nodes | Reads primitive values. |
| `SetInt(value)`, `SetLong(value)`, `SetFloat(value)`, `SetDouble(value)`, `SetDecimal(value)` | existing nodes | Replaces the current node with a numeric value. |
| `SetString(value)`, `SetBool(value)`, `SetBytes(value)`, `SetDateTime(value)`, `SetNull()` | existing nodes | Replaces the current node value. |
| `ReplaceWith(value)` | existing nodes | Replaces the current node with another node shape/value created by the same factory. |

Use `ISaveDataNodeFactory` to create new nodes:

```csharp
factory.CreateObject();
factory.CreateArray();
factory.CreateInt(1);
factory.CreateLong(9000000000L);
factory.CreateFloat(4.5f);
factory.CreateDouble(4.56789d);
factory.CreateDecimal(123.45m);
factory.CreateString("Rook");
factory.CreateBool(true);
factory.CreateBytes(bytes);
factory.CreateDateTime(DateTime.UtcNow);
factory.CreateNull();
```

For the built-in JSON serializer, `Long` writes a JSON integer and `Double` writes a JSON number. `Decimal` writes an invariant-culture string, `Bytes` writes a Base64 string, and `DateTime` writes an `"O"` round-trip string. When reading migration nodes from JSON, date-looking strings remain strings until migration code asks for `AsDateTime()`, and Base64 strings remain strings until migration code asks for `AsBytes()`.

Wrong-shape operations fail clearly. For example, calling `Get("Name")` on an array node or `Add(...)` on an integer node throws. Nodes also track factory ownership so migrations cannot accidentally mix nodes from different serializer instances.

Migration steps should be deterministic. Avoid reading live game state, random values, current time, engine services, or other providers while mutating old payloads. A migration should be able to transform the same old payload into the same new payload every time.

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

Implement `ISaveMigratable` only when the provider needs to load older schema versions. See the `Migration` section for the full provider-facing workflow and helper API.

Migration rules:

- one `SaveMigrationStep` migrates from `FromVersion` to `FromVersion + 1`;
- every version gap must have exactly one step;
- duplicate `FromVersion` steps and null migration entries are rejected during registration validation;
- migration steps mutate the provider data root node, not the full serializer envelope;
- use `SaveMigrationStep.From(...)` to compose several helper actions into one schema-version step;
- after successful migration, the serializer writes the current schema version before deserializing;
- downgrades are rejected.

Missing migration gaps are detected during load and are reported by `TryLoad...` as `SaveLoadStatus.MigrationFailed`.

### Serializer Contracts

Implement `ISaveSerializer` only when the built-in JSON serializer does not fit the application's persistence format.

Serializer methods are primarily extension points for the save-system pipeline. Application code should usually interact with `SaveManager` instead of calling serializers directly. Use manager APIs such as `SaveToDisk(...)`, `LoadFromDisk(...)`, `TryLoadFromDisk(...)`, and `ValidateSave(...)` for normal save/load flows. `ISaveSerializer.Serialize(...)`, `Deserialize(...)`, and `CreateSchematic(...)` are intended for `SaveManager`, serializer wrappers, and advanced integration code.

A custom serializer must provide these pieces as one coherent format:

- a file extension, including the leading dot;
- schematic creation for provider state types;
- byte-based serialization and deserialization through those schematics;
- schema-version extraction without fully restoring provider state.

Schematic creation should be lightweight where possible. Provider state write compatibility is validated through real provider state during `ValidateRegistrations()`. Read compatibility is validated when real save data is deserialized during load, so custom serializers should still fail clearly for deserialize-only problems.

If the serializer needs metadata stored with a save, implement `ISaveSerializerMetadataHandler` and return it from `ISaveSerializer.Metadata`. The manager calls `WriteMetadata(...)` before provider payloads and the metadata file are written, and calls `ValidateMetadata(...)` when temp-save, load, validation, and recovery-candidate metadata is validated. Missing serializer metadata is treated as empty metadata for compatibility with older saves.

If provider payload serialization depends on serializer-owned metadata or manager-owned context, implement contextual extension interfaces such as `IContextualSaveSerializer`. The manager will prefer contextual `Serialize(...)`, `Deserialize(...)`, and `ExtractSchemaVersion(...)` methods for provider files and fall back to `ISaveSerializer` methods for existing serializers. Use this when serializers need save metadata, provider keys, schema versions, or other manager-owned context, such as compact metadata-backed formats with MessagePack field maps. Direct calls to the base `ISaveSerializer` methods are best treated as low-level integration or diagnostic operations because they may not produce the same provider payload shape as manager-managed saves. JSON serializers and other self-describing formats usually do not need contextual serialization.

Custom serializers must also be able to create a schematic for the public `SaveMetadata` type. Existing metadata files that deserialize to `null` or to another type are treated as corrupt; use `ForceSaveToDisk(...)` when intentionally replacing a corrupt or incompatible save.

Use `TransformedSaveSerializer` with `ISavePayloadTransform` when an existing serializer format should be encoded after serialization and decoded before deserialization. The decorator composes file extensions, so wrapping JSON with a transform whose suffix is `.enc` writes provider and metadata files such as `player.json.enc` and `metadata.json.enc`. Migration is routed through the decorator by decoding before `DeserializeToNode(...)` and encoding after `SerializeFromNode(...)`; those migration methods pass provider root nodes, not serializer envelopes. Contextual serializer and migration calls are forwarded to the inner serializer when supported. Serializer metadata is delegated from the inner serializer.

`CompressedSaveSerializer` is the intended public compression API. Its internal compression transform should remain an implementation detail unless a concrete use case appears for exposing GZip as a standalone payload transform.

MessagePack support belongs in the optional `Workes.SaveSystem.MessagePack` companion package for dependency reasons. The core package does not reference MessagePack directly and does not implement field maps itself; it provides the contextual serializer plumbing needed for the companion package to store and use those maps in `SaveMetadata.SerializerMetadata`.

The companion package should treat the provider root as one of several root shapes: object, array/list, string-key map, scalar, or null while editing migration nodes. Field-map metadata applies to object-root DTOs only. Root arrays/lists, string-key dictionaries, primitives, and nil/null are provider root values, not fake object field maps. A companion serializer may support non-string dictionary keys for normal save/load if it documents that generic migration exposes only string-key maps through `ISaveDataNode`.

If providers using the serializer implement `ISaveMigratable`, the serializer must return an `ISaveMigrationCapableSerializer` from `ISaveSerializer.Migration`. That adapter must parse serialized payloads into editable provider-root `ISaveDataNode` trees, serialize edited root node trees back to the payload format, and expose a matching `NodeFactory` that creates new object, array, and primitive nodes for migration steps. Metadata-backed migration adapters can additionally implement `IContextualSaveMigrationCapableSerializer` to receive the same `SaveSerializerContext` used by contextual provider serialization and to write the current schema version after migration.

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
- expose null values through `IsNull()` and allow existing nodes to be replaced with null through `SetNull()`;
- allow existing nodes, including root nodes, to be replaced with same-factory nodes through `ReplaceWith(...)`;
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
