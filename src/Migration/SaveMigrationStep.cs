using System;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Represents a single migration step that transforms data from one schema version to the next.
    /// The migration action receives both the data node and a factory for creating new nodes.
    /// </summary>
    public sealed class SaveMigrationStep
    {
        /// <summary>
        /// Gets the schema version this step migrates from.
        /// </summary>
        public int FromVersion { get; }

        /// <summary>
        /// Gets the migration action that mutates a serialized data node from <see cref="FromVersion"/> to the next version.
        /// </summary>
        public Action<ISaveDataNode, ISaveDataNodeFactory> Migrate { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SaveMigrationStep"/> class.
        /// </summary>
        /// <param name="fromVersion">The version this migration step migrates from (migrates to fromVersion + 1).</param>
        /// <param name="migrate">The migration action that transforms the data. Receives the data node and a factory for creating new nodes.</param>
        public SaveMigrationStep(int fromVersion, Action<ISaveDataNode, ISaveDataNodeFactory> migrate)
        {
            if (fromVersion < 1)
                throw new ArgumentException("From version must be greater than 0", nameof(fromVersion));

            if (migrate == null)
                throw new ArgumentNullException(nameof(migrate));

            FromVersion = fromVersion;
            Migrate = migrate;
        }

        /// <summary>
        /// Creates a migration step from one or more migration actions.
        /// </summary>
        /// <param name="fromVersion">The version this migration step migrates from.</param>
        /// <param name="migrations">The migration actions to run in order.</param>
        /// <returns>A migration step that runs the provided actions in order.</returns>
        public static SaveMigrationStep From(
            int fromVersion,
            params Action<ISaveDataNode, ISaveDataNodeFactory>[] migrations)
        {
            if (migrations == null)
                throw new ArgumentNullException(nameof(migrations));

            if (migrations.Length == 0)
                throw new ArgumentException("At least one migration action is required.", nameof(migrations));

            foreach (var migration in migrations)
            {
                if (migration == null)
                    throw new ArgumentException("Migration action list cannot contain null entries.", nameof(migrations));
            }

            return new SaveMigrationStep(fromVersion, (data, factory) =>
            {
                foreach (var migration in migrations)
                    migration(data, factory);
            });
        }

        /// <summary>
        /// Creates a migration step that adds a field only when it does not already exist.
        /// </summary>
        /// <param name="fromVersion">The version this migration step migrates from.</param>
        /// <param name="key">The object field to add.</param>
        /// <param name="createValue">Creates the value to add using the active serializer's node factory.</param>
        /// <returns>A migration step that adds a default field value.</returns>
        public static SaveMigrationStep AddDefault(
            int fromVersion,
            string key,
            Func<ISaveDataNodeFactory, ISaveDataNode> createValue)
        {
            return From(fromVersion, AddDefault(key, createValue));
        }

        /// <summary>
        /// Creates a migration action that adds a field only when it does not already exist.
        /// </summary>
        /// <param name="key">The object field to add.</param>
        /// <param name="createValue">Creates the value to add using the active serializer's node factory.</param>
        /// <returns>A migration action that adds a default field value.</returns>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> AddDefault(
            string key,
            Func<ISaveDataNodeFactory, ISaveDataNode> createValue)
        {
            ValidateKey(key, nameof(key));
            ValidateCreateValue(createValue);

            return (data, factory) =>
            {
                if (!data.Has(key))
                    data.Set(key, CreateValue(createValue, factory));
            };
        }

        /// <summary>
        /// Creates a migration step that adds an integer field only when it does not already exist.
        /// </summary>
        public static SaveMigrationStep AddIntDefault(int fromVersion, string key, int value)
            => AddDefault(fromVersion, key, factory => factory.CreateInt(value));

        /// <summary>
        /// Creates a migration action that adds an integer field only when it does not already exist.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> AddIntDefault(string key, int value)
            => AddDefault(key, factory => factory.CreateInt(value));

        /// <summary>
        /// Creates a migration step that adds a 64-bit integer field only when it does not already exist.
        /// </summary>
        public static SaveMigrationStep AddLongDefault(int fromVersion, string key, long value)
            => AddDefault(fromVersion, key, factory => factory.CreateLong(value));

        /// <summary>
        /// Creates a migration action that adds a 64-bit integer field only when it does not already exist.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> AddLongDefault(string key, long value)
            => AddDefault(key, factory => factory.CreateLong(value));

        /// <summary>
        /// Creates a migration step that adds a floating-point field only when it does not already exist.
        /// </summary>
        public static SaveMigrationStep AddFloatDefault(int fromVersion, string key, float value)
            => AddDefault(fromVersion, key, factory => factory.CreateFloat(value));

        /// <summary>
        /// Creates a migration action that adds a floating-point field only when it does not already exist.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> AddFloatDefault(string key, float value)
            => AddDefault(key, factory => factory.CreateFloat(value));

        /// <summary>
        /// Creates a migration step that adds a double-precision floating-point field only when it does not already exist.
        /// </summary>
        public static SaveMigrationStep AddDoubleDefault(int fromVersion, string key, double value)
            => AddDefault(fromVersion, key, factory => factory.CreateDouble(value));

        /// <summary>
        /// Creates a migration action that adds a double-precision floating-point field only when it does not already exist.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> AddDoubleDefault(string key, double value)
            => AddDefault(key, factory => factory.CreateDouble(value));

        /// <summary>
        /// Creates a migration step that adds a decimal field only when it does not already exist.
        /// </summary>
        public static SaveMigrationStep AddDecimalDefault(int fromVersion, string key, decimal value)
            => AddDefault(fromVersion, key, factory => factory.CreateDecimal(value));

        /// <summary>
        /// Creates a migration action that adds a decimal field only when it does not already exist.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> AddDecimalDefault(string key, decimal value)
            => AddDefault(key, factory => factory.CreateDecimal(value));

        /// <summary>
        /// Creates a migration step that adds a string field only when it does not already exist.
        /// </summary>
        public static SaveMigrationStep AddStringDefault(int fromVersion, string key, string value)
            => AddDefault(fromVersion, key, factory => factory.CreateString(value));

        /// <summary>
        /// Creates a migration action that adds a string field only when it does not already exist.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> AddStringDefault(string key, string value)
            => AddDefault(key, factory => factory.CreateString(value));

        /// <summary>
        /// Creates a migration step that adds a Boolean field only when it does not already exist.
        /// </summary>
        public static SaveMigrationStep AddBoolDefault(int fromVersion, string key, bool value)
            => AddDefault(fromVersion, key, factory => factory.CreateBool(value));

        /// <summary>
        /// Creates a migration action that adds a Boolean field only when it does not already exist.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> AddBoolDefault(string key, bool value)
            => AddDefault(key, factory => factory.CreateBool(value));

        /// <summary>
        /// Creates a migration step that adds a byte-array field only when it does not already exist.
        /// </summary>
        public static SaveMigrationStep AddBytesDefault(int fromVersion, string key, byte[] value)
            => AddDefault(fromVersion, key, factory => factory.CreateBytes(value));

        /// <summary>
        /// Creates a migration action that adds a byte-array field only when it does not already exist.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> AddBytesDefault(string key, byte[] value)
            => AddDefault(key, factory => factory.CreateBytes(value));

        /// <summary>
        /// Creates a migration step that adds a date/time field only when it does not already exist.
        /// </summary>
        public static SaveMigrationStep AddDateTimeDefault(int fromVersion, string key, DateTime value)
            => AddDefault(fromVersion, key, factory => factory.CreateDateTime(value));

        /// <summary>
        /// Creates a migration action that adds a date/time field only when it does not already exist.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> AddDateTimeDefault(string key, DateTime value)
            => AddDefault(key, factory => factory.CreateDateTime(value));

        /// <summary>
        /// Creates a migration step that adds a null field only when it does not already exist.
        /// </summary>
        public static SaveMigrationStep AddNullDefault(int fromVersion, string key)
            => AddDefault(fromVersion, key, factory => factory.CreateNull());

        /// <summary>
        /// Creates a migration action that adds a null field only when it does not already exist.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> AddNullDefault(string key)
            => AddDefault(key, factory => factory.CreateNull());

        /// <summary>
        /// Creates a migration step that sets a field to a value, replacing any existing value.
        /// </summary>
        /// <param name="fromVersion">The version this migration step migrates from.</param>
        /// <param name="key">The object field to set.</param>
        /// <param name="createValue">Creates the value to set using the active serializer's node factory.</param>
        /// <returns>A migration step that sets a field value.</returns>
        public static SaveMigrationStep Set(
            int fromVersion,
            string key,
            Func<ISaveDataNodeFactory, ISaveDataNode> createValue)
        {
            return From(fromVersion, Set(key, createValue));
        }

        /// <summary>
        /// Creates a migration action that sets a field to a value, replacing any existing value.
        /// </summary>
        /// <param name="key">The object field to set.</param>
        /// <param name="createValue">Creates the value to set using the active serializer's node factory.</param>
        /// <returns>A migration action that sets a field value.</returns>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> Set(
            string key,
            Func<ISaveDataNodeFactory, ISaveDataNode> createValue)
        {
            ValidateKey(key, nameof(key));
            ValidateCreateValue(createValue);

            return (data, factory) =>
            {
                data.Set(key, CreateValue(createValue, factory));
            };
        }

        /// <summary>
        /// Creates a migration step that sets an integer field.
        /// </summary>
        public static SaveMigrationStep SetInt(int fromVersion, string key, int value)
            => Set(fromVersion, key, factory => factory.CreateInt(value));

        /// <summary>
        /// Creates a migration action that sets an integer field.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> SetInt(string key, int value)
            => Set(key, factory => factory.CreateInt(value));

        /// <summary>
        /// Creates a migration step that sets a 64-bit integer field.
        /// </summary>
        public static SaveMigrationStep SetLong(int fromVersion, string key, long value)
            => Set(fromVersion, key, factory => factory.CreateLong(value));

        /// <summary>
        /// Creates a migration action that sets a 64-bit integer field.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> SetLong(string key, long value)
            => Set(key, factory => factory.CreateLong(value));

        /// <summary>
        /// Creates a migration step that sets a floating-point field.
        /// </summary>
        public static SaveMigrationStep SetFloat(int fromVersion, string key, float value)
            => Set(fromVersion, key, factory => factory.CreateFloat(value));

        /// <summary>
        /// Creates a migration action that sets a floating-point field.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> SetFloat(string key, float value)
            => Set(key, factory => factory.CreateFloat(value));

        /// <summary>
        /// Creates a migration step that sets a double-precision floating-point field.
        /// </summary>
        public static SaveMigrationStep SetDouble(int fromVersion, string key, double value)
            => Set(fromVersion, key, factory => factory.CreateDouble(value));

        /// <summary>
        /// Creates a migration action that sets a double-precision floating-point field.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> SetDouble(string key, double value)
            => Set(key, factory => factory.CreateDouble(value));

        /// <summary>
        /// Creates a migration step that sets a decimal field.
        /// </summary>
        public static SaveMigrationStep SetDecimal(int fromVersion, string key, decimal value)
            => Set(fromVersion, key, factory => factory.CreateDecimal(value));

        /// <summary>
        /// Creates a migration action that sets a decimal field.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> SetDecimal(string key, decimal value)
            => Set(key, factory => factory.CreateDecimal(value));

        /// <summary>
        /// Creates a migration step that sets a string field.
        /// </summary>
        public static SaveMigrationStep SetString(int fromVersion, string key, string value)
            => Set(fromVersion, key, factory => factory.CreateString(value));

        /// <summary>
        /// Creates a migration action that sets a string field.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> SetString(string key, string value)
            => Set(key, factory => factory.CreateString(value));

        /// <summary>
        /// Creates a migration step that sets a Boolean field.
        /// </summary>
        public static SaveMigrationStep SetBool(int fromVersion, string key, bool value)
            => Set(fromVersion, key, factory => factory.CreateBool(value));

        /// <summary>
        /// Creates a migration action that sets a Boolean field.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> SetBool(string key, bool value)
            => Set(key, factory => factory.CreateBool(value));

        /// <summary>
        /// Creates a migration step that sets a byte-array field.
        /// </summary>
        public static SaveMigrationStep SetBytes(int fromVersion, string key, byte[] value)
            => Set(fromVersion, key, factory => factory.CreateBytes(value));

        /// <summary>
        /// Creates a migration action that sets a byte-array field.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> SetBytes(string key, byte[] value)
            => Set(key, factory => factory.CreateBytes(value));

        /// <summary>
        /// Creates a migration step that sets a date/time field.
        /// </summary>
        public static SaveMigrationStep SetDateTime(int fromVersion, string key, DateTime value)
            => Set(fromVersion, key, factory => factory.CreateDateTime(value));

        /// <summary>
        /// Creates a migration action that sets a date/time field.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> SetDateTime(string key, DateTime value)
            => Set(key, factory => factory.CreateDateTime(value));

        /// <summary>
        /// Creates a migration step that sets a field to null.
        /// </summary>
        public static SaveMigrationStep SetNull(int fromVersion, string key)
            => Set(fromVersion, key, factory => factory.CreateNull());

        /// <summary>
        /// Creates a migration action that sets a field to null.
        /// </summary>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> SetNull(string key)
            => Set(key, factory => factory.CreateNull());

        /// <summary>
        /// Creates a migration step that removes a field if it exists.
        /// </summary>
        /// <param name="fromVersion">The version this migration step migrates from.</param>
        /// <param name="key">The object field to remove.</param>
        /// <returns>A migration step that removes a field.</returns>
        public static SaveMigrationStep Remove(int fromVersion, string key)
        {
            return From(fromVersion, Remove(key));
        }

        /// <summary>
        /// Creates a migration action that removes a field if it exists.
        /// </summary>
        /// <param name="key">The object field to remove.</param>
        /// <returns>A migration action that removes a field.</returns>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> Remove(string key)
        {
            ValidateKey(key, nameof(key));

            return (data, _) => data.Remove(key);
        }

        /// <summary>
        /// Creates a migration step that renames a field if the source field exists.
        /// </summary>
        /// <param name="fromVersion">The version this migration step migrates from.</param>
        /// <param name="oldKey">The existing object field to rename.</param>
        /// <param name="newKey">The new object field name.</param>
        /// <param name="overwrite">Whether to replace an existing target field.</param>
        /// <returns>A migration step that renames a field.</returns>
        public static SaveMigrationStep Rename(
            int fromVersion,
            string oldKey,
            string newKey,
            bool overwrite = false)
        {
            return From(fromVersion, Rename(oldKey, newKey, overwrite));
        }

        /// <summary>
        /// Creates a migration action that renames a field if the source field exists.
        /// </summary>
        /// <param name="oldKey">The existing object field to rename.</param>
        /// <param name="newKey">The new object field name.</param>
        /// <param name="overwrite">Whether to replace an existing target field.</param>
        /// <returns>A migration action that renames a field.</returns>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> Rename(
            string oldKey,
            string newKey,
            bool overwrite = false)
        {
            ValidateKey(oldKey, nameof(oldKey));
            ValidateKey(newKey, nameof(newKey));

            return (data, _) =>
            {
                if (!data.Has(oldKey))
                    return;

                if (oldKey == newKey)
                    return;

                if (data.Has(newKey) && !overwrite)
                {
                    throw new InvalidOperationException(
                        $"Cannot rename migration field '{oldKey}' to '{newKey}' because the target field already exists.");
                }

                data.Set(newKey, data.Get(oldKey));
                data.Remove(oldKey);
            };
        }

        /// <summary>
        /// Creates a migration step that moves a field if the source field exists.
        /// </summary>
        /// <param name="fromVersion">The version this migration step migrates from.</param>
        /// <param name="fromKey">The existing object field to move.</param>
        /// <param name="toKey">The target object field.</param>
        /// <param name="overwrite">Whether to replace an existing target field.</param>
        /// <returns>A migration step that moves a field.</returns>
        public static SaveMigrationStep Move(
            int fromVersion,
            string fromKey,
            string toKey,
            bool overwrite = false)
        {
            return Rename(fromVersion, fromKey, toKey, overwrite);
        }

        /// <summary>
        /// Creates a migration action that moves a field if the source field exists.
        /// </summary>
        /// <param name="fromKey">The existing object field to move.</param>
        /// <param name="toKey">The target object field.</param>
        /// <param name="overwrite">Whether to replace an existing target field.</param>
        /// <returns>A migration action that moves a field.</returns>
        public static Action<ISaveDataNode, ISaveDataNodeFactory> Move(
            string fromKey,
            string toKey,
            bool overwrite = false)
        {
            return Rename(fromKey, toKey, overwrite);
        }

        private static void ValidateKey(string key, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Migration field key cannot be null, empty, or whitespace.", parameterName);
        }

        private static void ValidateCreateValue(Func<ISaveDataNodeFactory, ISaveDataNode> createValue)
        {
            if (createValue == null)
                throw new ArgumentNullException(nameof(createValue));
        }

        private static ISaveDataNode CreateValue(
            Func<ISaveDataNodeFactory, ISaveDataNode> createValue,
            ISaveDataNodeFactory factory)
        {
            var node = createValue(factory);
            if (node == null)
                throw new InvalidOperationException("Migration value factory returned null.");

            return node;
        }
    }
}
