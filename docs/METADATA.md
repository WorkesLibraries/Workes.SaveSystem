# Save Metadata

Save metadata is package-owned data written alongside provider payloads. It supports slot discovery, validation, provider manifests, timestamps, serializer-owned metadata, and optional application metadata.

Metadata is serialized through the active serializer. With JSON, metadata is written as `metadata.json`.

Think of save metadata as the save folder's index. Provider files contain gameplay or application state, while metadata describes the save itself and the files that belong to it.

With JSON, a simple save folder usually looks like:

```text
Saves/
  slot-1/
    metadata.json
    player.json
    world.json
```

Application code usually reads metadata through manager APIs instead of deserializing `metadata.json` directly.

## Core Metadata

Core metadata is maintained by the save system. It includes:

- a stable `SaveId` used by recovery validation.
- `CreatedAtUtc` and `LastWrittenAtUtc` timestamps.
- whether application metadata exists.
- the stored application metadata schema version, when present.

Use `ReadSaveMetadata(...)` when a menu or tool only needs core information:

```csharp
var info = manager.ReadSaveMetadata("slot-1");

if (info != null)
{
    Console.WriteLine(info.LastWrittenAtUtc);
    Console.WriteLine(info.HasApplicationMetadata);
}
```

Backup slots expose the same read-only metadata shape through backup metadata APIs.

## Provider Manifests

The provider manifest records the provider save keys, schema versions, and resolved file names that existed when a save was written.

This lets the loader distinguish:

- an old save that predates a newly registered provider
- a current save whose provider file was deleted or corrupted

Use default-state provider hooks when old saves should load with deterministic state for newly added providers.

For example, if a save was written with `player` and `world` providers, then a later version adds a `quests` provider, the manifest tells the loader that `quests` was absent because the save is old. If the manifest lists `world` but `world.json` is missing, the save is treated as damaged instead.

## Application Metadata

Applications may register one typed metadata provider per save manager to store save-menu or display metadata without loading provider files.

Typical application metadata includes:

- character or profile name
- playtime
- difficulty
- location name
- screenshot or thumbnail reference
- application-owned display tags

Register one metadata provider per manager:

```csharp
public sealed class SaveMenuMetadata
{
    public string CharacterName { get; set; } = string.Empty;
    public int PlaytimeSeconds { get; set; }
}

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

```csharp
var metadataProvider = new SaveMenuMetadataProvider
{
    Current = new SaveMenuMetadata
    {
        CharacterName = "Scout",
        PlaytimeSeconds = 90
    }
};

manager.RegisterMetadataProvider(metadataProvider);
manager.ValidateRegistrations();
manager.SaveToDisk("slot-1");
```

The one-provider rule is scoped to a single `SaveManager<TIdentity>` instance. An application can still use different application metadata types for different save scopes by using different managers:

```csharp
profileManager.RegisterMetadataProvider(profileMetadataProvider);
slotManager.RegisterMetadataProvider(slotMetadataProvider);
```

Application metadata has its own schema version and can be read independently:

```csharp
var metadata = manager.ReadApplicationMetadata<SaveMenuMetadata>("slot-1");
```

Backup metadata can also be read without loading provider files:

```csharp
var metadata = manager.ReadBackupApplicationMetadata<SaveMenuMetadata>(
    "slot-1",
    slotNumber: 1);
```

Application metadata is restored during load when a metadata provider is registered. If no metadata provider is registered, provider payloads can still load normally.

Use application metadata for display and selection data. Do not make it the only copy of state that must be restored for gameplay; provider payloads should remain the source of truth for runtime state.

## Application Metadata Migration

Application metadata can implement migration through `ISaveMetadataMigratable`. It reuses the same migration-source model as provider payloads.

Serializers that support inline application metadata use `ISaveApplicationMetadataSerializer`. The reserved synthetic save key for serializer metadata is `__workes_application_metadata`.

Increase `MetadataSchemaVersion` when old application metadata needs migration to the current metadata type. This version is independent from provider `SchemaVersion` values.

## Serializer-Owned Metadata

Advanced serializers may store serializer-owned metadata inside save metadata. This is for format details such as field maps or codec settings.

Application code should not use serializer-owned metadata for save-menu display data. Use an application metadata provider instead.

## Corrupt Metadata

Existing metadata that deserializes to null or the wrong type fails strict validation/load behavior. Use explicit repair workflows such as `ForceSaveToDisk(...)` when the application intentionally wants to overwrite a broken save.

Missing metadata is different from missing application metadata. A save folder without `metadata.json` is not a complete save folder. A save whose metadata exists but has no application metadata can still be valid.

Use validation APIs before destructive repair tools:

```csharp
var result = manager.ValidateSave("slot-1");

if (result.Status != SaveLoadStatus.Success)
{
    Console.WriteLine(result.Status);
}
```
