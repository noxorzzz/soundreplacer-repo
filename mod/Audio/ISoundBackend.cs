namespace SoundReplacer.Audio
{
    /// <summary>
    /// Точка расширения под разные движки подмены звука.
    ///
    /// Сейчас реализация одна — HarmonyAudioBackend (прямой патчинг AudioSource).
    /// Если/когда появится порт loaforcsSoundAPI под R.E.P.O., достаточно добавить
    /// SoundApiBackend : ISoundBackend и выбирать бэкенд в Plugin.Awake().
    /// Патчи Harmony при этом можно не устанавливать вовсе.
    /// </summary>
    public interface ISoundBackend
    {
        string Name { get; }

        bool Install(SoundReplacementManager manager);

        void Uninstall();
    }
}