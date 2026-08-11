# ARCHITECTURE

## Purpose

Describe the internal structure and dependency flow of `Workes.SaveSystem`.

This is a stable project-control document for maintainers. It is not a user manual and should not duplicate focused public documentation under `docs/`.

## Current Architecture

`Workes.SaveSystem` is an engine-neutral save orchestration package. The core package owns provider registration, save snapshot capture, disk persistence, load/recovery behavior, backups, metadata, migration coordination, and JSON serialization.

Applications own their engine integration and choose the save root path. The package does not assume Unity, Godot, or any other runtime.

## Main Components

| Area | Responsibility |
|---|---|
| `Core/` | `SaveManager<TIdentity>`, load results, diagnostics, validation, and high-level save/load orchestration. |
| `Configuration/` | Save root, path/file resolution, missing-provider-file behavior, and warning sinks. |
| `Providers/` | State provider contracts, lifecycle hooks, migration hooks, and default-state hooks for old saves. |
| `Snapshot/` | Captured in-memory save state and serialized snapshot data. |
| `Persistence/` | Save metadata, provider manifests, application metadata, and metadata read models. |
| `Serialization/` | Serializer contracts, contextual serializer APIs, transforms, compression, schematics, and built-in JSON serialization. |
| `Data/` | Format-neutral migration node model and factory. |
| `Migration/` | Migration sources, migration steps, and migration engine. |
| `Backup/` | Backup slot rotation and backup cleanup support. |

## Save Flow

1. A caller creates `SaveManager<TIdentity>` with `SaveSystemOptions<TIdentity>` or a default factory.
2. The caller registers providers and validates registrations.
3. The manager captures provider state into a `SaveSnapshot`.
4. The active serializer writes provider payloads and metadata to disk.
5. Backup and recovery helpers protect previous or interrupted saves when configured.

## Load Flow

1. The manager resolves the save identity to a save path.
2. Metadata is loaded first through the active serializer.
3. The provider manifest determines which persisted provider files are expected.
4. Missing-provider behavior distinguishes old saves from current saves with missing files.
5. Provider payloads are deserialized or migrated, then restored in load-priority order.
6. Structured load results expose stable status values for expected failure handling.

## Migration Flow

Migration is serializer-agnostic at the manager level. Migration-capable serializers convert old payloads into `ISaveDataNode` trees, migration steps edit those nodes, and serializers write the migrated nodes back into their native payload format.

Provider state and application metadata share the same migration-source pattern. Application metadata uses the reserved synthetic save key `__workes_application_metadata` when serializers need provider-like context.

## Serializer Boundaries

The core package ships JSON through Newtonsoft.Json because JSON is the built-in readable serializer and metadata persistence depends on the active serializer.

Compression and transforms are wrappers around another serializer. They forward serializer capabilities so migration and metadata support are preserved through composition.

MessagePack is intentionally a companion package concern. The core package exposes contextual serializer and application-metadata serializer contracts so the companion package can support compact metadata-backed payloads without adding a MessagePack dependency to the core package.

## Important Constraints

- Validate before save/load workflows that depend on provider registration correctness.
- Keep provider keys and resolved file names stable across versions.
- Keep schema versions inside provider payloads rather than file names.
- Treat `TryLoad...` statuses as the stable load-failure contract; exception messages are diagnostic text.
- Do not add serializer dependencies to the core package unless they are part of the intended core contract.
- Do not use project-control docs as task trackers. Trello owns task state once mapped.
