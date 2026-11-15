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
                replacement = found;
                return true;
            }
        }

        public bool TryGetByName(string originalName, out AudioClip replacement)
        {
            replacement = null;
            if (string.IsNullOrEmpty(originalName) || !_config.Enabled) return false;

            lock (_sync)
            {
                return _replacements.TryGetValue(originalName, out replacement) && replacement != null;
            }
        }

        public IEnumerator LoadAllCoroutine()
        {
            lock (_sync)
            {
                _replacements.Clear();
                _sources.Clear();
                _failures.Clear();
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
                clips = new List<AudioClip>(_replacements.Values);
                _replacements.Clear();
                _sources.Clear();
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