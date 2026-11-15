# 1.0.0

- Initial release.
- Harmony-based client-side sound replacement by `AudioClip` name (9 `AudioSource` hook points).
- Ogg Vorbis and WAV loading with validation; broken files fall back to the original sound.
- `catalog.json` with clip names and play counts for the companion app.
- Separate rotating log plus `BepInEx/LogOutput.log` output.
- Config: `enabled`, `patchClipSetter`, `exportCatalog`, `sounds` map.