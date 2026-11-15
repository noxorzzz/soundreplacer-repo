# SoundReplacer

Replace R.E.P.O. sounds with your own `.ogg` / `.wav` files — the game's files are never modified.

Client-side BepInEx 5 + Harmony mod: it swaps the `AudioClip` by name at play time. A small WinForms companion app lists the real clip names collected from your own play session, lets you pick a file for each one, and writes `config.json`.

## Features

- 9 hook points: `AudioSource.Play()`, `Play(ulong)`, `PlayDelayed`, `PlayScheduled`, `PlayOneShot` (2 overloads), `PlayClipAtPoint` (2), and the `clip` setter.
- Ogg Vorbis and WAV (PCM 8/16/24/32-bit or float32, 1–2 channels, up to 120 s).
- Missing or broken file never crashes the game: a warning is logged and the original sound plays.
- **Auto-fit to the original clip**: the file is trimmed to the original length, its peak is matched to the original loudness, and stereo is downmixed when the original is mono. The `AudioSource` is left untouched, so distance attenuation, 3D position, reverb and mixer effects stay exactly as vanilla. Toggle with `matchLength`, `matchVolume`, `matchChannels`.
- `noTrim`: names excluded from trimming, so a long track can replace a short jingle (quota honks, UI stingers).
- `oncePerFile`: the same file is not restarted within `onceWindowSeconds`, so a long track is not stacked three times when the game fires the jingle repeatedly.
- `fadeOutSeconds`: smooth fade-out at the end of the replaced clip.
- `spatial`: the track plays on its own 3D source at the event position — louder up close, quieter with distance (`minDistance`, `maxDistance`, `trackVolume`). It is unaffected by the game re-triggering the original sound.
- `echo`: optional `AudioEchoFilter` on that source (`echoDelayMs`, `echoDecayRatio`, `echoWetMix`, `echoDryMix`) — the track gets an echo tail, other sounds stay untouched.
- `lowPassHz` + `reverbPreset`: muffled, concrete-room sound matching the game — high-frequency rolloff and Unity reverb (`Room` by default in the example).
- `logPlays`: every played clip appears in the log with its length and channel count.
- `catalog.json` with real clip names, play counts, original `length` and `channels`, updated while you play.
- Own log with rotation plus `BepInEx/LogOutput.log`.
- Reset removes all replacements and returns vanilla sounds.

## Requirements

- R.E.P.O. (Steam, Windows x64, Unity Mono build)
- BepInEx 5 — installed automatically by the mod manager
- .NET 10 Desktop Runtime for the companion app

## Install

1. Install this package through r2modman / Thunderstore Mod Manager (or copy `SoundReplacer.dll` into `BepInEx/plugins/SoundReplacer/`).
2. Launch the game, play for a few minutes (shoot, break crates, take damage, open the menu), then quit. A `catalog.json` appears next to the DLL.
3. Download the companion app **SoundReplacer.App** from the [GitHub Releases](https://github.com/noxorzzz/soundreplacer-repo/releases/latest).
4. In the app: pick your game folder, choose a file for a sound, press **Apply**, restart the game.

The app finds the mod folder automatically, including Thunderstore/r2modman layouts (`BepInEx/plugins/<Team>-SoundReplacer/`).

## Config

`BepInEx/plugins/SoundReplacer/config.json`:

```json
{
  "version": 1,
  "enabled": true,
  "patchClipSetter": true,
  "exportCatalog": true,
  "matchLength": true,
  "matchVolume": true,
  "matchChannels": true,
  "logPlays": false,
  "noTrim": [],
  "oncePerFile": true,
  "onceWindowSeconds": 3.0,
  "fadeOutSeconds": 1.5,
  "spatial": ["extraction point alarm"],
  "minDistance": 3.0,
  "maxDistance": 40.0,
  "trackVolume": 1.0,
  "sounds": {
    "Sfx - Item Break": "Sounds/Sfx_Item_Break_1a2b3c4d5e6f7a8b.ogg"
  }
}
```

Keys are exact `AudioClip` names — take them from `catalog.json` or the app.

## Multiplayer

Replacement is **client-side only**. Other players hear your custom sound only if they have the same mod and the same files installed. There is no sync; the host is not required to have the mod. Voice chat is untouched.

## Links

- Source, issues, releases: [github.com/noxorzzz/soundreplacer-repo](https://github.com/noxorzzz/soundreplacer-repo)
- Changelog: see `CHANGELOG.md` in this package

## Notes

No R.E.P.O. assets are included or redistributed. Only replace audio you have the rights to use.