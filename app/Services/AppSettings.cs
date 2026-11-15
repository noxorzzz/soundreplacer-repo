using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SoundReplacer.Shared;

namespace SoundReplacer.App.Services
{
    /// <summary>Настройки самого приложения (не мода): где лежит игра и как искать процесс.</summary>
    public sealed class AppSettings
    {
        public string GameDirectory { get; set; }
        public List<string> ProcessNames { get; set; }
        public bool WarnWhenGameRunning { get; set; }

        public AppSettings()
        {
            ProcessNames = new List<string> { "REPO", "REPO_IL2CPP" };
            WarnWhenGameRunning = true;
        }

        public static string DirectoryPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SoundReplacer");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(DirectoryPath, "settings.json"); }
        }

        public static AppSettings Load(out string error)
        {
            error = null;
            var settings = new AppSettings();

            try
            {
                if (!File.Exists(FilePath)) return settings;

                object root;
                string parseError;
                if (!Json.TryParse(File.ReadAllText(FilePath, Encoding.UTF8), out root, out parseError))
                {
                    error = parseError;
                    return settings;
                }

                var obj = Json.AsObject(root);
                if (obj == null) return settings;

                settings.GameDirectory = Json.GetString(obj, "gameDirectory", null);
                settings.WarnWhenGameRunning = Json.GetBool(obj, "warnWhenGameRunning", true);

                var names = Json.AsArray(Json.Get(obj, "processNames"));
                if (names != null)
                {
                    var parsed = new List<string>();
                    foreach (object item in names)
                    {
                        string name = item as string;
                        if (!string.IsNullOrWhiteSpace(name)) parsed.Add(name.Trim());
                    }
                    if (parsed.Count > 0) settings.ProcessNames = parsed;
                }

                return settings;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return settings;
            }
        }

        public bool Save(out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(DirectoryPath);

                var root = new Dictionary<string, object>(StringComparer.Ordinal);
                root["gameDirectory"] = GameDirectory ?? string.Empty;
                root["warnWhenGameRunning"] = WarnWhenGameRunning;
                root["processNames"] = ProcessNames ?? new List<string>();

                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, Json.Serialize(root, true), new UTF8Encoding(false));

                if (File.Exists(FilePath))
                {
                    try
                    {
                        File.Replace(temp, FilePath, null);
                    }
                    catch (Exception)
                    {
                        File.Delete(FilePath);
                        File.Move(temp, FilePath);
                    }
                }
                else
                {
                    File.Move(temp, FilePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}