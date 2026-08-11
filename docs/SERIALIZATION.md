# Serialization

`Workes.SaveSystem` persists provider payloads and save metadata through the active serializer.

## Built-In JSON

`JsonSaveSerializer` is the built-in readable serializer:

```csharp
var serializer = new JsonSaveSerializer();
```

The default constructor writes indented JSON. Use compact formatting for the same payload shape without formatting whitespace:

```csharp
var serializer = new JsonSaveSerializer(JsonSaveFormatting.Compact);
```

JSON save files use `.json` extensions. Save metadata is written as `metadata.json`.

## Dependency Notes

The core package currently depends on `Newtonsoft.Json` 13.0.3. That dependency is intentional for the current package shape because JSON serialization, JSON-backed migration nodes, and metadata persistence are part of the core package contract.

`System.Text.Json` is not part of the core package. Under the current `netstandard2.1` target, adding it would still require an additional package reference.

## Compression

Use `CompressedSaveSerializer` when you want smaller files without adding another NuGet dependency:

```csharp
var serializer = new CompressedSaveSerializer(
    new JsonSaveSerializer(JsonSaveFormatting.Compact));
```

Compressed JSON files use composed extensions such as `player.json.gz` and `metadata.json.gz`.

Compression forwards migration and metadata capabilities from the inner serializer.

## Payload Transforms

Payload transforms wrap any serializer with reversible byte encoding. Use this extension point for custom obfuscation or encryption:

```csharp
var serializer = new TransformedSaveSerializer(
    new JsonSaveSerializer(JsonSaveFormatting.Compact),
    new XorToyTransform());
```

Transforms should be deterministic and reversible. If a transform changes file extensions, keep the suffix stable after release.

## Companion MessagePack Package

The core package does not reference MessagePack. Compact MessagePack saves belong in the optional companion package:

```text
Workes.SaveSystem
Workes.SaveSystem.MessagePack
```

Use the companion package when compact binary saves are more important than direct readability.

Contextual serializer APIs let metadata-backed companion serializers use field maps during payload reads, writes, validation, and migration.

## Serializer Output Examples

The test suite includes serializer output examples for pretty JSON, compact JSON, and compressed compact JSON. These examples are generated under test `obj` output folders and are not part of the package.
