using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// A Base64 JSON save schematic that wraps state in a versioned payload and encodes readable JSON as Base64.
    /// </summary>
    /// <typeparam name="T">The type of state object this schematic serializes. Must be compatible with Newtonsoft.Json.</typeparam>
    public sealed class Base64JsonSaveSchematic<T> : SaveSchematic<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Base64JsonSaveSchematic{T}"/> class.
        /// The manager will set the schema version from the provider at registration.
        /// </summary>
        public Base64JsonSaveSchematic()
            : base(1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Base64JsonSaveSchematic{T}"/> class with an initial schema version.
        /// The manager will overwrite this with the provider's schema version at registration.
        /// </summary>
        /// <param name="schemaVersion">The initial schema version. Ignored after registration.</param>
        public Base64JsonSaveSchematic(int schemaVersion)
            : base(schemaVersion)
        {
        }

        /// <inheritdoc />
        public override string Serialize(T state)
        {
            var payload = new VersionedPayload<T>
            {
                SchemaVersion = SchemaVersion,
                Data = state
            };

            return Base64JsonSaveSerializer.SerializePayloadToBase64(payload);
        }

        /// <inheritdoc />
        public override T Deserialize(string serialized)
        {
            try
            {
                var payload = Base64JsonSaveSerializer.DeserializePayloadFromBase64<VersionedPayload<T>>(serialized);
                if (payload == null)
                    throw new InvalidOperationException("Deserialized payload was null");

                if (payload.SchemaVersion != SchemaVersion)
                {
                    throw new InvalidOperationException(
                        $"Schema mismatch for {typeof(T).Name}. " +
                        $"Save is v{payload.SchemaVersion}, provider expects v{SchemaVersion}."
                    );
                }

                if (payload.Data == null)
                    throw new InvalidOperationException("Deserialized payload data was null. Provider state cannot be null.");

                return payload.Data;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to parse Base64 JSON save payload", ex);
            }
        }
    }
}
