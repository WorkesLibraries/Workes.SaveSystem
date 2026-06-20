using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Defines the serialization format used for saving data to disk.
    /// The serializer creates schematics that convert provider states into file formats;
    /// each serializer type has a matching schematic type (e.g. JSON serializer creates JSON schematics).
    /// </summary>
    public interface ISaveSerializer
    {
        /// <summary>
        /// Gets the file extension (including the leading dot) used for save files.
        /// </summary>
        string FileExtension { get; }

        /// <summary>
        /// Creates a schematic for the given state type. The schematic is used to serialize and deserialize
        /// provider state; the manager sets its schema version from the provider at registration.
        /// </summary>
        /// <param name="stateType">The type of state object to create a schematic for (e.g. the provider's state type).</param>
        /// <returns>A schematic that can serialize and deserialize instances of <paramref name="stateType"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when the serializer does not support the given state type.</exception>
        ISaveSchematic CreateSchematic(Type stateType);

        /// <summary>
        /// Serializes data using the specified schematic.
        /// </summary>
        /// <param name="data">The data object to serialize.</param>
        /// <param name="schematic">The schematic that defines how to serialize the data.</param>
        /// <returns>A serialized string representation of the data.</returns>
        string Serialize(object data, ISaveSchematic schematic);

        /// <summary>
        /// Deserializes raw data using the specified schematic.
        /// </summary>
        /// <param name="rawData">The raw serialized string to deserialize.</param>
        /// <param name="schematic">The schematic that defines how to deserialize the data.</param>
        /// <returns>The deserialized data object.</returns>
        object Deserialize(string rawData, ISaveSchematic schematic);

        /// <summary>
        /// Extracts the schema version from serialized data without fully deserializing it.
        /// This is a required method as schema versioning is fundamental to the save system.
        /// </summary>
        /// <param name="serializedData">The serialized string to extract the schema version from.</param>
        /// <returns>The schema version extracted from the serialized data.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the schema version cannot be extracted from the serialized data.</exception>
        int ExtractSchemaVersion(string serializedData);
    }
}