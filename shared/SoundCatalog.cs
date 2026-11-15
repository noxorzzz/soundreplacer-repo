using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SoundReplacer.Shared
{
    /// <summary>
    /// catalog.json — «витрина» звуков, которую мод собирает во время игры,
    /// а приложение читает, чтобы показать список доступных звуков.
    ///
    /// Почему каталог собирает мод, а не приложение: имена AudioClip живут внутри
    /// ресурсов Unity, снаружи игры их не получить без распаковки бандлов.
    /// Мод видит каждый проигранный клип и складывает имена в этот файл.
    /// </summary>
    public sealed class SoundCatalog
    {
        public const int CurrentVersion = 1;
        public const string FileName = "catalog.json";

        public int Version { get; set; }
        public string GeneratedUtc { get; set; }
        public string GameVersion { get; set; }
        public string UnityVersion { get; set; }
        public string ModVersion { get; set; }
        public List<SoundCatalogEntry> Sounds { get; private set; }

        public SoundCatalog()
        {
            Version = CurrentVersion;
            Sounds = new List<SoundCatalogEntry>();
        }

        public string ToJson()
        {
            var root = new Dictionary<string, object>(StringComparer.Ordinal);
            root["version"] = Version;
            root["generatedUtc"] = GeneratedUtc ?? DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            root["gameVersion"] = GameVersion ?? string.Empty;
            root["unityVersion"] = UnityVersion ?? string.Empty;
            root["modVersion"] = ModVersion ?? string.Empty;

            var sounds = new List<object>(Sounds.Count);
            foreach (SoundCatalogEntry entry in Sounds)
            {
                var item = new Dictionary<string, object>(StringComparer.Ordinal);
                item["name"] = entry.Name;
                item["plays"] = entry.Plays;
                item["length"] = entry.Length;
                item["channels"] = entry.Channels;
                item["lastSeenUtc"] = entry.LastSeenUtc ?? string.Empty;
                sounds.Add(item);
            }

            root["sounds"] = sounds;
            return Json.Serialize(root, true);
        }

        public static SoundCatalog FromJson(string json, out string error)
        {
            error = null;

            object root;
            string parseError;
            if (!Json.TryParse(json, out root, out parseError))
            {
                error = parseError;
                return null;
            }

            var obj = Json.AsObject(root);
            if (obj == null)
            {
                error = "корень catalog.json должен быть JSON-объектом";
                return null;
            }

            var catalog = new SoundCatalog();
            catalog.Version = Json.GetInt(obj, "version", CurrentVersion);
            catalog.GeneratedUtc = Json.GetString(obj, "generatedUtc", string.Empty);
            catalog.GameVersion = Json.GetString(obj, "gameVersion", string.Empty);
            catalog.UnityVersion = Json.GetString(obj, "unityVersion", string.Empty);
            catalog.ModVersion = Json.GetString(obj, "modVersion", string.Empty);

            var rawSounds = Json.AsArray(Json.Get(obj, "sounds"));
            if (rawSounds != null)
            {
                foreach (object raw in rawSounds)
                {
                    var item = Json.AsObject(raw);
                    if (item == null) continue;

                    string name = Json.GetString(item, "name");
                    if (string.IsNullOrEmpty(name)) continue;

                    catalog.Sounds.Add(new SoundCatalogEntry
                    {
                        Name = name,
                        Plays = Json.GetInt(item, "plays", 0),
                        Length = (float)Json.GetDouble(item, "length", 0.0),
                        Channels = Json.GetInt(item, "channels", 0),
                        LastSeenUtc = Json.GetString(item, "lastSeenUtc", string.Empty)
                    });
                }
            }

            catalog.Sounds = catalog.Sounds
                .OrderByDescending(s => s.Plays)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return catalog;
        }

        public static bool TryLoad(string path, out SoundCatalog catalog, out string error)
        {
            catalog = null;
            error = null;

            if (!File.Exists(path))
            {
                error = "файл не найден: " + path;
                return false;
            }

            try
            {
                catalog = FromJson(File.ReadAllText(path, Encoding.UTF8), out error);
                return catalog != null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool Save(string path, out string error)
        {
            error = null;
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                string temp = path + ".tmp";
                File.WriteAllText(temp, ToJson(), new UTF8Encoding(false));

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temp, path, null);
                    }
                    catch (Exception)
                    {
                        File.Delete(path);
                        File.Move(temp, path);
                    }
                }
                else
                {
                    File.Move(temp, path);
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

    public sealed class SoundCatalogEntry
    {
        public string Name { get; set; }
        public int Plays { get; set; }

        /// <summary>Длительность оригинала в секундах (0 — неизвестна: старый каталог).</summary>
        public float Length { get; set; }

        /// <summary>Каналов в оригинале: 1 (моно, 3D) или 2 (стерео, музыка/UI).</summary>
        public int Channels { get; set; }

        public string LastSeenUtc { get; set; }
    }
}