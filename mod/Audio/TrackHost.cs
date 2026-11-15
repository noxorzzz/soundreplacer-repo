using SoundReplacer.Logging;
using SoundReplacer.Shared;
using UnityEngine;

namespace SoundReplacer.Audio
{
    /// <summary>
    /// Трек играет на собственном AudioSource в точке события.
    ///
    /// Зачем отдельный источник: игра при каждом «гудке» вызывает Play() на своём
    /// AudioSource, и если бы трек висел на нём, второй гудок обрывал бы трек.
    /// Свой источник играет независимо, а spatialBlend = 1 даёт честную физику:
    /// ближе — громче, дальше — тише (лог-затухание, как у 3D-звуков Unity).
    /// </summary>
    internal sealed class TrackHost : MonoBehaviour
    {
        internal static void Play(Vector3 position, AudioClip clip, ReplacerConfig config, string originalName)
        {
            if (clip == null || config == null) return;

            try
            {
                var go = new GameObject("SoundReplacerTrack");
                go.transform.position = position;

                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.clip = clip;
                source.volume = Mathf.Clamp01((float)config.TrackVolume);
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = Mathf.Max(0.1f, (float)config.MinDistance);
                source.maxDistance = Mathf.Max(source.minDistance + 0.1f, (float)config.MaxDistance);
                source.dopplerLevel = 0f;
                source.spread = 0f;

                source.Play();

                float tailSeconds = 0.5f;
                if (config.Echo)
                {
                    AudioEchoFilter echo = go.AddComponent<AudioEchoFilter>();
                    echo.delay = Mathf.Clamp((float)config.EchoDelayMs, 10f, 5000f);
                    echo.decayRatio = Mathf.Clamp((float)config.EchoDecayRatio, 0f, 0.95f);
                    echo.wetMix = Mathf.Clamp01((float)config.EchoWetMix);
                    echo.dryMix = Mathf.Clamp01((float)config.EchoDryMix);

                    // Эхо затихает не мгновенно: держим объект, пока не затухнут повторы.
                    tailSeconds += echo.delay * 0.001f * 6f;
                }

                // Источник убираем чуть позже конца клипа (затухание уже в сэмплах).
                Object.Destroy(go, clip.length + tailSeconds);

                ModLog.Info(string.Format(
                    "трек для '{0}': 3D-источник в {1}, дистанции {2:0.0}-{3:0.0} м{4}",
                    originalName, position, source.minDistance, source.maxDistance,
                    config.Echo ? ", эхо " + config.EchoDelayMs.ToString("0") + " мс" : ", без эха"));
            }
            catch (System.Exception ex)
            {
                ModLog.Warn("не удалось запустить 3D-трек для '" + originalName + "': " + ex.Message);
            }
        }
    }
}