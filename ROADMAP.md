# Дорожная карта SoundReplacer

**Статус на 2026-11-15:** версия `1.0.0` опубликована — [GitHub](https://github.com/noxorzzz/soundreplacer-repo) и [Thunderstore](https://thunderstore.io/c/repo/p/noxorzzz/SoundReplacer/), CI зелёный, приложение в релизе `v1.0.1`.

Живые задачи — в [Issues](https://github.com/noxorzzz/soundreplacer-repo/issues), вехи — [Milestones](https://github.com/noxorzzz/soundreplacer-repo/milestones).

---

## 1. Сейчас: довести 1.0.0 до состояния «проверено» (веха `v1.0.x`)

| # | Задача | Кто делает | Оценка | Результат |
|---|---|---|---|---|
| [#1](https://github.com/noxorzzz/soundreplacer-repo/issues/1) | Протестировать мод в игре, снять скриншоты | вы (нужна игра) | 30–40 мин | подтверждение в логе + картинки в README |
| [#2](https://github.com/noxorzzz/soundreplacer-repo/issues/2) | Social preview: `docs/banner.png` | вы | 2 мин | превью ссылки в Discord/Telegram |
| [#3](https://github.com/noxorzzz/soundreplacer-repo/issues/3) | Английский README | я | 40 мин | страница понятна не-русским игрокам |
| [#4](https://github.com/noxorzzz/soundreplacer-repo/issues/4) | Пройти чек-лист установки «с нуля» | вы | 20 мин | инструкция без дыр |

**Минимум, чтобы считать релиз состоявшимся:** #1 (иначе мод не проверен в бою) и #2 (иначе ссылка выглядит пустой).

---

## 2. Следующая версия 1.1 (веха `v1.1`)

| # | Задача | Зачем | Оценка |
|---|---|---|---|
| [#5](https://github.com/noxorzzz/soundreplacer-repo/issues/5) | Горячая перезагрузка `config.json` | менять звук без перезапуска игры | 2–3 ч |
| [#6](https://github.com/noxorzzz/soundreplacer-repo/issues/6) | Поиск и группировка звуков | каталог легко перерастает 200 имён | 2 ч |
| [#7](https://github.com/noxorzzz/soundreplacer-repo/issues/7) | Автопубликация в Thunderstore из CI | релиз одной командой | 3 ч |
| [#8](https://github.com/noxorzzz/soundreplacer-repo/issues/8) | Валидация пакета в CI | не выпустить битый manifest | 1–2 ч |
| [#9](https://github.com/noxorzzz/soundreplacer-repo/issues/9) | Английский интерфейс приложения | половина аудитории не читает по-русски | 3 ч |

Порядок: #7 и #8 (инфраструктура) → #5 (главная фича) → #6 и #9 (удобство).

---

## 3. Идеи без вехи (обсуждаем, не обещаем)

- предпрослушивание файла прямо в приложении (плеер в строке);
- профили замен: «хоррор-пак», «мемы», «тишина» — переключение одним кликом;
- конвертация Ogg Opus → Vorbis на импорте (сейчас Opus отклоняется);
- поддержка `.flac` через декодер в приложении;
- публикация на Nexus Mods;
- CLI-режим приложения: `SoundReplacer.App --apply config.json` для скриптов;
- проверка обновлений мода из приложения (GitHub Releases API).

---

## 4. Ветки и правила

```text
main                  стабильная ветка, сюда только через PR
feature/<slug>        новая функциональность (пример: feature/hot-reload)
fix/<slug>            исправления (пример: fix/topics-null)
docs/<slug>           документация
chore/<slug>          CI, зависимости, мелочи
```

- Коммиты — короткие, в императиве: `добавить поиск по звукам`, `починить поиск папки мода`.
- Каждая ветка = одна задача из Issues; в PR пишем `Closes #5`.
- Теги — `vX.Y.Z`, только на `main`, после зелёного CI.
- Ломающие изменения — только в `v2.0.0` с разделом «Миграция» в CHANGELOG.

---

## 5. Чек-лист релиза

1. Поднять версию: `mod/Plugin.cs` (`PluginVersion`), `app/SoundReplacer.App.csproj`, `thunderstore/manifest.json`, `CHANGELOG.md`.
2. `dotnet build app/... -c Release` и `tests/SharedSmoke` — локально.
3. `git tag vX.Y.Z && git push origin main --tags`, дождаться зелёного CI.
4. GitHub Release: `SoundReplacer.App-win-x64.zip` из артефакта CI + `config.example.json`.
5. `cd thunderstore; .\pack.ps1 -GameDir "<игра>" -BepInExCore "<ядро BepInEx>" -Version X.Y.Z`.
6. Загрузить zip в Thunderstore (веб или API), проверить страницу и иконку.
7. Обновить бейдж версии Thunderstore в README.