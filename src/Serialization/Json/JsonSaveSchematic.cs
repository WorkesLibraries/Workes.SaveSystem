using Newtonsoft.Json;
using System;
using System.Text;

namespace Workes.SaveSystem
{
    /// <summary>
    /// A JSON-based save schematic that uses Newtonsoft.Json for serialization.
    /// Automatically wraps state in a versioned payload to track schema versions.
    /// </summary>
    /// <typeparam name="T">The type of state object this schematic serializes. Must be compatible with Newtonsoft.Json.</typeparam>
    public sealed class JsonSaveSchematic<T> : SaveSchematic<T>
    {
        private readonly JsonSaveFormatting _formatting;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSaveSchematic{T}"/> class.
        /// The manager will set the schema version from the provider at registration.
        /// </summary>
        public JsonSaveSchematic()
            : this(JsonSaveFormatting.Pretty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSaveSchematic{T}"/> class.
        /// The manager will set the schema version from the provider at registration.
        /// </summary>
        /// <param name="formatting">The JSON formatting style to use when writing save payloads.</param>
        public JsonSaveSchematic(JsonSaveFormatting formatting)
            : base(1)
        {
            if (!Enum.IsDefined(typeof(JsonSaveFormatting), formatting))
                throw new ArgumentOutOfRangeException(nameof(formatting));

            _formatting = formatting;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSaveSchematic{T}"/> class with an initial schema version.
        /// The manager will overwrite this with the provider's schema version at registration.
        /// </summary>
        /// <param name="schemaVersion">The initial schema version. Ignored after registration.</param>
        public JsonSaveSchematic(int schemaVersion)
            : this(schemaVersion, JsonSaveFormatting.Pretty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSaveSchematic{T}"/> class with an initial schema version.
        /// The manager will overwrite this with the provider's schema version at registration.
        /// </summary>
        /// <param name="schemaVersion">The initial schema version. Ignored after registration.</param>
        /// <param name="formatting">The JSON formatting style to use when writing save payloads.</param>
        public JsonSaveSchematic(int schemaVersion, JsonSaveFormatting formatting)
            : base(schemaVersion)
        {
            if (!Enum.IsDefined(typeof(JsonSaveFormatting), formatting))
                throw new ArgumentOutOfRangeException(nameof(formatting));

            _formatting = formatting;
        }

        /// <inheritdoc />
        public override byte[] Serialize(T? state)
        {
            var payload = new VersionedPayload<T>
            {
                SchemaVersion = SchemaVersion,
                Data = state
            };
            var json = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                Formatting = JsonSaveSerializer.ToNewtonsoftFormatting(_formatting)
            });
            return Encoding.UTF8.GetBytes(json);
        }

        /// <inheritdoc />
        public override T? Deserialize(byte[] serialized)
        {
            try
            {
                var json = Encoding.UTF8.GetString(serialized);
                var payload = JsonConvert.DeserializeObject<VersionedPayload<T>>(json);
                if (payload == null)
                    throw new InvalidOperationException("Deserialized payload was null");

                if (payload.SchemaVersion != SchemaVersion)
                {
                    throw new InvalidOperationException(
                        $"Schema mismatch for {typeof(T).Name}. " +
                        $"Save is v{payload.SchemaVersion}, provider expects v{SchemaVersion}."
                    );
                }

                if (payload.Data == null && !SaveStateCompatibility.CanAcceptNull(typeof(T)))
                    throw new InvalidOperationException("Deserialized payload data was null, but this provider state type cannot accept null.");

                return payload.Data;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse JSON save payload", ex);
            }
        }
    }
}
