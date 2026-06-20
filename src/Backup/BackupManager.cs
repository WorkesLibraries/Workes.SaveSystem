using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Workes.SaveSystem
{
    /// <summary>
    /// Manages backup operations for save files. Handles backup creation, rotation, and loading.
    /// </summary>
    internal sealed class BackupManager
    {
        private const string BackupFolderName = "_backup";

        private readonly string _saveRootPath;
        private readonly bool _enableBackupSystem;
        private readonly int _maxBackupCount;

        public BackupManager(string saveRootPath, bool enableBackupSystem, int maxBackupCount)
        {
            _saveRootPath = saveRootPath;
            _enableBackupSystem = enableBackupSystem;
            _maxBackupCount = maxBackupCount;
        }

        /// <summary>
        /// Gets the path to the backup folder for a specific save name and backup suffix.
        /// </summary>
        public string GetBackupFolderPath(string saveName, string backupSuffix)
        {
            return Path.Combine(_saveRootPath, BackupFolderName, saveName + backupSuffix);
        }

        /// <summary>
        /// Gets the path to the main backup folder.
        /// </summary>
        public string GetBackupMainFolderPath()
        {
            return Path.Combine(_saveRootPath, BackupFolderName);
        }

        /// <summary>
        /// Prepares existing backups for a new backup by rotating them forward.
        /// Returns the path to the backup that should be deleted if it exceeds max count, or null if none.
        /// </summary>
        public string? PrepareExistingBackupsForNewBackup(string saveName)
        {
            CorrectBackupIndices(saveName);

            Dictionary<int, string> backupIndicesAndDirectories = GetBackupIndicesAndDirectories(saveName);

            if (backupIndicesAndDirectories.Count == 0)
                return null;

            int max = backupIndicesAndDirectories.Keys.Max();
            bool resultsInExcessiveBackup = backupIndicesAndDirectories.Count >= _maxBackupCount;
            string? excessiveBackupDirectory = resultsInExcessiveBackup ? GetBackupFolderPath(saveName, $"_{max + 1:D4}") : null;

            for (int i = max; i >= 1; i--)
            {
                if (!Directory.Exists(GetBackupFolderPath(saveName, $"_{i:D4}")))
                {
                    continue;
                }
                string newPath = GetBackupFolderPath(saveName, $"_{i + 1:D4}");
                Directory.Move(GetBackupFolderPath(saveName, $"_{i:D4}"), newPath);
            }
            return excessiveBackupDirectory;
        }

        /// <summary>
        /// Gets the path where the old save should be moved during atomic swap.
        /// </summary>
        public string GetOldSaveDestinationPath(string saveName, string toDeletePath)
        {
            if (!_enableBackupSystem)
                return toDeletePath;

            var backupPath = GetBackupFolderPath(saveName, "_0001");
            if (!Directory.Exists(GetBackupMainFolderPath()))
                Directory.CreateDirectory(GetBackupMainFolderPath());
            return backupPath;
        }

        /// <summary>
        /// Cleans up after atomic swap, deleting excessive backups or to-delete folders.
        /// </summary>
        public void CleanupAfterSwap(string toDeletePath, string? excessiveBackupPath)
        {
            if (!_enableBackupSystem && Directory.Exists(toDeletePath))
                Directory.Delete(toDeletePath, true);
            else if (_enableBackupSystem && excessiveBackupPath != null)
            {
                Directory.Delete(excessiveBackupPath, true);
            }
        }

        /// <summary>
        /// Corrects backup index sequencing before the "make space" sweep.
        /// Deletes backups beyond MaxBackupCount (considered tampered). Fills gaps in the sequence
        /// (e.g. _0001, _0002, _0004 -> rename _0004 to _0003). Assumes higher index = newer save.
        /// </summary>
        private void CorrectBackupIndices(string saveName)
        {
            var backupMainFolderPath = GetBackupMainFolderPath();
            if (!Directory.Exists(backupMainFolderPath))
                return;

            var backupDirNames = GetBackupDirectoriesForSave(saveName);
            var validIndices = new List<int>();

            foreach (var dirName in backupDirNames)
            {
                var suffix = dirName.Split('_').LastOrDefault();
                if (string.IsNullOrEmpty(suffix) || !int.TryParse(suffix, out int index))
                    continue;

                if (index > _maxBackupCount)
                {
                    var path = Path.Combine(backupMainFolderPath, dirName);
                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                }
                else if (index >= 1)
                {
                    validIndices.Add(index);
                }
            }

            validIndices.Sort();
            if (validIndices.Count == 0)
                return;

            var targetSequence = Enumerable.Range(1, validIndices.Count).ToList();
            var relocationPairs = validIndices
                .Zip(targetSequence, (oldIdx, newIdx) => (oldIdx, newIdx))
                .Where(p => p.oldIdx != p.newIdx)
                .OrderBy(p => p.oldIdx)
                .ToList();

            foreach (var (oldIdx, newIdx) in relocationPairs)
            {
                var oldPath = GetBackupFolderPath(saveName, $"_{oldIdx:D4}");
                var newPath = GetBackupFolderPath(saveName, $"_{newIdx:D4}");
                if (Directory.Exists(newPath))
                {
                    SaveSystemDiagnostics.LogWarning($"Backup normalization skipped: directory {newPath} already exists. This may indicate backup folder tampering.");
                    continue;
                }
                
                if (Directory.Exists(oldPath))
                    Directory.Move(oldPath, newPath);
            }
        }

        private Dictionary<int, string> GetBackupIndicesAndDirectories(string saveName)
        {
            List<string> backupsForSave = GetBackupDirectoriesForSave(saveName);

            Dictionary<int, string> backupIndicesAndDirectories = new Dictionary<int, string>();
            foreach (var backupDirectory in backupsForSave)
            {
                if (int.TryParse(backupDirectory.Split('_').Last(), out int backupIndex))
                {
                    backupIndicesAndDirectories.Add(backupIndex, backupDirectory);
                }
            }
            return backupIndicesAndDirectories;
        }

        private List<string> GetBackupDirectoriesForSave(string saveName)
        {
            var backupMainFolderPath = GetBackupMainFolderPath();
            if (!Directory.Exists(backupMainFolderPath))
                return new List<string>();
                
            List<string> allBackupDirectoryNames = Directory.GetDirectories(backupMainFolderPath)
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Select(name => name!)
                .ToList();
            List<string> backupDirectoryNames = allBackupDirectoryNames
                .Where(dirName => IsBackupDirectoryForSave(dirName, saveName))
                .ToList();

            return backupDirectoryNames;
        }

        private static bool IsBackupDirectoryForSave(string directoryName, string saveName)
        {
            const int backupSuffixLength = 5;

            if (directoryName.Length != saveName.Length + backupSuffixLength)
                return false;

            if (!directoryName.StartsWith(saveName, StringComparison.Ordinal))
                return false;

            if (directoryName[saveName.Length] != '_')
                return false;

            for (int i = saveName.Length + 1; i < directoryName.Length; i++)
            {
                if (!char.IsDigit(directoryName[i]))
                    return false;
            }

            return true;
        }
    }
}
