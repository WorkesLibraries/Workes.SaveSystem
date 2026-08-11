# Extending The Save System

`Workes.SaveSystem` is built around small extension contracts. Most applications only implement providers. Implement serializer, transform, migration-node, or metadata contracts when you need a new file format or format-level behavior.

Choose the smallest extension point that matches the problem:

| Goal | Extension point |
|---|---|
| Save and restore application state | `ISaveProvider<TState>` |
| Run code before saving or after loading | `ISaveLifecycle` |
| Give deterministic defaults to providers added after old saves exist | `ISaveDefaultStateProvider<TState>` |
| Migrate old provider payloads | `ISaveMigratable` and `ISaveMigrationSource` |
| Store application-owned save-menu metadata | `ISaveMetadataProvider<TMetadata>` |
| Migrate application metadata | `ISaveMetadataMigratable` |
| Wrap payload bytes for compression, obfuscation, or encryption | `ISavePayloadTransform` with `TransformedSaveSerializer` |
| Create a new provider payload format | `ISaveSerializer` and `ISaveSchematic` |
| Let a serializer use save/provider context | `IContextualSaveSerializer` |
| Let a serializer migrate payload nodes | `ISaveMigrationCapableSerializer` |
| Let contextual serializer migration use save/provider context | `IContextualSaveMigrationCapableSerializer` |
| Store serializer-owned metadata in the save metadata file | `ISaveSerializerMetadataHandler` |
| Support typed application metadata inline | `ISaveApplicationMetadataSerializer` |

## Provider Extensions

Providers are the normal application extension point. A provider owns one saved state type and exposes three persistence values:

- `SaveKey`: persistent provider identity.
- `SchemaVersion`: current serialized state shape.
- `LoadPriority`: save/load ordering; lower values run first.

```csharp
public sealed class PlayerSaveProvider : ISaveProvider<PlayerState>
{
    public string SaveKey => "player";
    public int SchemaVersion => 1;
    public int LoadPriority => 0;

    public PlayerState Current { get; set; } = new PlayerState();

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

Keep provider state as a data transfer object. It should contain the data needed to restore the application, not engine objects, service instances, file handles, or UI state.

## Lifecycle Hooks

Implement `ISaveLifecycle` when a provider needs a callback around save/load operations:

```csharp
public sealed class WorldSaveProvider :
    ISaveProvider<WorldState>,
    ISaveLifecycle
{
    public string SaveKey => "world";
    public int SchemaVersion => 1;
    public int LoadPriority => -10;

    public WorldState Current { get; set; } = new WorldState();

    public void OnBeforeSave()
    {
        Current.LastCapturedTick = GetCurrentTick();
    }

    public WorldState CaptureState()
    {
        return Current;
    }

    public void RestoreState(WorldState state)
    {
        Current = state;
    }

    public void OnAfterLoad()
    {
        RebuildRuntimeCaches();
    }
}
```

`OnBeforeSave()` runs before provider state is captured. `OnAfterLoad()` runs after all providers in the restored snapshot have received state.

Use lifecycle hooks for preparing persisted state or rebuilding runtime-only data. Do not use them to change provider identity, schema versions, registration, save paths, or serializer settings.

## Default State For Added Providers

Old saves may predate a provider that exists in the current application. Implement `ISaveDefaultStateProvider<TState>` when that provider can safely restore deterministic compatibility state instead of failing with `MissingProviderFile`.

```csharp
public sealed class AchievementsProvider :
    ISaveProvider<AchievementState>,
    ISaveDefaultStateProvider<AchievementState>
{
    public string SaveKey => "achievements";
    public int SchemaVersion => 1;
    public int LoadPriority => 20;

    public AchievementState Current { get; set; } = new AchievementState();

    public AchievementState CaptureState()
    {
        return Current;
    }

    public void RestoreState(AchievementState state)
    {
        Current = state;
    }

    public AchievementState CreateDefaultStateForMissingSave()
    {
        return new AchievementState
        {
            UnlockedIds = new List<string>()
        };
    }
}
```

Defaults should be stable constants or values derived from stable application rules. Avoid current time, random values, service calls, and other providers' runtime state.

## Provider Migrations

Implement `ISaveMigratable` when old payloads need to be upgraded before deserialization:

```csharp
public sealed class PlayerSaveProvider :
    ISaveProvider<PlayerState>,
    ISaveMigratable
{
    public string SaveKey => "player";
    public int SchemaVersion => 2;
    public int LoadPriority => 0;

    public PlayerState Current { get; set; } = new PlayerState();

    public PlayerState CaptureState() => Current;

    public void RestoreState(PlayerState state)
    {
        Current = state;
    }

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
            SaveMigrationStep.From(
                1,
                SaveMigrationStep.Rename("XP", "Experience"),
                SaveMigrationStep.AddIntDefault("Level", 1))
        };
}
```

Provider migrations are covered in detail in [Migrations](MIGRATIONS.md).

## Application Metadata Extensions

Use `ISaveMetadataProvider<TMetadata>` for application-owned metadata stored with each save folder, such as character name, playtime, difficulty, or screenshot references.

```csharp
public sealed class SaveMenuMetadataProvider :
    ISaveMetadataProvider<SaveMenuMetadata>
{
    public int MetadataSchemaVersion => 1;

    public SaveMenuMetadata Current { get; set; } = new SaveMenuMetadata();

    public SaveMenuMetadata CaptureMetadata()
    {
        return Current;
    }

    public void RestoreMetadata(SaveMenuMetadata metadata)
    {
        Current = metadata;
    }
}
```

Application metadata is per manager. Two different managers can use different metadata types because each manager owns its own registrations and serializer configuration.

If metadata shape changes, increase `MetadataSchemaVersion` and implement `ISaveMetadataMigratable`:

```csharp
public sealed class SaveMenuMetadataProvider :
    ISaveMetadataProvider<SaveMenuMetadata>,
    ISaveMetadataMigratable
{
    public int MetadataSchemaVersion => 2;

    public SaveMenuMetadata Current { get; set; } = new SaveMenuMetadata();

    public SaveMenuMetadata CaptureMetadata() => Current;

    public void RestoreMetadata(SaveMenuMetadata metadata)
    {
        Current = metadata;
    }

    public ISaveMigrationSource CreateMetadataMigrationSource()
    {
        return new SaveMenuMetadataMigrations();
    }
}

public sealed class SaveMenuMetadataMigrations : ISaveMigrationSource
{
    public IReadOnlyList<SaveMigrationStep> Migrations { get; } =
        new[]
        {
            SaveMigrationStep.AddIntDefault(1, "PlaytimeSeconds", 0)
        };
}
```

The active serializer must implement `ISaveApplicationMetadataSerializer` for typed application metadata.

## Payload Transforms

Use `ISavePayloadTransform` when you want to wrap the bytes produced by another serializer without writing a full serializer.

```csharp
public sealed class XorPayloadTransform : ISavePayloadTransform
{
    private readonly byte _key;

    public XorPayloadTransform(byte key)
    {
        _key = key;
    }

    public string FileExtensionSuffix => ".xor";

    public byte[] Encode(byte[] data)
    {
        return Apply(data);
    }

    public byte[] Decode(byte[] data)
    {
        return Apply(data);
    }

    private byte[] Apply(byte[] data)
    {
        var copy = (byte[])data.Clone();

        for (var i = 0; i < copy.Length; i++)
            copy[i] = (byte)(copy[i] ^ _key);

        return copy;
    }
}
```

Register it by wrapping an inner serializer:

```csharp
var serializer = new TransformedSaveSerializer(
    new JsonSaveSerializer(),
    new XorPayloadTransform(0x5A));

var manager = SaveManager<string>.CreateDefault(
    serializer,
    saveRootPath: "Saves");
```

`TransformedSaveSerializer` appends the transform suffix to the inner extension. JSON plus `.xor` writes provider payloads with the extension `.json.xor`.

Transforms should be deterministic and reversible:

- `Decode(Encode(data))` must return the original bytes.
- `FileExtensionSuffix` must start with `.` and remain stable for existing saves.
- The transform should not inspect provider state types or save keys.
- Return new byte arrays instead of mutating caller-owned arrays.

Use a transform for byte-level concerns. Use a serializer when the payload format itself changes.

## Full Serializer Extensions

Implement `ISaveSerializer` when you need a new payload format.

The manager uses `ISaveSerializer` for provider payload files:

1. At provider registration, it calls `CreateSchematic(providerStateType)`.
2. It sets the schematic `SchemaVersion` from the provider.
3. On save, it captures provider state and calls `Serialize(...)`.
4. On load, it calls `ExtractSchemaVersion(...)` to decide whether migration is needed.
5. It calls `Deserialize(...)` after migration, or immediately when no migration is needed.

```csharp
public sealed class TextSaveSerializer : ISaveSerializer
{
    public string FileExtension => ".txtsave";

    public ISaveMigrationCapableSerializer? Migration => null;

    public ISaveSerializerMetadataHandler? Metadata => null;

    public ISaveSchematic CreateSchematic(Type stateType)
    {
        if (stateType != typeof(string))
            throw new ArgumentException("TextSaveSerializer only supports string state.", nameof(stateType));

        return new TextSaveSchematic();
    }

    public byte[] Serialize(object data, ISaveSchematic schematic)
    {
        return schematic.SerializeUntyped(data);
    }

    public object? Deserialize(byte[] rawData, ISaveSchematic schematic)
    {
        return schematic.DeserializeUntyped(rawData);
    }

    public int ExtractSchemaVersion(byte[] serializedData)
    {
        var text = Encoding.UTF8.GetString(serializedData);

        if (!text.StartsWith("v:", StringComparison.Ordinal))
            throw new InvalidOperationException("Text save payload is missing schema version.");

        var endOfLine = text.IndexOf('\n');
        var versionText = endOfLine < 0 ? text.Substring(2) : text.Substring(2, endOfLine - 2);
        return int.Parse(versionText, CultureInfo.InvariantCulture);
    }
}
```

The matching schematic owns typed serialization:

```csharp
public sealed class TextSaveSchematic : SaveSchematic<string>
{
    public TextSaveSchematic()
        : base(schemaVersion: 1)
    {
    }

    public override byte[] Serialize(string? state)
    {
        var text = "v:" + SchemaVersion.ToString(CultureInfo.InvariantCulture) + "\n" + (state ?? string.Empty);
        return Encoding.UTF8.GetBytes(text);
    }

    public override string Deserialize(byte[] serialized)
    {
        var text = Encoding.UTF8.GetString(serialized);
        var endOfLine = text.IndexOf('\n');
        return endOfLine < 0 ? string.Empty : text.Substring(endOfLine + 1);
    }
}
```

These examples require `System`, `System.Globalization`, and `System.Text`.

Full serializers should:

- use a stable `FileExtension` beginning with `.`;
- include the schema version in each provider payload;
- throw clear `InvalidOperationException` or `ArgumentException` messages for unsupported data;
- preserve null behavior if the serializer supports nullable provider state;
- avoid global mutable state;
- keep application paths and engine objects outside the serializer.

## Schematics

`ISaveSchematic` describes how one provider state type is serialized and deserialized. `SaveSchematic<T>` is the usual base class because it provides the untyped bridge required by the manager.

The manager owns the final schematic version. A custom schematic constructor can set a placeholder version, but provider registration overwrites `SchemaVersion` with the provider's current schema version.

Override `ValidateStateType()` when a serializer has extra type limitations. For example, a serializer can reject abstract types, unsupported collection shapes, engine-owned types, or value types if the format cannot represent them safely.

Most serializers should create a generic schematic at runtime for the requested `stateType`. The built-in JSON serializer does this with reflection and `JsonSaveSchematic<T>`.

## Contextual Serializers

Implement `IContextualSaveSerializer` when serialization depends on provider context or serializer metadata.

Contextual methods receive `SaveSerializerContext`:

| Property | Meaning |
|---|---|
| `SaveKey` | Provider save key being processed. |
| `SchemaVersion` | Provider schema version for this operation. |
| `StateType` | Provider state type. |
| `Schematic` | Schematic created for this provider. |
| `SerializerMetadata` | Serializer-owned metadata for the containing save. |

```csharp
public sealed class ContextAwareSerializer :
    ISaveSerializer,
    IContextualSaveSerializer
{
    private readonly JsonSaveSerializer _inner = new JsonSaveSerializer();

    public string FileExtension => _inner.FileExtension;
    public ISaveMigrationCapableSerializer? Migration => _inner.Migration;
    public ISaveSerializerMetadataHandler? Metadata => _inner.Metadata;

    public ISaveSchematic CreateSchematic(Type stateType)
    {
        return _inner.CreateSchematic(stateType);
    }

    public byte[] Serialize(object data, ISaveSchematic schematic)
    {
        return _inner.Serialize(data, schematic);
    }

    public byte[] Serialize(object data, SaveSerializerContext context)
    {
        var providerMetadata = context.SerializerMetadata.GetOrCreateProvider(context.SaveKey);
        providerMetadata["stateType"] = context.StateType.FullName ?? context.StateType.Name;

        return _inner.Serialize(data, context.Schematic);
    }

    public object? Deserialize(byte[] rawData, ISaveSchematic schematic)
    {
        return _inner.Deserialize(rawData, schematic);
    }

    public object? Deserialize(byte[] rawData, SaveSerializerContext context)
    {
        return _inner.Deserialize(rawData, context.Schematic);
    }

    public int ExtractSchemaVersion(byte[] serializedData)
    {
        return _inner.ExtractSchemaVersion(serializedData);
    }

    public int ExtractSchemaVersion(byte[] serializedData, SaveSerializerContext context)
    {
        return _inner.ExtractSchemaVersion(serializedData);
    }
}
```

When a serializer implements `IContextualSaveSerializer`, the manager uses contextual methods for provider disk operations. Non-contextual methods still matter because they are the fallback path and part of the base `ISaveSerializer` contract.

## Serializer-Owned Metadata

Implement `ISaveSerializerMetadataHandler` when a serializer needs metadata stored in the save metadata file. This is for format concerns, not application save-menu data.

Examples:

- field maps for compact serializers;
- codec names or format options;
- per-provider type fingerprints;
- serializer compatibility values.

`WriteMetadata(...)` runs before metadata is persisted:

```csharp
public void WriteMetadata(SaveSerializerMetadataWriteContext context)
{
    context.Metadata.Global["serializer"] = "custom-json";

    foreach (var provider in context.Providers)
    {
        var metadata = context.Metadata.GetOrCreateProvider(provider.SaveKey);
        metadata["stateType"] = provider.StateType.FullName ?? provider.StateType.Name;
        metadata["schemaVersion"] = provider.SchemaVersion.ToString(CultureInfo.InvariantCulture);
    }
}
```

`ValidateMetadata(...)` runs when an existing save's metadata is checked:

```csharp
public void ValidateMetadata(SaveSerializerMetadataValidationContext context)
{
    if (!context.Metadata.Global.TryGetValue("serializer", out var serializerName))
        throw new InvalidOperationException("Save is missing serializer metadata.");

    if (serializerName != "custom-json")
        throw new InvalidOperationException("Save was written by an incompatible serializer.");

    foreach (var provider in context.Providers)
    {
        if (!context.Metadata.Providers.ContainsKey(provider.SaveKey))
            throw new InvalidOperationException("Save is missing serializer metadata for " + provider.SaveKey + ".");
    }
}
```

The context `Providers` list contains the providers included in the save or currently registered for validation. Each `SaveSerializerProviderInfo` includes `SaveKey`, `SchemaVersion`, `StateType`, and `Schematic`.

## Migration-Capable Serializers

Provider migrations require `ISaveSerializer.Migration` to return an `ISaveMigrationCapableSerializer`.

The migration adapter parses serializer bytes into an editable `ISaveDataNode` tree and writes the edited tree back:

```csharp
public sealed class CustomMigrationAdapter : ISaveMigrationCapableSerializer
{
    public ISaveDataNodeFactory NodeFactory { get; } = new CustomNodeFactory();

    public ISaveDataNode DeserializeToNode(byte[] data)
    {
        // Parse your payload and create nodes with NodeFactory.
        throw new NotImplementedException();
    }

    public byte[] SerializeFromNode(ISaveDataNode node)
    {
        // Convert the edited node tree back to your payload bytes.
        throw new NotImplementedException();
    }
}
```

`CustomNodeFactory` represents a serializer-owned implementation of `ISaveDataNodeFactory`. The package's built-in JSON node factory is internal, so custom migration-capable serializers need to provide their own node tree or depend on another public implementation.

Do not claim migration support until the serializer can round-trip every state shape it supports:

- object roots;
- array/list roots;
- string-key dictionary roots;
- primitive roots;
- null roots;
- supported numeric, string, bool, byte-array, and date-time values.

An `ISaveDataNode` implementation must enforce shape-specific operations clearly. Object nodes should support key access through `Has(...)`, `Get(...)`, `Set(...)`, `Remove(...)`, and `Keys`. Array nodes should support `Count`, `GetAt(...)`, `SetAt(...)`, `InsertAt(...)`, `RemoveAt(...)`, and `Add(...)`. Primitive nodes should support matching `As...` and `Set...` methods. All node types should support `IsObject()`, `IsArray()`, `IsNull()`, and `ReplaceWith(...)`.

Migration steps can only combine nodes created by the same factory that owns the current migration tree.

## Contextual Migration

Implement `IContextualSaveMigrationCapableSerializer` on the migration adapter when node conversion needs provider context or serializer metadata:

```csharp
private sealed class ContextualMigrationAdapter :
    ISaveMigrationCapableSerializer,
    IContextualSaveMigrationCapableSerializer
{
    public ISaveDataNodeFactory NodeFactory { get; } = new CustomNodeFactory();

    public ISaveDataNode DeserializeToNode(byte[] data)
    {
        throw new NotSupportedException("Context is required for this serializer.");
    }

    public byte[] SerializeFromNode(ISaveDataNode node)
    {
        throw new NotSupportedException("Context is required for this serializer.");
    }

    public ISaveDataNode DeserializeToNode(byte[] data, SaveSerializerContext context)
    {
        // Use context.SaveKey and context.SerializerMetadata if needed.
        throw new NotImplementedException();
    }

    public byte[] SerializeFromNode(ISaveDataNode node, SaveSerializerContext context)
    {
        throw new NotImplementedException();
    }
}
```

The non-contextual methods still have to exist because they are part of `ISaveMigrationCapableSerializer`. If your serializer cannot migrate without context, throw a clear `NotSupportedException` from the fallback methods.

`TransformedSaveSerializer` forwards contextual migration calls to the inner migration adapter when the inner adapter implements `IContextualSaveMigrationCapableSerializer`.

## Application Metadata Serializer Support

Implement `ISaveApplicationMetadataSerializer` when the serializer supports `ISaveMetadataProvider<TMetadata>`.

The serializer converts between typed application metadata and serializer-native inline data stored in `SaveApplicationMetadata.Data`:

```csharp
public object? SerializeApplicationMetadata(object? metadata, SaveSerializerContext context)
{
    return JObject.FromObject(metadata ?? new object());
}

public object? DeserializeApplicationMetadata(object? data, SaveSerializerContext context)
{
    return ((JToken)data!).ToObject(context.StateType);
}

public ISaveDataNode DeserializeApplicationMetadataToNode(object? data, SaveSerializerContext context)
{
    return ConvertNativeMetadataToNode(data);
}

public object? SerializeApplicationMetadataFromNode(ISaveDataNode node, SaveSerializerContext context)
{
    return ConvertNodeToNativeMetadata(node);
}
```

This example uses `Newtonsoft.Json.Linq` types. A different serializer should return its own native inline metadata representation.

Application metadata migration uses the node methods. If a serializer can serialize metadata but cannot convert it to and from nodes, it does not fully support metadata migrations.

The metadata context uses the reserved save key `__workes_application_metadata`. Treat that key as reserved and do not use it for provider save keys.

## Wrapper Serializers

A wrapper serializer decorates another serializer. `TransformedSaveSerializer` and `CompressedSaveSerializer` are examples.

Wrapper serializers should forward capabilities from the inner serializer:

- `CreateSchematic(...)` usually returns the inner schematic.
- `Metadata` usually returns the inner metadata handler.
- `Migration` should wrap the inner migration adapter when the wrapper changes payload bytes.
- `IContextualSaveSerializer` should call inner contextual methods when available.
- `ISaveApplicationMetadataSerializer` should forward metadata operations when the inner serializer supports them.

If a wrapper changes bytes on disk, it must also apply the inverse operation before schema extraction, deserialization, and migration-node parsing.

## Validation And Failure Behavior

`ValidateRegistrations()` exercises extension contracts before disk operations. It catches common setup errors:

- unsupported provider state types;
- invalid schema versions;
- duplicate provider keys or resolved file names;
- serializers that cannot create schematics;
- serializers that cannot serialize current provider state;
- unsupported application metadata serializer capabilities;
- duplicate or null migration steps.

Disk validation and load operations catch persisted-data problems:

- corrupt payload bytes;
- incompatible serializer metadata;
- newer saved schema versions;
- missing migration paths;
- migration step failures;
- deserialization failures after migration.

Extension implementations should throw clear exceptions when they reject unsupported data. The manager converts many disk-operation failures into structured statuses for `TryLoadFromDisk(...)`, `ValidateSave(...)`, and related methods.

## Testing Extensions

Recommended coverage depends on the extension type.

For providers:

- save and load representative state;
- validate load priority interactions;
- test lifecycle callbacks when implemented;
- test missing provider file behavior for old saves;
- test migrations with real old payloads.

For transforms:

- assert `Decode(Encode(data))` returns the original bytes;
- save and load through `TransformedSaveSerializer`;
- verify the final file extension;
- test invalid or corrupt transformed bytes.

For serializers:

- `ValidateRegistrations()` succeeds with supported state types;
- unsupported state types fail clearly;
- schema version extraction works without full deserialization;
- null state behavior matches the serializer's documented support;
- output can be saved, validated, loaded, and backed up;
- migration nodes round-trip every supported root shape;
- serializer metadata is written and validated when implemented;
- application metadata saves, loads, and migrates when supported.

For wrappers:

- capability forwarding matches the inner serializer;
- contextual operations receive the original context;
- migration still works through the wrapper;
- application metadata behavior matches the inner serializer.

## Extension Checklist

- Keep save keys, file extensions, metadata keys, and schema meanings stable.
- Add migrations in the same change that updates persisted state shape.
- Keep extension behavior deterministic.
- Validate before mutating persistent files.
- Prefer transforms and wrappers before writing a full serializer.
- Prefer provider-local logic before changing manager-level behavior.
- Keep engine paths, scene objects, UI, and service calls outside serializer formats.
- Document any custom format's schema version, file extension, metadata keys, and migration guarantees.
