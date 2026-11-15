using System;
using System.IO;
using System.Text;

namespace SoundReplacer.App.Services
{
    /// <summary>
    /// Проверка файла ДО записи конфига: чтобы мод не тратил время на заведомо битый файл,
    /// а пользователь сразу увидел понятную причину.
    /// </summary>
    public static class SoundFileValidator
    {
        public const long MaxFileBytes = 200L * 1024 * 1024;

        public static bool TryValidate(string path, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "файл не выбран";
                return false;
            }

            if (!File.Exists(path))
            {
                error = "файл не найден";
                return false;
            }

            var info = new FileInfo(path);
            if (info.Length < 64)
            {
                error = "файл слишком маленький и похож на пустой";
                return false;
            }

            if (info.Length > MaxFileBytes)
            {
                error = "файл больше 200 МБ";
                return false;
            }

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".ogg" && extension != ".wav")
            {
                error = "поддерживаются только .ogg и .wav";
                return false;
            }

            byte[] header;
            try
            {
                header = new byte[256];
                using (FileStream stream = File.OpenRead(path))
                {
                    int read = stream.Read(header, 0, header.Length);
                    if (read < 12)
                    {
                        error = "не удалось прочитать заголовок файла";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                error = "не удалось открыть файл: " + ex.Message;
                return false;
            }

            if (extension == ".wav")
            {
                bool riff = header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F';
                bool wave = header[8] == 'W' && header[9] == 'A' && header[10] == 'V' && header[11] == 'E';

                if (!riff || !wave)
                {
                    error = "файл не является WAV (нет RIFF/WAVE)";
                    return false;
                }

                return true;
            }

            // .ogg
            bool ogg = header[0] == 'O' && header[1] == 'g' && header[2] == 'g' && header[3] == 'S';
            if (!ogg)
            {
                error = "файл не является OGG (нет сигнатуры OggS)";
                return false;
            }

            // Unity умеет только Ogg Vorbis; Ogg Opus/FWMA отвалится на декодировании.
            string ascii = Encoding.ASCII.GetString(header);
            if (ascii.IndexOf("OpusHead", StringComparison.Ordinal) >= 0)
            {
                error = "это Ogg Opus, а Unity поддерживает только Ogg Vorbis — перекодируйте в Vorbis";
                return false;
            }

            return true;
        }
    }
}