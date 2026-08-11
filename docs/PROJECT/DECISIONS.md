# DECISIONS

## Purpose

Record long-lived architectural and packaging decisions for `Workes.SaveSystem`.

This file preserves durable rationale. It is not a task list.

## Decisions

### D-001: Keep JSON in the core package

#### Context

The core save system needs one built-in readable serializer for first-use ergonomics, metadata persistence, validation, and migration examples.

#### Decision

Keep `JsonSaveSerializer` in `Workes.SaveSystem` and keep the Newtonsoft.Json dependency as part of the current core package contract.

#### Reasoning

Splitting JSON into a separate package would make the first-release consumer experience worse because users would need both the core package and a serializer package before they could save anything. Under the current `netstandard2.1` target, replacing Newtonsoft.Json with `System.Text.Json` would still require an additional package reference and would not make the core package dependency-free.

#### Consequences

The package remains immediately usable with readable JSON saves. A dependency-free default serializer can be revisited later as a deliberate design project, but it should not be rushed into the stable core package.

### D-002: Keep MessagePack as a companion package

#### Context

MessagePack is useful for compact production saves, but it brings its own serializer dependency and metadata-mapping requirements.

#### Decision

Keep MessagePack implementation in the optional `Workes.SaveSystem.MessagePack` companion package. The core package exposes contextual serializer and application-metadata serializer contracts needed by companion serializers.

#### Reasoning

This keeps the core package focused and avoids forcing every consumer to take MessagePack dependencies. The companion package can evolve compact field maps, serializer metadata, migration support, and application metadata support without expanding the core package dependency graph.

#### Consequences

Core users can choose JSON or compressed JSON without installing MessagePack. Consumers that need compact saves install the companion package. Core serializer contracts must remain expressive enough for metadata-backed companion serializers.

### D-003: Keep GZip compression in the core package

#### Context

Compressed saves are a common requirement and can be implemented through platform libraries.

#### Decision

Keep `CompressedSaveSerializer` in the core package as a public serializer wrapper.

#### Reasoning

`System.IO.Compression.GZipStream` is platform-provided for the current target and does not add a NuGet dependency. Compression composes naturally around JSON or companion serializers.

#### Consequences

Consumers can reduce save size without another package. Compression and transform wrappers must preserve migration and metadata capabilities exposed by their inner serializers.

### D-004: Use serializer-native inline application metadata

#### Context

Applications need optional typed display metadata that can be read without loading provider files. JSON can store this metadata readably, while compact serializers such as MessagePack need serializer-owned inline representations and field maps.

#### Decision

Store application metadata in `SaveMetadata.ApplicationMetadata` as serializer-native inline data with its own schema version. Use `ISaveApplicationMetadataSerializer` as the optional serializer capability for reading, writing, node conversion, and migrated writes.

#### Reasoning

This keeps application metadata inside the save metadata document while allowing each serializer to preserve its native representation. It avoids embedding Base64 or nested byte payloads as the normal metadata model.

#### Consequences

JSON metadata remains readable. Companion serializers should support application metadata through the same provider-like context and field-map mechanisms used for provider payloads. The reserved synthetic save key is `__workes_application_metadata`.

### D-005: Use provider manifests to distinguish old saves from missing files

#### Context

Provider sets can grow over time. Loading an old save that predates a provider should not be treated the same as loading a current save with a deleted provider file.

#### Decision

Persist a provider manifest in save metadata and use default-state provider hooks for providers absent from old manifest-backed saves.

#### Reasoning

The manifest gives the loader enough context to distinguish intentional absence from corruption or deletion. Default-state hooks let newly added providers restore deterministic state for old saves.

#### Consequences

Current saves with deleted provider files can fail strictly, while old saves can remain loadable when providers opt into deterministic default state.
