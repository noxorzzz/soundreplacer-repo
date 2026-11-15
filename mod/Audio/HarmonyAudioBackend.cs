using System;
using System.Reflection;
using HarmonyLib;
using SoundReplacer.Catalog;
using SoundReplacer.Logging;
using UnityEngine;

namespace SoundReplacer.Audio
{
    /// <summary>
    /// Основной (и единственный проверенный для R.E.P.O.) бэкенд подмены: Harmony-патчи
    /// на точки старта проигрывания AudioSource.
    ///
    /// Патчится:
    ///   AudioSource.Play()
    ///   AudioSource.Play(ulong)
    ///   AudioSource.PlayDelayed(float)
    ///   AudioSource.PlayScheduled(double)
    ///   AudioSource.PlayOneShot(AudioClip)
    ///   AudioSource.PlayOneShot(AudioClip, float)
    ///   AudioSource.PlayClipAtPoint(Vector3, AudioClip)
    ///   AudioSource.PlayClipAtPoint(Vector3, AudioClip, float)
    ///   AudioSource.set_clip  (свойство clip; можно отключить в config.json)
    ///
    /// Подход не требует знания конкретных классов игры: любой звук, который игра
    /// проигрывает через AudioSource с известным именем клипа, может быть заменён.
    /// Именно так работает loaforcsSoundAPI для Lethal Company — только у него
    /// дополнительно есть регистрация наборов из asset bundle.
    /// </summary>
    public sealed class HarmonyAudioBackend : ISoundBackend
    {
        private const string HarmonyId = "com.soundreplacer.repo.audio";

        private Harmony _harmony;
        private bool _installed;

        public string Name
        {
            get { return "Harmony / AudioSource"; }
        }

        public bool Install(SoundReplacementManager manager)
        {
            if (_installed) return true;
            if (_harmony == null) _harmony = new Harmony(HarmonyId);

            int patched = 0;
            int missing = 0;

            MethodInfo sourcePrefix = AccessTools.Method(typeof(SourcePlayPatch), "Prefix");
            MethodInfo argumentPrefix = AccessTools.Method(typeof(ClipArgumentPatch), "Prefix");
            MethodInfo setClipPostfix = AccessTools.Method(typeof(SetClipPatch), "Postfix");

            // --- инстансные методы, где клип уже лежит в AudioSource.clip
            if (Patch(AccessTools.Method(typeof(AudioSource), "Play", new Type[0]), sourcePrefix, null, ref missing)) patched++;
            if (Patch(AccessTools.Method(typeof(AudioSource), "Play", new[] { typeof(ulong) }), sourcePrefix, null, ref missing)) patched++;
            if (Patch(AccessTools.Method(typeof(AudioSource), "PlayDelayed", new[] { typeof(float) }), sourcePrefix, null, ref missing)) patched++;
            if (Patch(AccessTools.Method(typeof(AudioSource), "PlayScheduled", new[] { typeof(double) }), sourcePrefix, null, ref missing)) patched++;

            // --- методы, где клип приходит аргументом
            if (Patch(AccessTools.Method(typeof(AudioSource), "PlayOneShot", new[] { typeof(AudioClip) }), argumentPrefix, null, ref missing)) patched++;
            if (Patch(AccessTools.Method(typeof(AudioSource), "PlayOneShot", new[] { typeof(AudioClip), typeof(float) }), argumentPrefix, null, ref missing)) patched++;
            if (Patch(AccessTools.Method(typeof(AudioSource), "PlayClipAtPoint", new[] { typeof(Vector3), typeof(AudioClip) }), argumentPrefix, null, ref missing)) patched++;
            if (Patch(AccessTools.Method(typeof(AudioSource), "PlayClipAtPoint", new[] { typeof(Vector3), typeof(AudioClip), typeof(float) }), argumentPrefix, null, ref missing)) patched++;

            // --- сеттер свойства clip (ловит «положил клип, потом Play()»)
            if (Patch(AccessTools.Method(typeof(AudioSource), "set_clip"), null, setClipPostfix, ref missing)) patched++;

            _installed = patched > 0;
            ModLog.Info("Harmony backend: установлено патчей " + patched + ", недоступно " + missing);

            if (patched == 0)
                ModLog.Error("Harmony backend не установил ни одного патча — замена звуков работать не будет");

            return _installed;
        }

        public void Uninstall()
        {
            if (_harmony == null) return;

            try
            {
                _harmony.UnpatchSelf();
            }
            catch (Exception ex)
            {
                ModLog.Warn("не удалось снять Harmony-патчи: " + ex.Message);
            }
            finally
            {
                _installed = false;
            }
        }

        private bool Patch(MethodInfo target, MethodInfo prefix, MethodInfo postfix, ref int missing)
        {
            string label = target == null ? "?" : target.DeclaringType.Name + "." + target.Name;

            if (target == null)
            {
                missing++;
                ModLog.Warn("метод для патча не найден: " + label);
                return false;
            }

            try
            {
                _harmony.Patch(
                    target,
                    prefix: prefix == null ? null : new HarmonyMethod(prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(postfix));

                ModLog.Debug("пропатчен " + label);
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Warn("не удалось пропатчить " + label + ": " + ex.Message);
                return false;
            }
        }

        private static SoundReplacementManager Manager
        {
            get { return Plugin.Instance == null ? null : Plugin.Instance.Replacements; }
        }

        /// <summary>Каждый реально проигранный клип попадает в catalog.json (и счётчик plays).</summary>
        private static void ObserveClip(AudioClip clip)
        {
            if (clip == null || Plugin.Instance == null) return;

            CatalogCollector catalog = Plugin.Instance.Catalog;
            if (catalog != null) catalog.Observe(clip);

            SoundReplacementManager manager = Plugin.Instance.Replacements;
            if (manager != null && manager.LogPlays)
            {
                ModLog.Info("играет звук: '" + clip.name + "' (" +
                            clip.length.ToString("0.00") + " c, " + clip.channels + " кан.)");
            }
        }

        internal static class SourcePlayPatch
        {
            // ReSharper disable once UnusedParameter.Global
            public static void Prefix(AudioSource __instance)
            {
                if (__instance == null) return;

                AudioClip current = __instance.clip;
                ObserveClip(current);

                SoundReplacementManager manager = Manager;
                if (manager == null) return;

                AudioClip replacement;
                if (!manager.TryGet(current, out replacement)) return;

                AudioPatchHelpers.AssignClip(__instance, replacement);
            }
        }

        internal static class ClipArgumentPatch
        {
            public static void Prefix(ref AudioClip clip)
            {
                if (clip == null) return;

                ObserveClip(clip);

                SoundReplacementManager manager = Manager;
                if (manager == null) return;

                AudioClip replacement;
                if (!manager.TryGet(clip, out replacement)) return;

                clip = replacement;
            }
        }

        internal static class SetClipPatch
        {
            public static void Postfix(AudioSource __instance, AudioClip value)
            {
                if (__instance == null || value == null) return;

                SoundReplacementManager manager = Manager;
                if (manager == null || !manager.PatchClipSetter) return;

                AudioClip replacement;
                if (!manager.TryGet(value, out replacement)) return;

                AudioPatchHelpers.AssignClip(__instance, replacement);
            }
        }
    }

    internal static class AudioPatchHelpers
    {
        [ThreadStatic] private static bool _assigning;

        public static void AssignClip(AudioSource source, AudioClip clip)
        {
            if (source == null || clip == null) return;
            if (_assigning) return; // защита от рекурсии через патченный set_clip

            _assigning = true;
            try
            {
                source.clip = clip;
            }
            catch (Exception ex)
            {
                ModLog.Warn("не удалось подменить клип у AudioSource: " + ex.Message);
            }
            finally
            {
                _assigning = false;
            }
        }
    }
}
