using Newtonsoft.Json;
using System;
using System.IO;

namespace Workes.SaveSystem
{
    /// <summary>
    /// A JSON-based save schematic that uses Newtonsoft.Json for serialization.
    /// Automatically wraps state in a versioned payload to track schema versions.
    /// </summary>
    /// <typeparam name="T">The type of state object this schematic serializes. Must be JSON-compatible.</typeparam>
    public sealed class JsonSaveSchematic<T> : SaveSchematic<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSaveSchematic{T}"/> class.
        /// The manager will set the schema version from the provider at registration.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when type T is not compatible with JSON serialization (e.g., requires parameterless constructor, must be JSON-serializable).</exception>
        public JsonSaveSchematic()
            : base(1)
        {
            ValidateJsonCompatibility();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSaveSchematic{T}"/> class with an initial schema version.
        /// The manager will overwrite this with the provider's schema version at registration.
        /// </summary>
        /// <param name="schemaVersion">The initial schema version. Ignored after registration.</param>
        /// <exception cref="InvalidOperationException">Thrown when type T is not compatible with JSON serialization.</exception>
        public JsonSaveSchematic(int schemaVersion)
            : base(schemaVersion)
        {
            ValidateJsonCompatibility();
        }

        /// <inheritdoc />
        public override string Serialize(T state)
        {
            var payload = new VersionedPayload<T>
            {
                SchemaVersion = SchemaVersion,
                Data = state
            };
            return JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            });
        }

        /// <inheritdoc />
        public override T Deserialize(string serialized)
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<VersionedPayload<T>>(serialized);
                if (payload == null)
                    throw new InvalidOperationException("Deserialized payload was null");

                if (payload.SchemaVersion != SchemaVersion)
                {
                    throw new InvalidOperationException(
                        $"Schema mismatch for {typeof(T).Name}. " +
                        $"Save is v{payload.SchemaVersion}, provider expects v{SchemaVersion}."
                    );
                }

                return payload.Data;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse JSON save payload", ex);
            }
        }
        
        private void ValidateJsonCompatibility()
        {
            try
            {
                var instance = Activator.CreateInstance<T>();
        
                var payload = new VersionedPayload<T>
                {
                    SchemaVersion = SchemaVersion,
                    Data = instance
                };
        
                var json = JsonConvert.SerializeObject(payload);
                JsonConvert.DeserializeObject<VersionedPayload<T>>(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Type {typeof(T).Name} is not compatible with JSON save serialization.",
                    ex
                );
            }
        }
    }
}
