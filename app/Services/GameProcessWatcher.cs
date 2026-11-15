using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SoundReplacer.App.Services
{
    /// <summary>
    /// Проверка «запущена ли игра». Нужна только для предупреждения: мод читает
    /// config.json один раз на старте, поэтому при работающей игре изменения
    /// вступят в силу только после перезапуска.
    /// </summary>
    public static class GameProcessWatcher
    {
        public static string GetRunningName(IEnumerable<string> processNames)
        {
            if (processNames == null) return null;

            foreach (string rawName in processNames)
            {
                if (string.IsNullOrWhiteSpace(rawName)) continue;

                string name = rawName.Trim();
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - 4);

                Process[] processes;
                try
                {
                    processes = Process.GetProcessesByName(name);
                }
                catch
                {
                    continue;
                }

                try
                {
                    if (processes.Length > 0) return name + ".exe";
                }
                finally
                {
                    foreach (Process process in processes)
                    {
                        try { process.Dispose(); } catch { }
                    }
                }
            }

            return null;
        }
    }
}