using System;
using System.Collections;
using System.IO;
using BepInEx;
using SoundReplacer.Audio;
using SoundReplacer.Catalog;
using SoundReplacer.Logging;
using SoundReplacer.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoundReplacer
{
    /// <summary>
    /// Точка входа мода.
    ///
    /// Жизненный цикл:
    ///   1. Awake: создаём папку, логгер, читаем config.json, ставим Harmony-патчи,
    ///      запускаем корутину загрузки аудиофайлов.
    ///   2. Корутина: грузим .ogg/.wav в AudioClip и регистрируем их по именам.
    ///   3. Дальше любой AudioSource.Play*/PlayOneShot с известным именем клипа
    ///      получает подменённый клип (см. HarmonyAudioBackend).
    ///
    /// Мод НИКОГДА не вызывает методы приложения: связь только через config.json,
    /// папку Sounds и catalog.json (файловая система).
    /// </summary>
    [BepInPlugin(Guid, PluginName, PluginVersion)]
    // [BepInProcess("REPO.exe")] — можно раскомментировать, если exe игры точно называется REPO.exe.
    // По умолчанию не ставим: при переименовании exe (например, REPO_IL2CPP.exe) мод бы просто не загрузился.
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.soundreplacer.repo";
        public const string PluginName = "SoundReplacer";
        public const string PluginVersion = "1.6.0";

        internal static Plugin Instance { get; private set; }

        internal static string PluginDirectory { get; private set; }

        internal static string ConfigFilePath
        {
            get { return Path.Combine(PluginDirectory, ReplacerConfig.FileName); }
        }

        internal static string CatalogFilePath
        {
            get { return Path.Combine(PluginDirectory, SoundCatalog.FileName); }
        }

        internal static string SoundsDirectory
        {
            get { return Path.Combine(PluginDirectory, "Sounds"); }
        }

        internal static string LogFilePath
        {
            get { return Path.Combine(PluginDirectory, "SoundReplacer.log"); }
        }

        internal SoundReplacementManager Replacements { get; private set; }

        internal CatalogCollector Catalog { get; private set; }

        private ISoundBackend _backend;
        private ReplacerConfig _config;
        private bool _ready;

        private void Awake()
        {
            Instance = this;

            try
            {
                Initialize();
            }
            catch (Exception ex)
            {
                _ready = false;
                ModLog.Error("инициализация SoundReplacer провалилась, мод отключён: " + ex.Message, ex);
                Logger.LogError("[SoundReplacer] " + ex);
            }
        }

        private void Initialize()
        {
            string directory = Path.GetDirectoryName(Info.Location);
            if (string.IsNullOrEmpty(directory)) directory = Paths.PluginPath;
            PluginDirectory = directory;

            Directory.CreateDirectory(PluginDirectory);
            Directory.CreateDirectory(SoundsDirectory);

            ModLog.Initialize(LogFilePath, Logger);
            ModLog.Info("SoundReplacer " + PluginVersion + " стартует из " + PluginDirectory);
            ModLog.Info("игра " + Application.version + ", Unity " + Application.unityVersion + ", платформа " + Application.platform);

            string probeDetails;
            if (SoundApiProbe.IsLoaded(out probeDetails))
                ModLog.Info("в процессе найдена SoundAPI-сборка (" + probeDetails + "), используется собственный Harmony-бэкенд");

            _config = LoadConfig();

            Replacements = new SoundReplacementManager(_config, PluginDirectory);

            _backend = new HarmonyAudioBackend();
            if (!_backend.Install(Replacements))
                ModLog.Error("Harmony-бэкенд не установлен, звуки останутся оригинальными");

            Catalog = new CatalogCollector(CatalogFilePath, _config.ExportCatalog);
            Catalog.ScanLoadedClips();

            SceneManager.sceneLoaded += OnSceneLoaded;

            _ready = true;
            StartCoroutine(StartupRoutine());
        }

        private IEnumerator StartupRoutine()
        {
            // Даём игре догрузить первую сцену и ассеты.
            yield return null;
            yield return null;

            if (_config == null) yield break;

            if (!_config.Enabled)
            {
                ModLog.Warn("замена звуков отключена в config.json (enabled = false)");
                yield break;
            }

            yield return Replacements.LoadAllCoroutine();

            ModLog.Info("активных замен: " + Replacements.Count);
            Catalog.WriteNow();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_ready) return;
            Catalog.ScanLoadedClips();
        }

        private void Update()
        {
            if (!_ready) return;
            Catalog.Tick();
        }

        private void OnApplicationQuit()
        {
            if (!_ready) return;
            Catalog.WriteNow();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            try
            {
                if (Catalog != null) Catalog.WriteNow();
            }
            catch (Exception ex)
            {
                ModLog.Warn("не удалось сохранить catalog.json при выходе: " + ex.Message);
            }

            if (_backend != null) _backend.Uninstall();
            if (Replacements != null) Replacements.UnloadAll();

            ModLog.Info("SoundReplacer выгружен");
            ModLog.Shutdown();
        }

        private ReplacerConfig LoadConfig()
        {
            string path = ConfigFilePath;

            if (!File.Exists(path))
            {
                ModLog.Warn("config.json не найден: " + path);
                ModLog.Warn("пока звуки оригинальные. Запустите SoundReplacer.App, выберите звуки и нажмите «Применить».");
                return new ReplacerConfig();
            }

            ReplacerConfig config;
            string error;
            if (!ReplacerConfig.TryLoad(path, out config, out error))
            {
                ModLog.Error("config.json не прочитан: " + error);
                ModLog.Error("играют оригинальные звуки; исправьте файл и перезапустите игру");
                return new ReplacerConfig();
            }

            ModLog.Info("конфиг загружен: " + config.Describe());
            return config;
        }
    }
}