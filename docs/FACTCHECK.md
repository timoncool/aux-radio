# Проверка фактов и ссылок (17.10.2026)

Проверено по живым страницам и исходникам (GitHub, raw, gta5-mods, dev-c.com, alloc8or nativedb,
DurtyFree dumps, Microsoft Learn). Исправления внесены в RESEARCH.md, MODS-MAP.md, PREFERENCES.md.

## Ошибки в прежних заметках

| Где | Было | На самом деле |
|---|---|---|
| RESEARCH §6, HISTORY, CODE-NOTES | «захват не режется мьютом сессии» | Замер сделан с `ProcessTreeScope = 1` = `PROCESS_LOOPBACK_MODE_EXCLUDE_TARGET_PROCESS_TREE` (Microsoft Learn: INCLUDE = 0, EXCLUDE = 1). Захватывалось всё, кроме плеера. Вывод недействителен; по OBS захват приложения идёт после громкости микшера |
| HISTORY, RESEARCH §6 | причина гула — формат, двойное воспроизведение | Главная причина — EXCLUDE: клиент захватывал собственный вывод (петля) |
| legacy мод | HUD «не передавал трек» | HUD рисовался раз в 350 мс (draw-вызовы живут один кадр) — фактически невидим. HUD в задаче не было, в 2.0 удалён |
| legacy мод | `SET_VEHICLE_RADIO_ENABLED(false)` + проверка `GET_PLAYER_RADIO_STATION_NAME == RADIO_02_POP` каждый тик | после выключения станция читается как OFF -> мод сам снимал своё условие (дребезг). В 2.0 — логика владения образца |
| legacy мод | play/pause медиа-клавишей | `VK_MEDIA_PLAY_PAUSE` — переключатель для «текущего» приложения, может попасть не в тот плеер. В 2.0 — явные SMTC `TryPlay/TryPause` выбранной сессии |
| legacy клиент | «Удалить мод» удалял ScriptHookV.dll, SHVDN | общие файлы для других модов (в т.ч. кооп). В 2.0 установщик ставит SHV/SHVDN только если их нет и не удаляет |
| MODS-MAP §3 | bozbez/win-capture-audio — «process loopback -> виртуальное устройство» | плагин-источник OBS, виртуального устройства не создаёт (GPL-2.0, beta) |
| MODS-MAP, RESEARCH §7 | Internet Radio LS — «закрытый» | исходник открыт для чтения: github.com/trenkmannsteven-del/-Internet-Radio-LS (C# SHVDN Enhanced, All Rights Reserved) |
| MODS-MAP | sahailee — «открытый исходник» | исходник есть, LICENSE нет (все права защищены). CiderGTA — AGPL-3.0 |
| MODS-MAP | CiderGTA — «форк sahailee» | не форк, самостоятельный код по мотивам |
| MODS-MAP | LfxB — «добавляет станции в колесо, файлы/URL» | свои колёса поверх штатного; URL не подтверждён; SHVDN 2.x, 2019, Enhanced не заявлен |
| RESEARCH §1 | SHVDN Enhanced 1.1.0.6 «06.10.2026» (в coop-ТЗ) | релиз 1.1.0.6 — 15.07.2026; 06.10 — только коммит |
| RESEARCH §8 | `GET_PLAYER_RADIO_STATION_NAME` возвращает ID станции | верно при включённом радио; при OFF образец получает `"OFF"`/NULL |

## Подтверждено

- `RADIO_02_POP` = `0xB5DD6F19` (joaat, пересчитано).
- MichonGoddijn231849/gta-spotify-radio: C++ ASI, librespot, XAudio2, Enhanced, `Mode=Replace` на `RADIO_02_POP`,
  `MuteStrategies=Scene,Freeze,VehicleOff`, `SET_RADIO_POSITION_AUDIO_MUTE` — nullsub (по их README).
- Аудиосцены `CAR_MOD_RADIO_MUTE_SCENE`, `MP_JOB_CHANGE_RADIO_MUTE`, `MUTES_RADIO_SCENE` есть в DurtyFree
  `audioSceneNames.json`; `FBI_HEIST_H5_MUTE_RADIO_SCENE` из ini образца там отсутствует.
- sahailee: `dashboard` -> `SET_RADIO("", "Spotify Radio", artist, track)`; глушение — громкость Spotify Web API 0;
  станция — add-on `RADIO_47_SPOTIFY_RADIO` (Community HUD.gfx).
- Internet Radio LS: Spotify/YouTube Music управляются громкостью сессии в микшере; штатное радио — `RadioOff`.
- SiverKing/GTA5_CarRadioMusicMod: Python/PyLoaderV, медиа-клавиши, Legacy+Enhanced.
- op30mmd/MusicPlayerV: C++ ASI, FFmpeg/MF, XAudio2, yt-dlp, радио принудительно OFF.
- ScriptHookV v3889.0 (15.07.2026): 1.0.3889.0 / 1.0.1158.13. Билд 1.0.1158.16 в списке не значится.
- DurtyFree/gta-v-data-dumps#2: флага `DisableRadio` нет.
- Хеши всех используемых нативов сверены с alloc8or/gta5-nativedb-data `natives.json`.

## Не проверить извне

- AUMID Яндекс Музыки `ru.yandex.desktop.music` — снят на ПК пользователя; публично не подтверждается.
  Агент 2.0 не зависит от него: ищет процесс по известным именам exe, частям AppId и по реально звучащей сессии.
- Точный синтаксис `gta-patch put` — проверяется на ПК (DEEPSEEK.md, шаг 3).
