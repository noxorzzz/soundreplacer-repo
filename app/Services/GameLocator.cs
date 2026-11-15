using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SoundReplacer.App.Services
{
    /// <summary>
    /// Ищет папку игры: сначала по запущенному процессу, потом по библиотекам Steam.
    /// Если не нашлось — пользователь выбирает папку вручную (MainForm).
    /// AppID намеренно не хардкодим: ищем steamapps/common/*/REPO.exe во всех библиотеках.
    /// </summary>
    public static class GameLocator
    {
        public static readonly string[] KnownExeNames = { "REPO.exe", "REPO_IL2CPP.exe" };

        private static readonly Regex PathRegex = new Regex(
            "\"path\"\\s*\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool IsGameDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return false;
            if (!Directory.Exists(directory)) return false;

            foreach (string exe in KnownExeNames)
            {
                if (File.Exists(Path.Combine(directory, exe))) return true;
            }

            return Directory.Exists(Path.Combine(directory, "REPO_Data"))
                   || Directory.Exists(Path.Combine(directory, "BepInEx"));
        }

        /// <summary>Возвращает папку игры, если процесс запущен и удалось прочитать путь к exe.</summary>
        public static string FromRunningProcess(IEnumerable<string> processNames)
        {
            if (processNames == null) return null;

            foreach (string rawName in processNames)
            {
                if (string.IsNullOrWhiteSpace(rawName)) continue;

                string name = rawName.Trim();
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - 4);

                Process[] processes = Array.Empty<Process>();
                try
                {
                    processes = Process.GetProcessesByName(name);
                    foreach (Process process in processes)
                    {
                        try
                        {
                            string path = process.MainModule == null ? null : process.MainModule.FileName;
                            if (string.IsNullOrEmpty(path)) continue;

                            string directory = Path.GetDirectoryName(path);
                            if (IsGameDirectory(directory)) return directory;
                        }
                        catch
                        {
                            // нет прав на MainModule (игра от другого пользователя / elevated) — идём дальше
                        }
                    }
                }
                catch
                {
                    // процесс мог завершиться между вызовами
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

        public static string AutoDetect(IEnumerable<string> processNames)
        {
            string running = FromRunningProcess(processNames);
            if (!string.IsNullOrEmpty(running)) return running;
            return FromSteam();
        }

        public static string FromSteam()
        {
            foreach (string steamRoot in SteamRoots())
            {
                string steamApps = Path.Combine(steamRoot, "steamapps");
                if (!Directory.Exists(steamApps)) continue;

                string found = FindGameInCommon(Path.Combine(steamApps, "common"));
                if (found != null) return found;

                string vdf = Path.Combine(steamApps, "libraryfolders.vdf");
                foreach (string library in ReadLibraryFolders(vdf))
                {
                    found = FindGameInCommon(Path.Combine(library, "steamapps", "common"));
                    if (found != null) return found;
                }
            }

            return null;
        }

        private static IEnumerable<string> SteamRoots()
        {
            var roots = new List<string>();

            string programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles");

            if (!string.IsNullOrEmpty(programFilesX86)) roots.Add(Path.Combine(programFilesX86, "Steam"));
            if (!string.IsNullOrEmpty(programFiles)) roots.Add(Path.Combine(programFiles, "Steam"));

            roots.Add(@"C:\Program Files (x86)\Steam");
            roots.Add(@"C:\Program Files\Steam");
            roots.Add(@"C:\Steam");

            return roots.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists);
        }

        private static IEnumerable<string> ReadLibraryFolders(string vdfPath)
        {
            if (!File.Exists(vdfPath)) yield break;

            string text;
            try
            {
                text = File.ReadAllText(vdfPath);
            }
            catch
            {
                yield break;
            }

            foreach (Match match in PathRegex.Matches(text))
            {
                string value = match.Groups[1].Value.Replace("\\\\", "\\").Trim();
                if (value.Length == 0) continue;
                if (!Directory.Exists(value)) continue;
                yield return value;
            }
        }

        private static string FindGameInCommon(string commonPath)
        {
            if (!Directory.Exists(commonPath)) return null;

            try
            {
                foreach (string directory in Directory.EnumerateDirectories(commonPath))
                {
                    if (IsGameDirectory(directory)) return directory;
                }
            }
            catch
            {
                // нет доступа к части библиотеки — не критично
            }

            return null;
        }
    }
}