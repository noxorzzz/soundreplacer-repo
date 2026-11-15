using System;
using System.Diagnostics;
using System.IO;
using SoundReplacer.Shared;

namespace SoundReplacer.App.Services
{
    /// <summary>
    /// Все пути согласованы с модом: мод читает config.json из своей папки
    /// (рядом с SoundReplacer.dll), приложение пишет туда же.
    ///
    /// Папка мода не всегда BepInEx/plugins/SoundReplacer: r2modman и Thunderstore
    /// ставят пакет в свою подпапку, поэтому DLL ищется по всем каталогам плагинов.
    /// </summary>
    public sealed class ModPaths
    {
        public const string PluginFolderName = "SoundReplacer";
        public const string SoundsFolderName = "Sounds";
        public const string ModAssemblyName = "SoundReplacer.dll";

        private readonly string _pluginDirectory;

        public ModPaths(string gameDirectory)
        {
            if (string.IsNullOrWhiteSpace(gameDirectory))
                throw new ArgumentException("Не указана папка игры", nameof(gameDirectory));

            GameDirectory = Path.GetFullPath(gameDirectory);
            _pluginDirectory = ResolvePluginDirectory();
        }

        public string GameDirectory { get; private set; }

        public string BepInExDirectory
        {
            get { return Path.Combine(GameDirectory, "BepInEx"); }
        }

        public string PluginsDirectory
        {
            get { return Path.Combine(BepInExDirectory, "plugins"); }
        }

        /// <summary>Папка, где лежит SoundReplacer.dll (или куда он будет положен).</summary>
        public string PluginDirectory
        {
            get { return _pluginDirectory; }
        }

        public bool IsDefaultPluginDirectory
        {
            get
            {
                return string.Equals(
                    Path.GetFullPath(PluginDirectory),
                    Path.GetFullPath(Path.Combine(PluginsDirectory, PluginFolderName)),
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        public string ConfigPath
        {
            get { return Path.Combine(PluginDirectory, ReplacerConfig.FileName); }
        }

        public string BackupConfigPath
        {
            get { return Path.Combine(PluginDirectory, "config.previous.json"); }
        }

        public string SoundsDirectory
        {
            get { return Path.Combine(PluginDirectory, SoundsFolderName); }
        }

        public string CatalogPath
        {
            get { return Path.Combine(PluginDirectory, SoundCatalog.FileName); }
        }

        public string ModLogPath
        {
            get { return Path.Combine(PluginDirectory, "SoundReplacer.log"); }
        }

        public bool BepInExInstalled
        {
            get { return Directory.Exists(BepInExDirectory); }
        }

        public bool ModInstalled
        {
            get { return File.Exists(Path.Combine(PluginDirectory, ModAssemblyName)); }
        }

        public void EnsureDirectories()
        {
            Directory.CreateDirectory(PluginDirectory);
            Directory.CreateDirectory(SoundsDirectory);
        }

        public void OpenPluginFolder()
        {
            EnsureDirectories();
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + PluginDirectory + "\"",
                UseShellExecute = true
            });
        }

        private string ResolvePluginDirectory()
        {
            string defaultDirectory = Path.Combine(PluginsDirectory, PluginFolderName);

            if (!Directory.Exists(PluginsDirectory))
                return defaultDirectory;

            if (File.Exists(Path.Combine(defaultDirectory, ModAssemblyName)))
                return defaultDirectory;

            // Thunderstore / r2modman: BepInEx/plugins/<Team>-<Package>/SoundReplacer.dll
            try
            {
                string[] found = Directory.GetFiles(PluginsDirectory, ModAssemblyName, SearchOption.AllDirectories);
                foreach (string dll in found)
                {
                    string directory = Path.GetDirectoryName(dll);
                    if (string.IsNullOrEmpty(directory)) continue;

                    string relative = directory.Substring(PluginsDirectory.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (relative.Length == 0) continue;
                    if (relative.Split(Path.DirectorySeparatorChar).Length <= 3) return directory;
                }
            }
            catch (IOException)
            {
                // нет доступа к части папок — остаёмся на пути по умолчанию
            }
            catch (UnauthorizedAccessException)
            {
            }

            return defaultDirectory;
        }
    }
}