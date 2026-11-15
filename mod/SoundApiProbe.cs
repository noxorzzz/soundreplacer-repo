using System;
using System.Collections.Generic;
using SoundReplacer.Logging;

namespace SoundReplacer
{
    /// <summary>
    /// Проверяет, загружена ли в процесс какая-либо SoundAPI-библиотека
    /// (loaforcsSoundAPI и её возможные порты). Нужно только для диагностики:
    /// мод всё равно работает через свой Harmony-бэкенд.
    ///
    /// Если порт SoundAPI под R.E.P.O. появится, здесь же можно сделать
    /// выбор бэкенда: SoundApiBackend : ISoundBackend вместо HarmonyAudioBackend.
    /// </summary>
    internal static class SoundApiProbe
    {
        private static readonly string[] Markers = { "SoundAPI", "SoundApi", "Loaforc" };

        internal static bool IsLoaded(out string details)
        {
            var found = new List<string>();

            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string name = assembly.GetName().Name;
                    if (string.IsNullOrEmpty(name)) continue;

                    for (int i = 0; i < Markers.Length; i++)
                    {
                        if (name.IndexOf(Markers[i], StringComparison.OrdinalIgnoreCase) < 0) continue;
                        found.Add(name);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Debug("SoundAPI probe: " + ex.Message);
            }

            details = found.Count == 0 ? null : string.Join(", ", found.ToArray());
            return found.Count > 0;
        }
    }
}