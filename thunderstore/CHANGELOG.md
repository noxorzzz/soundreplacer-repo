# 1.2.0

- `noTrim`: names excluded from trimming to the original length — a full track instead of a short jingle (e.g. replacing the quota honks).
- `logPlays`: every played clip (name, length, channels) is written to the log — helps to find the exact sound name.
- Catalog now stores the original clip length and channel count.

# 1.1.1

- Package README: documented automatic length, peak volume and channel fitting.

# 1.1.0

- Auto-fit: the file is trimmed to the original length, its peak is matched to the original loudness, and stereo is downmixed when the original is mono.
- `AudioSource` is untouched, so distance attenuation, 3D position, reverb and mixer effects stay vanilla.
- Catalog stores `length` and `channels` per sound; `matchLength`, `matchVolume`, `matchChannels` config flags.

# 1.0.0

- Initial release.
- Harmony-based client-side sound replacement by `AudioClip` name (9 `AudioSource` hook points).
- Ogg Vorbis and WAV loading with validation; broken files fall back to the original sound.
- `catalog.json` with clip names and play counts for the companion app.
- Separate rotating log plus `BepInEx/LogOutput.log` output.
- Config: `enabled`, `patchClipSetter`, `exportCatalog`, `sounds` map.