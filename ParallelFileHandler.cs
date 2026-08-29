using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MurkysManySaves
{
    /// <summary>
    /// Reads and writes values in a parallel ES3 file under a per-namespace MMS subfolder,
    /// never touching the player's real save file.
    /// Example: SaveValue("save_5.es3", "myfeature", "isEnabled", true) writes
    /// "MMS/myfeature/save_5_myfeature.es3".
    /// </summary>
    public static class ParallelFileHandler
    {
        private const string RootFolder = "MMS";

        /// <summary>Creates the top-level MMS folder on mod init, so it exists before the first save.</summary>
        public static void EnsureRootFolder()
        {
            try
            {
                System.IO.Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, RootFolder));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MurkysManySaves] Failed to create MMS root folder: {ex.Message}");
            }
        }

        public static void SaveValue<T>(string saveFile, string ns, string key, T value)
        {
            try
            {
                EnsureMigrated(saveFile, ns);
                string path = BuildParallelPath(saveFile, ns);
                System.IO.Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, RootFolder, ns));
                Type es3Type = Es3ReflectionHandler.FindEs3Type();
                MethodInfo save = es3Type != null
                    ? Es3ReflectionHandler.FindMethod(es3Type, "Save", typeof(string), typeof(object), typeof(string))
                    : null;

                if (save == null)
                {
                    Debug.LogWarning("[MurkysManySaves] ES3.Save not found - cannot save value");
                    return;
                }

                Es3ReflectionHandler.Invoke(save, null, key, value, path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MurkysManySaves] Failed to save '{key}' to {saveFile}: {ex.Message}");
            }
        }

        public static T LoadValue<T>(string saveFile, string ns, string key, T defaultValue)
        {
            try
            {
                EnsureMigrated(saveFile, ns);
                string path = BuildParallelPath(saveFile, ns);
                Type es3Type = Es3ReflectionHandler.FindEs3Type();
                if (es3Type == null || !FileExists(saveFile, ns) || !KeyExists(saveFile, ns, key))
                    return defaultValue;

                MethodInfo load = Es3ReflectionHandler.FindMethod(es3Type, "Load", typeof(string), typeof(string), typeof(T));
                if (load == null)
                    return defaultValue;

                return (T)Es3ReflectionHandler.Invoke(load, null, key, path, defaultValue);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MurkysManySaves] Failed to load '{key}' from {saveFile}: {ex.Message}");
                return defaultValue;
            }
        }

        public static bool KeyExists(string saveFile, string ns, string key)
        {
            try
            {
                EnsureMigrated(saveFile, ns);
                Type es3Type = Es3ReflectionHandler.FindEs3Type();
                MethodInfo method = es3Type != null ? Es3ReflectionHandler.FindMethod(es3Type, "KeyExists", typeof(string), typeof(string)) : null;
                if (method == null)
                    return false;

                return (bool)Es3ReflectionHandler.Invoke(method, null, key, BuildParallelPath(saveFile, ns));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MurkysManySaves] Failed to check key '{key}' in {saveFile}: {ex.Message}");
                return false;
            }
        }

        public static bool FileExists(string saveFile, string ns)
        {
            try
            {
                EnsureMigrated(saveFile, ns);
                Type es3Type = Es3ReflectionHandler.FindEs3Type();
                MethodInfo method = es3Type != null ? Es3ReflectionHandler.FindMethod(es3Type, "FileExists", typeof(string)) : null;
                if (method == null)
                    return false;

                return (bool)Es3ReflectionHandler.Invoke(method, null, BuildParallelPath(saveFile, ns));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MurkysManySaves] Failed to check existence of parallel file for {saveFile}: {ex.Message}");
                return false;
            }
        }

        public static void DeleteFile(string saveFile, string ns)
        {
            try
            {
                EnsureMigrated(saveFile, ns);
                if (!FileExists(saveFile, ns))
                    return;

                Type es3Type = Es3ReflectionHandler.FindEs3Type();
                MethodInfo method = es3Type != null ? Es3ReflectionHandler.FindMethod(es3Type, "DeleteFile", typeof(string)) : null;
                if (method == null)
                    return;

                Es3ReflectionHandler.Invoke(method, null, BuildParallelPath(saveFile, ns));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MurkysManySaves] Failed to delete parallel file for {saveFile}: {ex.Message}");
            }
        }

        private static string BuildLegacyPath(string saveFile, string ns)
        {
            return saveFile.EndsWith(".es3", StringComparison.OrdinalIgnoreCase)
                ? saveFile.Substring(0, saveFile.Length - 4) + $"_{ns}.es3"
                : $"{saveFile}_{ns}";
        }

        private static string BuildParallelPath(string saveFile, string ns)
        {
            return $"{RootFolder}/{ns}/{BuildLegacyPath(saveFile, ns)}";
        }

        /// <summary>Moves a pre-update flat parallel file into its new MMS/{ns} subfolder, if one exists. No-op otherwise.</summary>
        private static void EnsureMigrated(string saveFile, string ns)
        {
            try
            {
                string newAbs = Path.Combine(Application.persistentDataPath, RootFolder, ns, BuildLegacyPath(saveFile, ns));
                if (File.Exists(newAbs))
                    return;

                string legacyAbs = Path.Combine(Application.persistentDataPath, BuildLegacyPath(saveFile, ns));
                if (!File.Exists(legacyAbs))
                    return;

                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(newAbs));
                File.Move(legacyAbs, newAbs);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MurkysManySaves] Failed to migrate legacy parallel file for {saveFile}: {ex.Message}");
            }
        }
    }
}
