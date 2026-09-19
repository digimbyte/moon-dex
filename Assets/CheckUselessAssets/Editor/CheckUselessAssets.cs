/*
    ╔══════════════════════════════════════════════════════════════════════════╗
    ║                     CHECK USELESS ASSETS   v1.0                          ║
    ║                         by Uppercut Studio                               ║
    ║                                                                          ║
    ║ This tool helps you clean up your project by scanning selected folders   ║
    ║ or files for assets that are not used in any enabled scene (from Build   ║
    ║ Settings). It then lets you choose to move these "useless" assets into   ║
    ║ a backup folder, while recording their original locations in a JSON file.║
    ║                                                                          ║
    ║ You can later mark assets as useful (i.e. move them back), select all    ║
    ║ useless assets at once, or simply open the backup folder to review them. ║
    ║                                                                          ║
    ║ This script is non-destructive – it only moves files and records their   ║
    ║ original paths so that you can revert if needed. Enjoy the tidying!      ║
    ╚══════════════════════════════════════════════════════════════════════════╝
*/

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace UppercutStudio.Tools
{
    [System.Serializable]
    public class BackupRecord
    {
        public string backupPath;    // The new location in the backup folder.
        public string originalPath;  // The original location before the asset was moved.
        public bool isTracked = true; // Flag to indicate if the file has a known origin.
        public System.DateTime timestamp = System.DateTime.Now; // When the file was moved.
    }

    [System.Serializable]
    public class BackupData
    {
        public List<BackupRecord> records = new List<BackupRecord>();
    }

    public static class CheckUselessAssets
    {
        // Configuration constants.
        private const string BACKUP_FOLDER_NAME = "UselessAssetsBackup";
        private const string INFO_FILE_NAME = "📋_README_FIRST.txt";
        private const string BACKUP_DATA_FILENAME = "_UselessBackupData_DO_NOT_TOUCH.json";
        private const string INFO_TEXT = 
@"# 📦 USELESS ASSETS MANAGER 📦

Welcome to the **Useless Assets Manager**!

## What is this folder?
This folder contains assets that have been flagged as *useless* because they are not referenced by any enabled scene in your Build Settings. Think of it as a temporary holding area for assets that may be candidates for removal.
💡Specially useful when combined with GIT for cleaner commits! 

## 🔄 How to restore assets
You can easily restore these assets to their original locations:
1. **Right-click** on any asset in this folder and select:
   > **Check Useless Assets → Mark as Useful**
2. To restore multiple assets, select them all and use the same menu option.

## 💡 Tips for Managing Assets
- **Non-Destructive:** Assets here are NOT deleted—they’re simply moved out of your main project structure.
- **Automatic Tracking:** All original locations are recorded automatically.
- **Quick Selection:** Use **Check Useless Assets → Find and Select Useless Assets** to quickly view all assets that appear unused.
- **Statistics:** Run the statistics function to see what types of files are taking up space and how much.

## ⚠️ Important Warning
Before permanently deleting these assets, please verify that:
- They are not used in disabled scenes.
- They are not loaded dynamically by your scripts at runtime.
- They are not referenced by assets outside your Build Settings.

## 📝 Credits
Made with ❤️ using **CheckUselessAssets** by Uppercut Studio.
";
        // Define paths for the backup folder and JSON data file.
        private static readonly string BackupFolder = $"Assets/{BACKUP_FOLDER_NAME}";
        private static readonly string BackupJsonPath = Path.Combine(BackupFolder, BACKUP_DATA_FILENAME);
        private static readonly string AbsoluteBackupFolder = Path.Combine(Application.dataPath, BACKUP_FOLDER_NAME);
        private static readonly string InfoFilePath = Path.Combine(AbsoluteBackupFolder, INFO_FILE_NAME);

        #region Data Management

        private static BackupData LoadBackupData()
        {
            if (File.Exists(BackupJsonPath))
            {
                string json = File.ReadAllText(BackupJsonPath);
                return JsonUtility.FromJson<BackupData>(json);
            }
            return new BackupData();
        }

        private static void SaveBackupData(BackupData data)
        {
            EnsureBackupFolderExists();
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(BackupJsonPath, json);
            AssetDatabase.Refresh();
        }

        private static void AddBackupRecord(string backupPath, string originalPath)
        {
            BackupData data = LoadBackupData();
            // Avoid duplicate entries.
            if (!data.records.Any(r => r.backupPath == backupPath))
            {
                data.records.Add(new BackupRecord
                {
                    backupPath = backupPath,
                    originalPath = originalPath,
                    isTracked = true,
                    timestamp = System.DateTime.Now
                });
                SaveBackupData(data);
            }
        }

        private static void RemoveBackupRecord(string backupPath)
        {
            BackupData data = LoadBackupData();
            data.records.RemoveAll(r => r.backupPath == backupPath);
            SaveBackupData(data);
        }

        private static BackupRecord GetBackupRecord(string backupPath)
        {
            BackupData data = LoadBackupData();
            return data.records.FirstOrDefault(r => r.backupPath == backupPath);
        }

        /// <summary>
        /// Tracks any untracked files in the backup folder.
        /// </summary>
        private static void TrackNewBackupFiles()
        {
            if (!AssetDatabase.IsValidFolder(BackupFolder))
                return;

            // Get all assets in the backup folder.
            string[] guidsInFolder = AssetDatabase.FindAssets("", new[] { BackupFolder });
            var existingBackupPaths = new HashSet<string>();

            foreach (var guid in guidsInFolder)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                // Exclude the backup JSON and info file.
                if (!AssetDatabase.IsValidFolder(assetPath) &&
                    assetPath != BackupJsonPath &&
                    assetPath != Path.Combine(BackupFolder, INFO_FILE_NAME))
                {
                    existingBackupPaths.Add(assetPath);
                }
            }

            // Load current backup data.
            BackupData data = LoadBackupData();
            var trackedPaths = data.records.Select(r => r.backupPath).ToHashSet();

            // Find untracked files.
            var untrackedFiles = existingBackupPaths.Except(trackedPaths).ToList();
            if (untrackedFiles.Count > 0)
            {
                foreach (var untrackedFile in untrackedFiles)
                {
                    data.records.Add(new BackupRecord
                    {
                        backupPath = untrackedFile,
                        originalPath = "Unknown Origin",
                        isTracked = false,
                        timestamp = System.DateTime.Now
                    });
                    Debug.Log($"Added untracked file to registry: {untrackedFile}");
                }
                SaveBackupData(data);
            }
        }

        #endregion

        #region Folder Management

        private static void EnsureBackupFolderExists()
        {
            if (!Directory.Exists(AbsoluteBackupFolder))
            {
                AssetDatabase.CreateFolder("Assets", BACKUP_FOLDER_NAME);
                AssetDatabase.Refresh();
            }
        }

        private static void EnsureInfoFileExists()
        {
            if (!File.Exists(InfoFilePath))
            {
                File.WriteAllText(InfoFilePath, INFO_TEXT);
                AssetDatabase.Refresh();
            }
        }

        private static void EnsureDirectoryExists(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Navigates to the backup folder in the Unity Project window by selecting and pinging it.
        /// </summary>
        private static void OpenFolderInProjectWindow(string folderPath)
        {
            EditorUtility.FocusProjectWindow();
            Object folderAsset = AssetDatabase.LoadAssetAtPath<Object>(folderPath);
            if (folderAsset != null)
            {
                Selection.activeObject = folderAsset;
                EditorGUIUtility.PingObject(folderAsset);
            }
            else
            {
                Debug.LogError("Unable to locate the backup folder asset.");
            }
        }

        #endregion

        #region Asset Scanning and Movement

        /// <summary>
        /// Scans the currently selected folders or files in the Project window and returns
        /// a list of asset paths that are not referenced in any of the enabled scenes (as defined in Build Settings).
        /// It filters out the backup JSON and README file so they are not treated as useless.
        /// </summary>
        private static List<string> ScanForUselessAssets()
        {
            var selectedGUIDs = Selection.assetGUIDs;
            var candidateAssets = new List<string>();

            foreach (var guid in selectedGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Skip assets already in the backup folder.
                if (path.StartsWith(BackupFolder))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var folderGUID in AssetDatabase.FindAssets("", new[] { path }))
                    {
                        var assetPath = AssetDatabase.GUIDToAssetPath(folderGUID);
                        if (!AssetDatabase.IsValidFolder(assetPath) && !assetPath.StartsWith(BackupFolder))
                        {
                            candidateAssets.Add(assetPath);
                        }
                    }
                }
                else
                {
                    candidateAssets.Add(path);
                }
            }

            candidateAssets = candidateAssets.Distinct().ToList();

            // Exclude the backup JSON file and the info file.
            string infoFileRelative = Path.Combine(BackupFolder, INFO_FILE_NAME).Replace("\\", "/");
            candidateAssets = candidateAssets.Where(asset => asset != BackupJsonPath && asset != infoFileRelative).ToList();

            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToList();

            if (enabledScenes.Count == 0)
            {
                Debug.LogWarning("No enabled scenes found in Build Settings. Results may not be accurate.");
            }

            var usedAssets = new HashSet<string>();
            foreach (var scenePath in enabledScenes)
            {
                var dependencies = AssetDatabase.GetDependencies(scenePath, true);
                foreach (var dep in dependencies)
                {
                    usedAssets.Add(dep);
                }
            }

            return candidateAssets.Where(asset => !usedAssets.Contains(asset)).ToList();
        }

        /// <summary>
        /// Creates a unique path for an asset in the backup folder to avoid naming conflicts.
        /// </summary>
        private static string GetUniqueBackupPath(string assetPath)
        {
            string fileName = Path.GetFileName(assetPath);
            string newPath = Path.Combine(BackupFolder, fileName).Replace("\\", "/");

            if (AssetDatabase.LoadAssetAtPath<Object>(newPath) != null)
            {
                string baseName = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                int count = 1;

                do
                {
                    newPath = Path.Combine(BackupFolder, $"{baseName}_{count}{ext}").Replace("\\", "/");
                    count++;
                } while (AssetDatabase.LoadAssetAtPath<Object>(newPath) != null);
            }

            return newPath;
        }

        /// <summary>
        /// Moves an asset to its backup location and records the operation.
        /// </summary>
        private static bool MoveAssetToBackup(string assetPath)
        {
            // Double-check not already in the backup folder.
            if (assetPath.StartsWith(BackupFolder))
            {
                Debug.LogWarning($"Cannot move {assetPath} - already in backup folder");
                return false;
            }

            string newPath = GetUniqueBackupPath(assetPath);
            string moveResult = AssetDatabase.MoveAsset(assetPath, newPath);

            if (string.IsNullOrEmpty(moveResult))
            {
                AddBackupRecord(newPath, assetPath);
                Debug.Log($"Moved {assetPath} to backup folder as {newPath}");
                return true;
            }
            else
            {
                Debug.LogError($"Failed to move {assetPath}: {moveResult}");
                return false;
            }
        }


        private static bool IsSystemFile(string assetPath)
        {
            // Compare just the filename portion to our known system files
            string fileName = Path.GetFileName(assetPath);
            return fileName == BACKUP_DATA_FILENAME || fileName == INFO_FILE_NAME;
        }

        /// <summary>
        /// Moves an asset from the backup folder back to its original location.
        /// If the asset is untracked, prompts the user to delete it instead.
        /// </summary>
        private static bool RestoreAssetFromBackup(string backupPath)
        {
            var record = GetBackupRecord(backupPath);

            if (record == null)
            {
                // If the file exists in the backup folder but not in records, try to track it.
                TrackNewBackupFiles();
                record = GetBackupRecord(backupPath);

                if (record == null)
                {
                    Debug.LogError($"No backup record found for {backupPath}");
                    return false;
                }
            }

            if (!record.isTracked)
            {
                EditorUtility.DisplayDialog("Unknown Origin", 
                    $"The file '{Path.GetFileName(backupPath)}' was added manually and has no known original location. " +
                    "It cannot be automatically restored.", "OK");
                return false;
            }


            EnsureDirectoryExists(record.originalPath);

            string moveResult = AssetDatabase.MoveAsset(backupPath, record.originalPath);
            if (string.IsNullOrEmpty(moveResult))
            {
                RemoveBackupRecord(backupPath);
                Debug.Log($"Restored {backupPath} to {record.originalPath}");
                return true;
            }
            else
            {
                Debug.LogError($"Failed to restore {backupPath}: {moveResult}");
                return false;
            }
        }

        #endregion

        #region Menu Commands

        [MenuItem("Assets/✓ Check Useless Assets/↙ Move to Useless", false, 1000)]
        public static void MoveToUselessAssets()
        {
            // Check if all selected assets are already in the backup folder.
            bool allInBackup = true;
            foreach (var guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(BackupFolder))
                {
                    allInBackup = false;
                    break;
                }
            }

            if (allInBackup)
            {
                EditorUtility.DisplayDialog("Check Useless Assets",
                    "All selected assets are already in the backup folder.", "OK");
                return;
            }

            var unusedAssets = ScanForUselessAssets();

            if (unusedAssets.Count == 0)
            {
                EditorUtility.DisplayDialog("Check Useless Assets",
                    "No unused assets found in the selection.", "OK");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog("Check Useless Assets",
                $"Found {unusedAssets.Count} unused asset(s).\n\n" +
                "Do you want to move them to the backup folder?\n" +
                $"(Backup Folder: {BackupFolder})",
                "Yes, move them", "Cancel");

            if (!confirm)
                return;

            EnsureBackupFolderExists();

            int successCount = 0;
            foreach (var assetPath in unusedAssets)
            {
                if (MoveAssetToBackup(assetPath))
                {
                    successCount++;
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Check Useless Assets",
                $"Operation complete. {successCount} of {unusedAssets.Count} assets moved to backup folder.", "OK");
        }

        /// <summary>
        /// Scans the selected assets (using dependency analysis) and selects those that are unused.
        /// This function uses the same scanning logic as "Move to Useless" but does not move files.
        /// </summary>
        [MenuItem("Assets/✓ Check Useless Assets/‣ Select Useless Assets", false, 1002)]
        public static void FindAndSelectUselessAssets()
        {
            var uselessAssets = ScanForUselessAssets();

            if (uselessAssets.Count > 0)
            {
                List<Object> objectsToSelect = new List<Object>();
                foreach (var path in uselessAssets)
                {
                    Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (obj != null)
                    {
                        objectsToSelect.Add(obj);
                    }
                }
                Selection.objects = objectsToSelect.ToArray();
                EditorUtility.DisplayDialog("Find Useless Assets",
                    $"Found and selected {uselessAssets.Count} useless asset(s) based on current dependencies.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Find Useless Assets",
                    "No useless assets found in the current selection.", "OK");
            }
        }

        [MenuItem("Assets/✓ Check Useless Assets/↗ Mark as Useful", false, 1001)]
        public static void MarkAsUseful()
        {
            TrackNewBackupFiles(); // Track any manually added files first.

            int successCount = 0;
            int totalCount = Selection.objects.Length;

            foreach (var obj in Selection.objects)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);

                if (!assetPath.StartsWith(BackupFolder))
                {
                    Debug.LogWarning($"Skipping {assetPath}: Not in backup folder");
                    continue;
                }

                if (RestoreAssetFromBackup(assetPath))
                {
                    successCount++;
                }
            }

            AssetDatabase.Refresh();

            if (totalCount > 0)
            {
                EditorUtility.DisplayDialog("↗ Mark as Useful",
                    $"Operation complete. {successCount} of {totalCount} assets processed.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("↗ Mark as Useful",
                    "No assets selected. Please select assets from the backup folder to restore.", "OK");
            }
        }

        [MenuItem("Assets/✓ Check Useless Assets/⌂ Navigate to Useless Assets Folder", false, 1003)]
        public static void NavigateToUselessAssetsFolder()
        {
            EnsureBackupFolderExists();
            EnsureInfoFileExists();
            TrackNewBackupFiles(); // Track any manually added files.

            // Simply select and ping the backup folder in the Project window.
            OpenFolderInProjectWindow(BackupFolder);
        }

        [MenuItem("Assets/✓ Check Useless Assets/⌁ View Statistics", false, 1004)]
        public static void ViewUselessAssetsStatistics()
        {
            TrackNewBackupFiles(); // Track any manually added files first.

            BackupData data = LoadBackupData();

            // Exclude the JSON backup file and the README from the statistics
            var relevantRecords = data.records
                .Where(r => !IsSystemFile(r.backupPath))
                .ToList();

            // Group assets by extension
            var byExtension = relevantRecords
                .GroupBy(r => Path.GetExtension(r.backupPath).ToLower())
                .ToDictionary(g => g.Key, g => g.Count());

            // Count untracked files
            int untrackedCount = relevantRecords.Count(r => !r.isTracked);

            // Calculate total size
            long totalSize = 0;
            foreach (var record in relevantRecords)
            {
                string fullPath = Path.Combine(Application.dataPath, "..", record.backupPath);
                if (File.Exists(fullPath))
                {
                    totalSize += new FileInfo(fullPath).Length;
                }
            }

            // Build message
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"📊 Total unused assets: {relevantRecords.Count}");
            sb.AppendLine($"💾 Total size: {FormatSize(totalSize)}");

            if (untrackedCount > 0)
            {
                sb.AppendLine($"⚠️ Untracked files (unknown origin): {untrackedCount}");
            }

            sb.AppendLine("\n📋 Breakdown by file type:");
            foreach (var ext in byExtension.OrderByDescending(kv => kv.Value))
            {
                string extension = string.IsNullOrEmpty(ext.Key) ? "(no extension)" : ext.Key;
                sb.AppendLine($"  {extension}: {ext.Value} file(s)");
            }

            EditorUtility.DisplayDialog("Useless Assets Statistics", sb.ToString(), "OK");
        }
        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        #endregion

        #region Menu Validation

        [MenuItem("Assets/✓ Check Useless Assets/↙ Move to Useless", true)]
        private static bool ValidateMoveToUseless()
        {
            // Check if any selected asset is not already in the backup folder.
            foreach (var guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(BackupFolder))
                {
                    return true; // At least one asset is not in backup folder.
                }
            }
            return false; // All selected assets are already in backup folder.
        }

        [MenuItem("Assets/✓ Check Useless Assets/↗ Mark as Useful", true)]
        private static bool ValidateMarkAsUseful()
        {
            // Valid only if at least one selected asset is in the backup folder.
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (path.StartsWith(BackupFolder))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion
    }
}
