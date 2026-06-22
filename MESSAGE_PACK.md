# MessagePack Companion Package Handoff

This document tracks core `Workes.SaveSystem` changes that affect the optional
`Workes.SaveSystem.MessagePack` package.

## Public Save Metadata Contract

`SaveMetadata` is now a public serializer-facing contract in the core package.
The MessagePack package should serialize and deserialize this type directly when
the core save system requests a schematic for metadata.

The C# contract uses public properties, but the serialized shape is unchanged:

- `SaveId`
- `CreatedAtUtc`
- `LastWrittenAtUtc`
- `SerializerMetadata`

The lifecycle helpers that create new metadata and prepare timestamps for write
are internal to the core manager and should not be part of companion-package API
usage.

`SaveMetadataInfo` remains the application-facing read/menu metadata type.
The MessagePack package should not expose `SerializerMetadata` through menu APIs.

## Required MessagePack Package Changes

When updating `Workes.SaveSystem.MessagePack` against this core version:

1. Remove any workaround that existed only because `SaveMetadata` was internal.
2. Keep support for `MessagePackSaveSerializer.CreateSchematic(typeof(SaveMetadata))`.
3. Ensure the property names above remain supported when serializing metadata.
4. Ensure metadata deserialize failures throw clearly.
5. Ensure metadata deserialization never returns `null` or a non-`SaveMetadata` object.
6. Keep `ForceSaveToDisk(...)` as the repair path for intentionally replacing corrupt or incompatible metadata.

If the MessagePack serializer still needs private resolvers for non-public provider
state types, that can remain a MessagePack package decision. It should no longer
be required for core save metadata alone.

## Future MessagePack Migration Work

MessagePack migration and field-map metadata remain companion-package work.
The intended direction is:

- store MessagePack field maps in `SaveSerializerMetadata`;
- decode compact MessagePack payloads plus saved field maps into named `ISaveDataNode` trees;
- run normal `SaveMigrationStep` migrations;
- encode migrated nodes back to compact current-schema MessagePack payloads.
