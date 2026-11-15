using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SoundReplacer.Logging;
using SoundReplacer.Shared;
using UnityEngine;

namespace SoundReplacer.Catalog
{
    /// <summary>
    /// Собирает имена реально встречающихся в игре AudioClip и пишет catalog.json.
    ///
    /// Зачем: снаружи Unity не знает, какие клипы есть в игре и как они называются.
    /// Приложение читает этот файл и показывает список звуков для замены.
    ///
    /// Источники имён:
    ///  - каждый старт проигрывания (Harmony-патч дергает Observe) — даёт реальные имена;
    ///  - периодическое сканирование уже загруженных AudioClip (Resources.FindObjectsOfTypeAll);
    ///  - скан после загрузки каждой сцены.
    ///
    /// Файл пишется не чаще раза в 10 секунд и всегда атомарно, чтобы приложение
    /// никогда не прочитало половину JSON.
    /// </summary>
    public sealed class CatalogCollector
    {
        private const int MaxEntries = 20000;

        private readonly string _path;
        private readonly bool _enabled;
        private readonly object _sync = new object();
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        private DateTime _nextWriteUtc = DateTime.UtcNow.AddSeconds(5);
        private DateTime _nextScanUtc = DateTime.MinValue;
        private bool _dirty;
        private bool _limitLogged;

        private sealed class Entry
        {
            public int Plays;
            public DateTime LastSeenUtc;
        }

        public CatalogCollector(string path, bool enabled)
        {
            _path = path;
            _enabled = enabled;
        }

        public int Count
        {
            get { lock (_sync) return _entries.Count; }
        }

        /// <summary>Вызывается на каждый старт звука. Обязан быть очень дешёвым.</summary>
        public void Observe(AudioClip clip)
        {
            if (!_enabled || clip == null) return;

            string name = clip.name;
            if (string.IsNullOrEmpty(name)) return;

            lock (_sync)
            {
                Entry entry;
                if (!_entries.TryGetValue(name, out entry))
                {
                    if (_entries.Count >= MaxEntries)
                    {
                        if (!_limitLogged)
                        {
                            _limitLogged = true;
                            ModLog.Warn("каталог достиг лимита " + MaxEntries + " имён, новые имена игнорируются");
                        }
                        return;
                    }

                    entry = new Entry();
                    _entries[name] = entry;
                }

                entry.Plays++;
                entry.LastSeenUtc = DateTime.UtcNow;
                _dirty = true;
            }
        }

        /// <summary>Сканирует уже загруженные в память AudioClip (не чаще раза в 10 секунд).</summary>
        public void ScanLoadedClips()
        {
            if (!_enabled) return;

            DateTime now = DateTime.UtcNow;
            if (now < _nextScanUtc) return;
            _nextScanUtc = now.AddSeconds(10);

            try
            {
                AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
                int added = 0;

                foreach (AudioClip clip in clips)
                {
                    if (clip == null) continue;

                    string name = clip.name;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (Register(name)) added++;
                }

                if (added > 0)
                    ModLog.Info("каталог: добавлено " + added + " новых имён, всего " + Count);
            }
            catch (Exception ex)
            {
                ModLog.Warn("сканирование AudioClip не удалось: " + ex.Message);
            }
        }

        /// <summary>Вызывать из Update: пишет каталог не чаще раза в 10 секунд, только если были изменения.</summary>
        public void Tick()
        {
            if (!_enabled) return;
            if (DateTime.UtcNow < _nextWriteUtc) return;

            _nextWriteUtc = DateTime.UtcNow.AddSeconds(10);
            WriteNow();
        }

        public void WriteNow()
        {
            if (!_enabled) return;

            SoundCatalog catalog;
            lock (_sync)
            {
                if (_entries.Count == 0) return;
                if (!_dirty && File.Exists(_path)) return;

                _dirty = false;
                catalog = BuildLocked();
            }

            string error;
            if (!catalog.Save(_path, out error))
            {
                ModLog.Warn("не удалось записать catalog.json: " + error);
                lock (_sync) _dirty = true;
                return;
            }

            ModLog.Debug("catalog.json обновлён: " + catalog.Sounds.Count + " записей");
        }

        private bool Register(string name)
        {
            lock (_sync)
            {
                if (_entries.ContainsKey(name)) return false;
                if (_entries.Count >= MaxEntries) return false;

                _entries[name] = new Entry { LastSeenUtc = DateTime.UtcNow };
                _dirty = true;
                return true;
            }
        }

        private SoundCatalog BuildLocked()
        {
            var catalog = new SoundCatalog
            {
                GeneratedUtc = DateTime.UtcNow.ToString("o"),
                GameVersion = Application.version,
                UnityVersion = Application.unityVersion,
                ModVersion = Plugin.PluginVersion
            };

            IEnumerable<KeyValuePair<string, Entry>> ordered = _entries
                .OrderByDescending(pair => pair.Value.Plays)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, Entry> pair in ordered)
            {
                catalog.Sounds.Add(new SoundCatalogEntry
                {
                    Name = pair.Key,
                    Plays = pair.Value.Plays,
                    LastSeenUtc = pair.Value.LastSeenUtc.ToString("o")
                });
            }

            return catalog;
        }
    }
}