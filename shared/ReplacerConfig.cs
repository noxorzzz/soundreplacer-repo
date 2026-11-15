using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SoundReplacer.Shared
{
    /// <summary>
    /// Модель config.json — единственный контракт между приложением и модом.
    ///
    /// {
    ///   "version": 1,
    ///   "enabled": true,
    ///   "patchClipSetter": true,
    ///   "exportCatalog": true,
    ///   "sounds": {
    ///     "originalSoundName": "Sounds/my_file.ogg"
    ///   }
    /// }
    ///
    /// Пути в "sounds" могут быть относительными (от папки мода) или абсолютными.
    /// Приложение всегда пишет относительные пути вида "Sounds/...".
    /// </summary>
    public sealed class ReplacerConfig
    {
        public const int CurrentVersion = 1;
        public const string FileName = "config.json";

        public int Version { get; set; }
        public bool Enabled { get; set; }
        public bool PatchClipSetter { get; set; }
        public bool ExportCatalog { get; set; }

        /// <summary>Обрезать файл до длины оригинала (иначе звук «висит» дольше события).</summary>
        public bool MatchLength { get; set; }

        /// <summary>Приводить пик файла к пику оригинального клипа, чтобы громкость не скакала.</summary>
        public bool MatchVolume { get; set; }

        /// <summary>Сводить стерео в моно, если оригинал моно: иначе 3D-панорама работает криво.</summary>
        public bool MatchChannels { get; set; }

        /// <summary>Писать в лог каждый проигранный клип (имя и длину) — для поиска нужного звука.</summary>
        public bool LogPlays { get; set; }

        /// <summary>
        /// Имена, которые НЕ обрезаются по длине оригинала (треки вместо коротких джинглов).
        /// Пример: ["extraction point jingle complete 2 local"].
        /// </summary>
        public List<string> NoTrim { get; private set; }

        /// <summary>
        /// Не запускать тот же файл повторно в течение <see cref="OnceWindowSeconds"/>:
        /// игра иногда проигрывает один джингл несколько раз подряд (local/global, 3 гудка),
        /// и длинный трек начинался бы трижды.
        /// </summary>
        public bool OncePerFile { get; set; }

        public double OnceWindowSeconds { get; set; }

        /// <summary>Плавное затухание в конце заменённого клипа, секунды (0 — выключено).</summary>
        public double FadeOutSeconds { get; set; }

        /// <summary>Имена, для которых трек играет на отдельном 3D-источнике (с физикой расстояния).</summary>
        public List<string> Spatial { get; private set; }

        public double MinDistance { get; set; }

        public double MaxDistance { get; set; }

        public double TrackVolume { get; set; }

        /// <summary>Эхо для 3D-трека (AudioEchoFilter на собственном источнике).</summary>
        public bool Echo { get; set; }

        /// <summary>Задержка эха, миллисекунды (10–5000).</summary>
        public double EchoDelayMs { get; set; }

        /// <summary>Сколько затухает каждое повторение (0–0.9).</summary>
        public double EchoDecayRatio { get; set; }

        /// <summary>Доля «мокрого» (эхового) сигнала (0–1).</summary>
        public double EchoWetMix { get; set; }

        /// <summary>Доля «сухого» сигнала (0–1).</summary>
        public double EchoDryMix { get; set; }

        /// <summary>Срез высоких частот, Гц (0 — выключено). У игры звук глухой, без «стекла».</summary>
        public double LowPassHz { get; set; }

        /// <summary>Пресет реверберации Unity: Room, Hall, Cave, Off и т.д. Пусто — выключено.</summary>
        public string ReverbPreset { get; set; }

        public Dictionary<string, string> Sounds { get; private set; }

        public ReplacerConfig()
        {
            Version = CurrentVersion;
            Enabled = true;
            PatchClipSetter = true;
            ExportCatalog = true;
            MatchLength = true;
            MatchVolume = true;
            MatchChannels = true;
            LogPlays = false;
            NoTrim = new List<string>();
            OncePerFile = false;
            OnceWindowSeconds = 2.0;
            FadeOutSeconds = 0.0;
            Spatial = new List<string>();
            MinDistance = 3.0;
            MaxDistance = 40.0;
            TrackVolume = 1.0;
            Echo = false;
            EchoDelayMs = 500.0;
            EchoDecayRatio = 0.5;
            EchoWetMix = 0.5;
            EchoDryMix = 1.0;
            LowPassHz = 0.0;
            ReverbPreset = string.Empty;
            Sounds = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public string ToJson()
        {
            var root = new Dictionary<string, object>(StringComparer.Ordinal);
            root["version"] = Version;
            root["enabled"] = Enabled;
            root["patchClipSetter"] = PatchClipSetter;
            root["exportCatalog"] = ExportCatalog;
            root["matchLength"] = MatchLength;
            root["matchVolume"] = MatchVolume;
            root["matchChannels"] = MatchChannels;
            root["logPlays"] = LogPlays;
            root["oncePerFile"] = OncePerFile;
            root["onceWindowSeconds"] = OnceWindowSeconds;
            root["fadeOutSeconds"] = FadeOutSeconds;
            root["spatial"] = new List<object>(Spatial.Count);
            foreach (string name in Spatial) ((List<object>)root["spatial"]).Add(name);
            root["minDistance"] = MinDistance;
            root["maxDistance"] = MaxDistance;
            root["trackVolume"] = TrackVolume;
            root["echo"] = Echo;
            root["echoDelayMs"] = EchoDelayMs;
            root["echoDecayRatio"] = EchoDecayRatio;
            root["echoWetMix"] = EchoWetMix;
            root["echoDryMix"] = EchoDryMix;
            root["lowPassHz"] = LowPassHz;
            root["reverbPreset"] = ReverbPreset;

            var noTrim = new List<object>(NoTrim.Count);
            foreach (string name in NoTrim) noTrim.Add(name);
            root["noTrim"] = noTrim;

            var sounds = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in Sounds.OrderBy(p => p.Key, StringComparer.Ordinal))
                sounds[pair.Key] = pair.Value;
            root["sounds"] = sounds;

            return Json.Serialize(root, true);
        }

        /// <summary>Разбор JSON. Возвращает null и текст ошибки, если документ некорректен.</summary>
        public static ReplacerConfig FromJson(string json, out string error)
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
                error = "корень config.json должен быть JSON-объектом";
                return null;
            }

            var config = new ReplacerConfig();
            config.Version = Json.GetInt(obj, "version", CurrentVersion);
            config.Enabled = Json.GetBool(obj, "enabled", true);
            config.PatchClipSetter = Json.GetBool(obj, "patchClipSetter", true);
            config.ExportCatalog = Json.GetBool(obj, "exportCatalog", true);
            config.MatchLength = Json.GetBool(obj, "matchLength", true);
            config.MatchVolume = Json.GetBool(obj, "matchVolume", true);
            config.MatchChannels = Json.GetBool(obj, "matchChannels", true);
            config.LogPlays = Json.GetBool(obj, "logPlays", false);
            config.OncePerFile = Json.GetBool(obj, "oncePerFile", false);
            config.OnceWindowSeconds = Json.GetDouble(obj, "onceWindowSeconds", 2.0);
            config.FadeOutSeconds = Json.GetDouble(obj, "fadeOutSeconds", 0.0);
            config.MinDistance = Json.GetDouble(obj, "minDistance", 3.0);
            config.MaxDistance = Json.GetDouble(obj, "maxDistance", 40.0);
            config.TrackVolume = Json.GetDouble(obj, "trackVolume", 1.0);
            config.Echo = Json.GetBool(obj, "echo", false);
            config.EchoDelayMs = Json.GetDouble(obj, "echoDelayMs", 500.0);
            config.EchoDecayRatio = Json.GetDouble(obj, "echoDecayRatio", 0.5);
            config.EchoWetMix = Json.GetDouble(obj, "echoWetMix", 0.5);
            config.EchoDryMix = Json.GetDouble(obj, "echoDryMix", 1.0);
            config.LowPassHz = Json.GetDouble(obj, "lowPassHz", 0.0);
            config.ReverbPreset = (Json.Get(obj, "reverbPreset") as string ?? string.Empty).Trim();

            var rawNoTrim = Json.AsArray(Json.Get(obj, "noTrim"));
            if (rawNoTrim != null)
            {
                foreach (object item in rawNoTrim)
                {
                    var name = item as string;
                    if (!string.IsNullOrEmpty(name)) config.NoTrim.Add(name.Trim());
                }
            }

            var rawSpatial = Json.AsArray(Json.Get(obj, "spatial"));
            if (rawSpatial != null)
            {
                foreach (object item in rawSpatial)
                {
                    var name = item as string;
                    if (!string.IsNullOrEmpty(name)) config.Spatial.Add(name.Trim());
                }
            }

            object rawSounds = Json.Get(obj, "sounds");
            if (rawSounds != null)
            {
                var sounds = Json.AsObject(rawSounds);
                if (sounds == null)
                {
                    error = "поле \"sounds\" должно быть объектом { \"имя\": \"путь\" }";
                    return null;
                }

                foreach (KeyValuePair<string, object> pair in sounds)
                {
                    string original = pair.Key == null ? null : pair.Key.Trim();
                    string path = pair.Value as string;
                    if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(path)) continue;
                    config.Sounds[original] = path.Trim();
                }
            }

            if (config.Version > CurrentVersion)
            {
                // Не отказываемся от загрузки: новые поля просто игнорируются.
                error = null;
            }

            return config;
        }

        public static bool TryLoad(string path, out ReplacerConfig config, out string error)
        {
            config = null;
            error = null;

            if (!File.Exists(path))
            {
                error = "файл не найден: " + path;
                return false;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                config = FromJson(json, out error);
                return config != null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Атомарная запись: временный файл + подмена, чтобы мод никогда не прочитал половину конфига.</summary>
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

        /// <summary>Разрешает путь из конфига: относительный — от папки мода, абсолютный — как есть.</summary>
        public static bool TryResolvePath(string pluginDirectory, string configValue, out string absolutePath, out string error)
        {
            absolutePath = null;
            error = null;

            if (string.IsNullOrEmpty(configValue))
            {
                error = "пустой путь";
                return false;
            }

            try
            {
                string normalized = configValue.Trim()
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);

                absolutePath = Path.IsPathRooted(normalized)
                    ? Path.GetFullPath(normalized)
                    : Path.GetFullPath(Path.Combine(pluginDirectory, normalized));

                if (!File.Exists(absolutePath))
                {
                    error = "файл не найден: " + absolutePath;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public string Describe()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "version={0}, enabled={1}, patchClipSetter={2}, matchLength={3}, matchVolume={4}, matchChannels={5}, logPlays={6}, noTrim={7}, oncePerFile={8}, fadeOut={9}, spatial={10}, sounds={11}",
                Version, Enabled, PatchClipSetter, MatchLength, MatchVolume, MatchChannels, LogPlays, NoTrim.Count, OncePerFile, FadeOutSeconds, Spatial.Count, Sounds.Count);
        }
    }
}