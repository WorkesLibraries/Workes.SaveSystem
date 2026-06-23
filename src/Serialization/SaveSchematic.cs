using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Base class for type-safe save schematics. Provides a strongly-typed interface for serialization
    /// while implementing the untyped <see cref="ISaveSchematic"/> interface.
    /// </summary>
    /// <remarks>
    /// Serializer implementations usually create schematic instances during provider registration.
    /// The manager then assigns the provider schema version before any save data is written.
    /// </remarks>
    /// <typeparam name="T">The type of state object this schematic serializes.</typeparam>
    public abstract class SaveSchematic<T> : ISaveSchematic
    {
        private int _schemaVersion;

        /// <summary>
        /// Gets or sets the schema version. The manager sets this from the provider's <see cref="ISaveProvider.SchemaVersion"/> at registration.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the assigned version is less than 1.</exception>
        public int SchemaVersion
        {
            get => _schemaVersion;
            set
            {
                if (value < 1)
                    throw new ArgumentException("Schema version must be greater than 0", nameof(value));
                _schemaVersion = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SaveSchematic{T}"/> class.
        /// </summary>
        /// <param name="schemaVersion">The initial schema version for this schematic. The manager will set it from the provider at registration.</param>
        /// <exception cref="InvalidOperationException">Thrown when the type T is invalid (is abstract, etc.).</exception>
        protected SaveSchematic(int schemaVersion)
        {
            if (schemaVersion < 1)
                throw new ArgumentException("Schema version must be greater than 0", nameof(schemaVersion));

            _schemaVersion = schemaVersion;
            ValidateStateType();
        }

        /// <summary>
        /// Validates that <typeparamref name="T"/> can be used as a save-state type.
        /// </summary>
        protected virtual void ValidateStateType()
        {
            var type = typeof(T);

            if (type.IsAbstract)
            {
                throw new InvalidOperationException($"Type {type.Name} is abstract and cannot be used as a save-state");
            }
        }

        /// <summary>
        /// Serializes a state object of type T to bytes.
        /// </summary>
        /// <param name="state">The state object to serialize.</param>
        /// <returns>The serialized payload bytes.</returns>
        public abstract byte[] Serialize(T? state);

        /// <summary>
        /// Deserializes bytes back into a state object of type T.
        /// </summary>
        /// <param name="serialized">The serialized bytes to deserialize.</param>
        /// <returns>The deserialized state object.</returns>
        public abstract T? Deserialize(byte[] serialized);

        byte[] ISaveSchematic.SerializeUntyped(object state)
            => Serialize((T?)state);

        object? ISaveSchematic.DeserializeUntyped(byte[] serialized)
            => Deserialize(serialized);
    }
}
