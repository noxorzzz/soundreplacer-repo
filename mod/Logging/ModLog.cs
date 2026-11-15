using System;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx.Logging;

namespace SoundReplacer.Logging
{
    public enum ModLogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    /// <summary>
    /// Двойное логирование: BepInEx/LogOutput.log (через ManualLogSource) и отдельный
    /// BepInEx/plugins/SoundReplacer/SoundReplacer.log с ротацией.
    ///
    /// Логгер намеренно «безопасный»: любая ошибка записи в файл глотается и никогда
    /// не всплывает в игровую логику — замена звуков не должна ронять игру из-за логов.
    /// </summary>
    public static class ModLog
    {
        private const long MaxFileBytes = 4L * 1024 * 1024;
        private static readonly object Sync = new object();

        private static string _filePath;
        private static StreamWriter _writer;
        private static ManualLogSource _bepInEx;
        private static ModLogLevel _minimumLevel = ModLogLevel.Info;

        public static void Initialize(string filePath, ManualLogSource bepInExLogger)
        {
            lock (Sync)
            {
                _filePath = filePath;
                _bepInEx = bepInExLogger;

                try
                {
                    string directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    OpenWriterLocked();
                }
                catch (Exception ex)
                {
                    _writer = null;
                    if (_bepInEx != null)
                        _bepInEx.LogWarning("[SoundReplacer] не удалось открыть SoundReplacer.log: " + ex.Message);
                }
            }
        }

        public static void SetMinimumLevel(ModLogLevel level)
        {
            lock (Sync) _minimumLevel = level;
        }

        public static void Debug(string message)
        {
            Write(ModLogLevel.Debug, message, null);
        }

        public static void Info(string message, Exception exception = null)
        {
            Write(ModLogLevel.Info, message, exception);
        }

        public static void Warn(string message, Exception exception = null)
        {
            Write(ModLogLevel.Warn, message, exception);
        }

        public static void Error(string message, Exception exception = null)
        {
            Write(ModLogLevel.Error, message, exception);
        }

        public static void Shutdown()
        {
            lock (Sync)
            {
                try
                {
                    if (_writer != null)
                    {
                        _writer.Flush();
                        _writer.Dispose();
                    }
                }
                catch
                {
                    // на выходе из игры уже неважно
                }
                finally
                {
                    _writer = null;
                }
            }
        }

        private static void Write(ModLogLevel level, string message, Exception exception)
        {
            if (string.IsNullOrEmpty(message) && exception == null) return;

            string line = string.Format(CultureInfo.InvariantCulture,
                "{0} [{1,-5}] {2}",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                level.ToString().ToUpperInvariant(),
                message ?? string.Empty);

            if (exception != null)
                line += Environment.NewLine + "    " + exception.GetType().Name + ": " + exception.Message +
                        Environment.NewLine + exception.StackTrace;

            lock (Sync)
            {
                if (level < _minimumLevel) return;

                try
                {
                    if (_writer == null && !string.IsNullOrEmpty(_filePath)) OpenWriterLocked();
                    if (_writer != null)
                    {
                        RotateIfNeededLocked();
                        _writer.WriteLine(line);
                    }
                }
                catch
                {
                    // файл может быть занят другим процессом (например, приложением) — не падаем
                }

                WriteToBepInExLocked(level, message ?? string.Empty);
            }
        }

        private static void WriteToBepInExLocked(ModLogLevel level, string message)
        {
            if (_bepInEx == null) return;

            try
            {
                switch (level)
                {
                    case ModLogLevel.Debug:
                        _bepInEx.Log(LogLevel.Debug, message);
                        break;
                    case ModLogLevel.Warn:
                        _bepInEx.Log(LogLevel.Warning, message);
                        break;
                    case ModLogLevel.Error:
                        _bepInEx.Log(LogLevel.Error, message);
                        break;
                    default:
                        _bepInEx.Log(LogLevel.Info, message);
                        break;
                }
            }
            catch
            {
                // BepInEx логгер не должен ломать мод
            }
        }

        private static void OpenWriterLocked()
        {
            if (_writer != null)
            {
                try { _writer.Dispose(); } catch { }
                _writer = null;
            }

            var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        }

        private static void RotateIfNeededLocked()
        {
            if (_writer == null) return;
            if (_writer.BaseStream.Length < MaxFileBytes) return;

            try
            {
                _writer.Dispose();
            }
            catch
            {
                // ignore
            }
            _writer = null;

            string previous = _filePath + ".1";
            try
            {
                if (File.Exists(previous)) File.Delete(previous);
                File.Move(_filePath, previous);
            }
            catch
            {
                // если не удалось — просто продолжаем писать в тот же файл
            }

            OpenWriterLocked();
        }
    }
}