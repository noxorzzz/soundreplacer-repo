# Changelog

Все значимые изменения — здесь. Формат: [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/), версии — [SemVer](https://semver.org/lang/ru/).

## [1.0.1] — 2026-11-15

### Исправлено

- Сборка мода против реальных сборок игры: `netstandard.dll` из `Managed`, ссылки на Mono-сборки Unity 2022.3 (`-p:FrameworkPathOverride=…\REPO_Data\Managed`), `UnpatchSelf()` вместо устаревшего `UnpatchAll(id)`.
- Приложение теперь находит папку мода и в раскладке менеджеров Thunderstore/r2modman (`BepInEx/plugins/<Team>-SoundReplacer/`), а не только `BepInEx/plugins/SoundReplacer/`. Пути пересчитываются по «Обновить каталог».

### Добавлено

- Мод опубликован на Thunderstore: https://thunderstore.io/c/repo/p/noxorzzz/SoundReplacer/ (пакет `noxorzzz-SoundReplacer-1.0.0`).
- Заготовка Thunderstore-пакета: `thunderstore/manifest.json`, `icon.png` (256×256), `README.md`, `CHANGELOG.md`, скрипт `pack.ps1` для сборки и упаковки zip.
- Ссылка на релиз в README.

## [1.0.0] — 2026-11-15

### Добавлено

- Мод SoundReplacer для R.E.P.O.: BepInEx 5 + Harmony, чтение `config.json` при старте, замена `AudioClip` по имени через 9 точек `AudioSource` (`Play`, `Play(ulong)`, `PlayDelayed`, `PlayScheduled`, `PlayOneShot` ×2, `PlayClipAtPoint` ×2, сеттер `clip`).
- Загрузка `.ogg` через `UnityWebRequestMultimedia` и `.wav` собственным RIFF-декодером (PCM 8/16/24/32, IEEE float32, 1–2 канала, до 120 с, до 200 МБ).
- `catalog.json`: имена реально проигранных клипов + счётчик проигрываний, запись не чаще раза в 10 секунд и при выходе из игры.
- Отдельный лог `SoundReplacer.log` с ротацией; при отсутствующем/битом файле — WARN и оригинальный звук, без падения игры.
- WinForms-приложение: поиск игры (запущенный процесс → библиотеки Steam → вручную), список звуков из каталога, «Выбрать файл» на каждый звук, «Применить» (копирование в `Sounds/` + атомарная запись конфига), «Сбросить», предупреждение о запущенной игре и перезапуске.
- Общий слой `shared/` (мини-JSON, модель конфига, модель каталога) без NuGet-зависимостей — одинаковый код у мода и приложения.
- Дымовые тесты shared-слоя (26 проверок) и CI на GitHub Actions (тесты + сборка приложения, artifact `SoundReplacer.App-win-x64`).

### Известные ограничения

- Замена клиентская: в мультиплеере её слышат только игроки с тем же модом и теми же файлами; синхронизации нет.
- `loaforcsSoundAPI` под R.E.P.O. не портирован, поэтому используется Harmony-бэкенд; `ISoundBackend` оставлен точкой расширения под SoundAPI.
- Мод не перечитывает `config.json` на лету — изменения применяются после перезапуска игры.