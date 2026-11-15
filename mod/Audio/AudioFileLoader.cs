using System;
using System.IO;
using System.Text;
using SoundReplacer.Logging;
using UnityEngine;

namespace SoundReplacer.Audio
{
    /// <summary>
    /// Загрузка пользовательских .wav/.ogg в AudioClip во время работы игры.
    ///
    /// .wav декодируется вручную (RIFF/PCM), потому что UnityWebRequestMultimedia
    /// нестабильно работает с WAV на standalone-билдах, а нам нужен предсказуемый
    /// результат и понятные сообщения об ошибках.
    ///
    /// .ogg декодируется самим Unity через UnityWebRequestMultimedia (Vorbis встроен
    /// в движок), это делается корутиной в SoundReplacementManager.
    ///
    /// Любая проблема с файлом — исключение InvalidDataException с человекочитаемым
    /// текстом. Вызывающий код логирует его и оставляет оригинальный звук.
    /// </summary>
    public static class AudioFileLoader
    {
        public const int MaxDurationSeconds = 120;
        public const long MaxFileBytes = 200L * 1024 * 1024;

        public static bool IsSupportedExtension(string path)
        {
            string extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension)) return false;
            extension = extension.ToLowerInvariant();
            return extension == ".wav" || extension == ".ogg";
        }

        public static AudioClip LoadWav(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists) throw new InvalidDataException("файл не найден");
            if (info.Length > MaxFileBytes) throw new InvalidDataException("файл больше 200 МБ");

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 44) throw new InvalidDataException("файл короче 44 байт — это не WAV");

            if (!(bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'))
                throw new InvalidDataException("нет сигнатуры RIFF");
            if (!(bytes[8] == 'W' && bytes[9] == 'A' && bytes[10] == 'V' && bytes[11] == 'E'))
                throw new InvalidDataException("нет сигнатуры WAVE");

            int formatTag = 0;
            int channels = 0;
            int sampleRate = 0;
            int bitsPerSample = 0;
            int dataOffset = -1;
            int dataLength = 0;

            int position = 12;
            while (position + 8 <= bytes.Length)
            {
                string chunkId = Encoding.ASCII.GetString(bytes, position, 4);
                int chunkSize = BitConverter.ToInt32(bytes, position + 4);
                int body = position + 8;

                if (chunkSize < 0 || body > bytes.Length) break;

                if (chunkId == "fmt ")
                {
                    if (chunkSize < 16 || body + 16 > bytes.Length)
                        throw new InvalidDataException("некорректный fmt-чанк");

                    formatTag = BitConverter.ToUInt16(bytes, body);
                    channels = BitConverter.ToUInt16(bytes, body + 2);
                    sampleRate = (int)BitConverter.ToUInt32(bytes, body + 4);
                    bitsPerSample = BitConverter.ToUInt16(bytes, body + 14);

                    // WAVE_FORMAT_EXTENSIBLE: реальный кодек лежит в SubFormat GUID.
                    if (formatTag == 0xFFFE && chunkSize >= 40 && body + 26 <= bytes.Length)
                        formatTag = BitConverter.ToUInt16(bytes, body + 24);
                }
                else if (chunkId == "data")
                {
                    dataOffset = body;
                    dataLength = Math.Min(chunkSize, bytes.Length - body);
                }

                long next = (long)body + chunkSize + (chunkSize & 1);
                if (next <= position) break;
                position = (int)Math.Min(next, bytes.Length);
            }

            if (formatTag != 1 && formatTag != 3)
                throw new InvalidDataException("поддерживается только PCM или IEEE-float WAV (format=" + formatTag + ")");
            if (channels < 1 || channels > 2)
                throw new InvalidDataException("поддерживается 1 или 2 канала, найдено " + channels);
            if (sampleRate < 4000 || sampleRate > 192000)
                throw new InvalidDataException("неподдерживаемая частота дискретизации: " + sampleRate + " Гц");
            if (bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 24 && bitsPerSample != 32)
                throw new InvalidDataException("неподдерживаемая разрядность: " + bitsPerSample + " бит");
            if (formatTag == 3 && bitsPerSample != 32)
                throw new InvalidDataException("IEEE-float WAV поддерживается только с 32 битами");
            if (dataOffset < 0 || dataLength <= 0)
                throw new InvalidDataException("в файле нет data-чанка со звуком");

            int bytesPerSample = bitsPerSample / 8;
            int frameSize = bytesPerSample * channels;
            int frames = dataLength / frameSize;

            if (frames <= 0) throw new InvalidDataException("в файле нет сэмплов");
            if (frames > sampleRate * MaxDurationSeconds)
                throw new InvalidDataException("звук длиннее " + MaxDurationSeconds + " секунд");

            float[] samples = new float[frames * channels];
            int offset = dataOffset;

            for (int i = 0; i < samples.Length; i++)
            {
                if (formatTag == 3)
                {
                    samples[i] = Clamp(BitConverter.ToSingle(bytes, offset));
                }
                else
                {
                    switch (bitsPerSample)
                    {
                        case 8:
                            samples[i] = (bytes[offset] - 128) / 128f;
                            break;
                        case 16:
                            samples[i] = BitConverter.ToInt16(bytes, offset) / 32768f;
                            break;
                        case 24:
                            int value = bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16);
                            if ((value & 0x800000) != 0) value |= unchecked((int)0xFF000000);
                            samples[i] = value / 8388608f;
                            break;
                        default: // 32-bit PCM
                            samples[i] = BitConverter.ToInt32(bytes, offset) / 2147483648f;
                            break;
                    }
                }

                offset += bytesPerSample;
            }

            string clipName = Path.GetFileNameWithoutExtension(path);
            AudioClip clip = AudioClip.Create(clipName, frames, channels, sampleRate, false);
            if (clip == null)
                throw new InvalidDataException("Unity не смог создать AudioClip (возможно, закончилась память)");

            if (!clip.SetData(samples, 0))
                throw new InvalidDataException("Unity не принял аудиоданные (SetData вернул false)");

            return clip;
        }

        private static float Clamp(float value)
        {
            if (value > 1f) return 1f;
            if (value < -1f) return -1f;
            return value;
        }

        /// <summary>Логирует причину, по которой файл не был загружен. Вызывается из менеджера.</summary>
        internal static void LogFailure(string originalName, string file, string reason)
        {
            ModLog.Warn("звук '" + originalName + "' не заменён: " + reason + " (файл: " + file + ")");
        }
    }
}