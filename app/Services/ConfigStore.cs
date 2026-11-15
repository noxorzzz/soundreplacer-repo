using System;
using System.IO;
using System.Text;
using SoundReplacer.Shared;

namespace SoundReplacer.App.Services
{
    /// <summary>Чтение/запись config.json. Пишем атомарно и оставляем копию предыдущей версии.</summary>
    public static class ConfigStore
    {
        public static ReplacerConfig Load(string path, out string error)
        {
            error = null;

            try
            {
                if (!File.Exists(path))
                    return new ReplacerConfig();

                string json = File.ReadAllText(path, Encoding.UTF8);
                ReplacerConfig config = ReplacerConfig.FromJson(json, out error);
                if (config == null)
                {
                    error = "config.json повреждён: " + error;
                    return new ReplacerConfig();
                }

                return config;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return new ReplacerConfig();
            }
        }

        public static bool Save(string path, ReplacerConfig config, string backupPath, out string error)
        {
            error = null;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                if (File.Exists(path) && !string.IsNullOrEmpty(backupPath))
                {
                    try { File.Copy(path, backupPath, true); }
                    catch { /* бэкап не критичен */ }
                }

                return config != null && config.Save(path, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}