# API GUIDELINES

## Purpose

Describe the public API style and maintenance rules for `Workes.SaveSystem`.

This is a stable project-control document for maintainers. It is not an exhaustive API reference.

## API Design Principles

- Keep `SaveManager<TIdentity>` as the main orchestration entry point.
- Keep applications responsible for engine paths, serializer choice, provider state types, and release-specific migration decisions.
- Prefer explicit configuration through `SaveSystemOptions<TIdentity>` when defaults are not enough.
- Keep normal application APIs separate from serializer, migration, and provider extension contracts.
- Preserve engine neutrality; do not add Unity, Godot, or platform-specific assumptions to core APIs.

## Naming And Structure

- Use stable provider save keys as persistent identity.
- Use `SchemaVersion` for payload compatibility, not file names.
- Use `Try...` names for expected failure paths with structured result/status values.
- Use throwing wrappers only when success is normally expected or caller misuse should fail loudly.
- Keep extension interfaces small and named by the behavior they vary, such as serializers, metadata handlers, payload transforms, providers, migration sources, and default-state providers.

## Configuration Style

Default factories should remain convenient for simple .NET applications. Engine integrations should prefer explicit save-root configuration so the engine owns persistent storage location.

Options should keep resolver behavior visible:

- `savePathResolver` controls save identity to relative path mapping.
- `fileNameResolver` controls provider file naming.
- `missingProviderFileBehavior` controls strictness for missing provider files.
- `warningSink` lets applications route diagnostics without coupling the package to a logging framework.

## Error Handling And Validation

- Validate registrations before disk operations when provider sets change.
- Keep failed expected operations atomic.
- Use structured load statuses for load failures.
- Preserve strict metadata handling; corrupt metadata should fail validation/load unless the caller explicitly repairs with force-save behavior.
- Avoid making callers parse exception messages for control flow.

## Serializer And Migration APIs

Serializer APIs are part of the extension surface and should remain capability-based.

- `ISaveSerializer` is the base payload serializer contract.
- Migration support is optional and exposed through migration-capable serializer contracts.
- Metadata support is optional and exposed through metadata-capable serializer contracts.
- Contextual serializer APIs should receive enough save-key, schema-version, state-type, schematic, and serializer-owned metadata context to support compact external formats.
- Payload transforms should compose over existing serializers and preserve capability routing.

## Documentation Expectations

Public API or behavior changes should update the relevant public docs and `CHANGELOG.md` in the same work. README examples should show normal workflows first; detailed guidance should live in focused docs under `docs/`.
