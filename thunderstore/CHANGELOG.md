# 1.6.0

- Echo on the 3D track: `echo`, `echoDelayMs`, `echoDecayRatio`, `echoWetMix`, `echoDryMix`. An `AudioEchoFilter` is attached only to the track's own source, so nothing else changes.

# 1.5.0

- Fixed: stereo files were cut exactly in half — `AudioClip.samples` is frames per channel, the code divided it by the channel count.
- Fixed: a repeated trigger no longer swaps the clip for silence; that start is skipped, so a playing track is not interrupted.
- Added `spatial`: a track can play on its own 3D AudioSource at the event position with logarithmic distance falloff (`minDistance`, `maxDistance`, `trackVolume`).

# 1.4.0

- `fadeOutSeconds`: smooth fade-out at the end of a replaced clip, so a long track does not cut off abruptly.

# 1.3.0

- `oncePerFile` / `onceWindowSeconds`: repeated starts of the same file inside the window are muted, so a long track does not restart three times when the game fires the jingle several times (local + global, quota honks).

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