using System;
using SoundReplacer.Logging;
using UnityEngine;

namespace SoundReplacer.Audio
{
    /// <summary>
    /// Подгоняет пользовательский клип под оригинал, чтобы замена не ломала игру:
    ///
    ///  - длина: файл длиннее оригинала обрезается до длины оригинала, иначе звук
    ///    продолжает играть после события (шаг, выстрел) и накладывается сам на себя;
    ///  - громкость: пик файла приводится к пику оригинала, иначе один звук
    ///    выстреливает громче остальных, хотя источник и микшер те же;
    ///  - каналы: стерео сводится в моно, если оригинал моно — иначе 3D-панорама
    ///    (затухание по карте, позиция) работает некорректно: Unity не панорамирует
    ///    стерео-клип, и звук «прилипает» к игроку.
    ///
    /// Всё, что касается эффектов, остаётся нетронутым: мы меняем только AudioClip,
    /// а AudioSource (spatialBlend, minDistance, maxDistance, rolloff, doppler,
    /// panStereo, outputAudioMixerGroup, reverbZoneMix, volume, pitch) не трогаем —
    /// поэтому пространственный эффект на карте сохраняется автоматически.
    /// </summary>
    internal static class ClipFitter
    {
        private const float PeakFloor = 0.0001f;
        private const float GainTolerance = 0.02f;

        public static AudioClip Fit(AudioClip original, AudioClip custom, bool matchLength, bool matchVolume, bool matchChannels)
        {
            if (original == null || custom == null) return null;

            try
            {
                int sourceChannels = custom.channels;
                int sourceFrames = sourceChannels > 0 ? custom.samples / sourceChannels : 0;
                if (sourceFrames <= 0) return null;

                bool downmix = matchChannels && original.channels == 1 && sourceChannels == 2;

                int targetFrames = sourceFrames;
                bool trim = false;
                if (matchLength && original.length > 0f && custom.length > original.length + 0.01f)
                {
                    int frames = (int)Math.Floor(original.length * custom.frequency);
                    if (frames > 0 && frames < sourceFrames)
                    {
                        targetFrames = frames;
                        trim = true;
                    }
                }

                float[] source = new float[custom.samples * sourceChannels];
                if (!custom.GetData(source, 0))
                {
                    ModLog.Warn("подгонка '" + original.name + "': Unity не отдал сэмплы файла, оставляю как есть");
                    return null;
                }

                float customPeak = Peak(source, source.Length);
                float gain = 1f;
                bool level = false;

                if (matchVolume && customPeak > PeakFloor)
                {
                    float originalPeak = TryPeak(original);
                    if (originalPeak > PeakFloor)
                    {
                        gain = originalPeak / customPeak;
                        if (Math.Abs(gain - 1f) > GainTolerance)
                            level = true;
                        else
                            gain = 1f;
                    }
                }

                if (!trim && !downmix && !level) return null;

                int outChannels = downmix ? 1 : sourceChannels;
                float[] output = new float[targetFrames * outChannels];

                if (downmix)
                {
                    for (int frame = 0; frame < targetFrames; frame++)
                        output[frame] = (source[frame * 2] + source[frame * 2 + 1]) * 0.5f;
                }
                else
                {
                    Array.Copy(source, 0, output, 0, output.Length);
                }

                if (level)
                {
                    for (int i = 0; i < output.Length; i++)
                        output[i] = Mathf.Clamp(output[i] * gain, -1f, 1f);
                }

                AudioClip fitted = AudioClip.Create(
                    custom.name + "_fit",
                    targetFrames,
                    outChannels,
                    custom.frequency,
                    false);

                if (!fitted.SetData(output, 0))
                {
                    UnityEngine.Object.Destroy(fitted);
                    ModLog.Warn("подгонка '" + original.name + "': SetData не сработал, оставляю оригинальный файл");
                    return null;
                }

                ModLog.Info(string.Format(
                    "подгонка '{0}': длина {1:0.00} c -> {2:0.00} c{3}, громкость пик {4:0.000} -> {5:0.000}{6}, каналы {7} -> {8}",
                    original.name,
                    custom.length,
                    (float)targetFrames / custom.frequency,
                    trim ? " (обрезано)" : string.Empty,
                    customPeak,
                    level ? customPeak * gain : customPeak,
                    level ? " (нормализовано)" : string.Empty,
                    sourceChannels,
                    outChannels));

                return fitted;
            }
            catch (Exception ex)
            {
                ModLog.Warn("подгонка '" + (original != null ? original.name : "?") + "' не удалась: " + ex.Message);
                return null;
            }
        }

        private static float TryPeak(AudioClip clip)
        {
            try
            {
                int samples = clip.samples * clip.channels;
                if (samples <= 0) return 0f;

                float[] buffer = new float[samples];
                if (!clip.GetData(buffer, 0)) return 0f;

                return Peak(buffer, buffer.Length);
            }
            catch (Exception ex)
            {
                ModLog.Debug("пик оригинала '" + clip.name + "' недоступен: " + ex.Message);
                return 0f;
            }
        }

        private static float Peak(float[] data, int count)
        {
            float peak = 0f;
            for (int i = 0; i < count; i++)
            {
                float value = data[i] < 0f ? -data[i] : data[i];
                if (value > peak) peak = value;
            }

            return peak;
        }
    }
}