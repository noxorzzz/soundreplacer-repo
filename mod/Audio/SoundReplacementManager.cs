using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SoundReplacer.Logging;
using SoundReplacer.Shared;
using UnityEngine;
using UnityEngine.Networking;

namespace SoundReplacer.Audio
{
    /// <summary>
    /// Хранит соответствие «имя оригинального AudioClip -> загруженный AudioClip»
    /// и умеет загружать файлы из config.json.
    ///
    /// Вся загрузка идёт корутиной: .ogg декодирует Unity (нужен кадр на файл),
    /// .wav декодируется синхронно, но между файлами мы отдаём кадр, чтобы не
    /// подвешивать игру при большом конфиге.
    ///
    /// Ни одна ошибка файла не является фатальной: неудачный маппинг пропускается,
    /// оригинальный звук остаётся, в лог уходит предупреждение.
    /// </summary>
    public sealed class SoundReplacementManager
    {
        private readonly ReplacerConfig _config;
        private readonly string _pluginDirectory;
        private readonly Dictionary<string, AudioClip> _replacements = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _sources = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _failures = new List<string>();
        private readonly HashSet<string> _fitted = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<AudioClip> _owned = new List<AudioClip>();
        private readonly Dictionary<string, float> _lastStartBySource = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private AudioClip _silent;
        private readonly object _sync = new object();

        public SoundReplacementManager(ReplacerConfig config, string pluginDirectory)
        {
            _config = config ?? new ReplacerConfig();
            _pluginDirectory = pluginDirectory;
        }

        public bool Enabled
        {
            get { return _config.Enabled; }
        }

        public bool PatchClipSetter
        {
            get { return _config.PatchClipSetter; }
        }

        public bool LogPlays
        {
            get { return _config.LogPlays; }
        }

        public bool IsNoTrim(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (string item in _config.NoTrim)
            {
                if (string.Equals(item, name, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        public int Count
        {
            get { lock (_sync) return _replacements.Count; }
        }

        public IList<string> Failures
        {
            get { lock (_sync) return _failures.ToArray(); }
        }

        /// <summary>Вызывается из Harmony-патчей на каждый старт проигрывания.</summary>
        public bool TryGet(AudioClip original, out AudioClip replacement)
        {
            replacement = null;
            if (original == null || !_config.Enabled) return false;

            string name = original.name;
            if (string.IsNullOrEmpty(name)) return false;

            lock (_sync)
            {
                AudioClip found;
                if (!_replacements.TryGetValue(name, out found) || found == null) return false;
                if (ReferenceEquals(found, original)) return false;

                // Первое реальное проигрывание — единственный момент, когда у нас есть
                // и оригинал, и файл: подгоняем длину/громкость/каналы один раз и кешируем.
                if (!_fitted.Contains(name))
                {
                    _fitted.Add(name);

                    AudioClip fitted = ClipFitter.Fit(
                        original,
                        found,
                        _config.MatchLength && !IsNoTrim(name),
                        _config.MatchVolume,
                        _config.MatchChannels);

                    if (fitted != null)
                    {
                        _owned.Add(fitted);
                        _replacements[name] = fitted;
                        found = fitted;
                    }
                }

                if (ShouldSuppress(name))
                {
                    replacement = Silent;
                    return true;
                }

                replacement = found;
                return true;
            }
        }

        /// <summary>
        /// Игра иногда стартует один и тот же джингл несколько раз подряд
        /// (local + global, три гудка). Для длинного трека это три наложения —
        /// поэтому второй и следующие старты в окне глушим.
        /// </summary>
        private bool ShouldSuppress(string name)
        {
            if (!_config.OncePerFile) return false;

            string source;
            if (!_sources.TryGetValue(name, out source) || string.IsNullOrEmpty(source)) return false;

            float now = Time.unscaledTime;
            float last;
            if (_lastStartBySource.TryGetValue(source, out last) &&
                now - last < (float)_config.OnceWindowSeconds)
            {
                ModLog.Info("повторный старт '" + name + "' заглушён (окно " +
                            _config.OnceWindowSeconds.ToString("0.0") + " c)");
                return true;
            }

            _lastStartBySource[source] = now;
            return false;
        }

        private AudioClip Silent
        {
            get
            {
                if (_silent != null) return _silent;

                const int frames = 4410; // 0.1 c при 44.1 кГц
                _silent = AudioClip.Create("SR_silent", frames, 1, 44100, false);
                _silent.SetData(new float[frames], 0);
                _owned.Add(_silent);
                return _silent;
            }
        }

        public bool TryGetByName(string originalName, out AudioClip replacement)
        {
            replacement = null;
            if (string.IsNullOrEmpty(originalName) || !_config.Enabled) return false;

            lock (_sync)
            {
                AudioClip found;
                if (!_replacements.TryGetValue(originalName, out found) || found == null) return false;

                if (ShouldSuppress(originalName))
                {
                    replacement = Silent;
                    return true;
                }

                replacement = found;
                return true;
            }
        }

        public IEnumerator LoadAllCoroutine()
        {
            lock (_sync)
            {
                _replacements.Clear();
                _sources.Clear();
                _failures.Clear();
                _fitted.Clear();
                _owned.Clear();
                _lastStartBySource.Clear();
                _silent = null;
            }

            if (!_config.Enabled)
            {
                ModLog.Warn("замена звуков отключена в config.json (enabled = false)");
                yield break;
            }

            int total = _config.Sounds.Count;
            if (total == 0)
            {
                ModLog.Info("config.json не содержит маппингов — звуки остаются оригинальными");
                yield break;
            }

            ModLog.Info("загрузка пользовательских звуков, маппингов: " + total);
            int loaded = 0;

            foreach (KeyValuePair<string, string> pair in _config.Sounds)
            {
                string originalName = pair.Key;
                string configuredPath = pair.Value;

                string absolutePath;
                string pathError;
                if (!ReplacerConfig.TryResolvePath(_pluginDirectory, configuredPath, out absolutePath, out pathError))
                {
                    Fail(originalName, configuredPath, pathError);
                    yield return null;
                    continue;
                }

                string extension = Path.GetExtension(absolutePath).ToLowerInvariant();
                AudioClip clip = null;
                string loadError = null;

                if (extension == ".wav")
                {
                    try
                    {
                        clip = AudioFileLoader.LoadWav(absolutePath);
                    }
                    catch (Exception ex)
                    {
                        loadError = ex.Message;
                    }
                }
                else if (extension == ".ogg")
                {
                    AudioClip decoded = null;
                    string decodeError = null;
                    yield return LoadOggCoroutine(absolutePath, c => decoded = c, e => decodeError = e);
                    clip = decoded;
                    loadError = decodeError;
                }
                else
                {
                    loadError = "неподдерживаемое расширение '" + extension + "' (нужен .ogg или .wav)";
                }

                if (clip == null)
                {
                    Fail(originalName, configuredPath, loadError ?? "неизвестная ошибка декодирования");
                    yield return null;
                    continue;
                }

                try
                {
                    clip.name = "SR_" + SanitizeClipName(Path.GetFileNameWithoutExtension(absolutePath));
                    Register(originalName, clip, configuredPath);
                    loaded++;
                    ModLog.Info("заменён звук '" + originalName + "' -> " + configuredPath);
                }
                catch (Exception ex)
                {
                    Fail(originalName, configuredPath, ex.Message);
                }

                // Отдаём кадр, чтобы загрузка десятков файлов не выглядела как фриз.
                yield return null;
            }

            ModLog.Info("итог загрузки: заменено " + loaded + " из " + total + " звук(ов)");
        }

        /// <summary>Вызывается при выгрузке мода, чтобы не оставлять созданные AudioClip в памяти.</summary>
        public void UnloadAll()
        {
            List<AudioClip> clips;
            lock (_sync)
            {
                clips = new List<AudioClip>(_owned);
                clips.AddRange(_replacements.Values);

                _replacements.Clear();
                _sources.Clear();
                _owned.Clear();
                _fitted.Clear();
                _lastStartBySource.Clear();
                _silent = null;
            }

            foreach (AudioClip clip in clips)
            {
                if (clip == null) continue;
                try
                {
                    UnityEngine.Object.Destroy(clip);
                }
                catch (Exception ex)
                {
                    ModLog.Warn("не удалось уничтожить AudioClip: " + ex.Message);
                }
            }
        }

        private void Register(string originalName, AudioClip clip, string source)
        {
            lock (_sync)
            {
                if (_replacements.ContainsKey(originalName))
                    ModLog.Warn("дубликат маппинга для '" + originalName + "', используется последний файл");

                _replacements[originalName] = clip;
                _sources[originalName] = source;
                _owned.Add(clip);
            }
        }

        private void Fail(string originalName, string path, string reason)
        {
            string message = "звук '" + originalName + "' не заменён: " + reason +
                             " (файл: " + path + ") — играет оригинальный звук";
            ModLog.Warn(message);

            lock (_sync) _failures.Add(message);
        }

        private static IEnumerator LoadOggCoroutine(string path, Action<AudioClip> onSuccess, Action<string> onError)
        {
            string uri;
            try
            {
                uri = new Uri(path).AbsoluteUri;
            }
            catch (Exception ex)
            {
                onError("не удалось построить file:// URI: " + ex.Message);
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError("Unity не смог декодировать OGG: " + (request.error ?? "unknown") +
                            " (HTTP " + request.responseCode + ")");
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null)
                {
                    onError("декодер вернул null");
                    yield break;
                }

                if (clip.samples <= 0)
                {
                    onError("в декодированном клипе 0 сэмплов");
                    yield break;
                }

                onSuccess(clip);
            }
        }

        private static string SanitizeClipName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "custom";

            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' ')
                    sb.Append(c);
                else
                    sb.Append('_');

                if (sb.Length >= 80) break;
            }

            return sb.Length == 0 ? "custom" : sb.ToString();
        }
    }
}