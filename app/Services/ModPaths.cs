using System;
using System.Diagnostics;
using System.IO;
using SoundReplacer.Shared;

namespace SoundReplacer.App.Services
{
    /// <summary>
    /// Все пути согласованы с модом: мод читает config.json из своей папки
    /// (BepInEx/plugins/SoundReplacer), приложение пишет туда же.
    /// </summary>
    public sealed class ModPaths
    {
        public const string PluginFolderName = "SoundReplacer";
        public const string SoundsFolderName = "Sounds";

        public ModPaths(string gameDirectory)
        {
            if (string.IsNullOrWhiteSpace(gameDirectory))
                throw new ArgumentException("Не указана папка игры", nameof(gameDirectory));

            GameDirectory = Path.GetFullPath(gameDirectory);
        }

        public string GameDirectory { get; private set; }

        public string BepInExDirectory
        {
            get { return Path.Combine(GameDirectory, "BepInEx"); }
        }

        public string PluginDirectory
        {
            get { return Path.Combine(BepInExDirectory, "plugins", PluginFolderName); }
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
            get { return File.Exists(Path.Combine(PluginDirectory, "SoundReplacer.dll")); }
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
    }
}