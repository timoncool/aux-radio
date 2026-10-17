# DEEPSEEK-RESULT-4: метки колеса и T2c

Дата: 2026-10-17. Выполнялся `docs/DEEPSEEK-NEXT-4.md`. Код мода не менялся.

## Шаг 0. Старый клиент 1.x

- Процесс `GtaRadio` не запущен.
- В автозапуске (HKCU/HKLM Run, Startup) записей GtaRadio нет.
- Копии `GtaRadio.exe` 1.x перемещены в `D:\backup\gtaradio_1x\`
  (`GtaRadio_game_v1.exe`, `GtaRadio_pub_v1.exe`). Из игры не удалялись.
- Mute снят: `fixed 2 sessions for Spotify`, `fixed 1 sessions for Яндекс`.

## Шаг 1. Сборка и деплой

- Первый `build.ps1 -Deploy` упал на копировании `scripts\*`: игра была запущена и держала
  `GtaRadio.Mod.dll`. После выхода из игры деплой прошёл.
- В корне игры: `MinHook.x64.dll` есть, `ScriptHookVDotNet2.dll` нет.
- Новый ini: `MuteStrategies=Scene,Freeze`, `Output=Game`.

## Шаг 2. Метки колеса

- Бэкап: `D:\backup\update.rpf.before_r4` (3 122 517 504 байта).
- Сухой прогон: `[DRY] american/russian 'GTA Radio' -> 'GTA Radio', wheel labels updated (not written)`.
- Реальный прогон: `[OK] american/russian 'GTA Radio' -> 'GTA Radio', wheel labels present`.
- Проверка `GtaRadio.Gxt2.exe get` на извлечённых `global.gxt2`:
  `GTARADIO_ARTIST` — exit 0 (127 пробелов), `GTARADIO_TITLE` — exit 0 (127 пробелов), оба языка.
- Попутно исправлен баг `patch_station.ps1`: `Has-Label` падал, потому что PowerShell 5.1
  считает stderr от `gxt2 get` (метка отсутствует) фатальной ошибкой. Теперь вызов идёт
  с `$ErrorActionPreference = "Continue"` и проверкой кода возврата. Правка только в проверке.
- Отчёт: `diag/report_R4-2_20261017_095805.txt`.

## Шаг 3. T2c — Remote и колесо

Конфиг: `Output=Remote`, `MuteStrategies=Scene,Freeze`.

Факты из `mod.log`:

```
09:58:43.011 [OK] loaded, station=RADIO_02_POP audio=False output=2
09:58:52.135 mute scene: CAR_MOD_RADIO_MUTE_SCENE
09:58:52.141 wheel labels present: True
09:58:52.143 wheel track 1549 -> GTA Radio labels
09:58:52.186 wheel track 1590 -> GTA Radio labels
09:59:30.528 wheel track 1498 -> GTA Radio labels
09:59:34.579 wheel track 1498 -> GTA Radio labels
09:59:34.586 wheel track 1590 -> GTA Radio labels
```

- Перенаправление меток включается (`wheel labels present: True`, `wheel track ... -> GTA Radio labels`).
- **Результат в игре:** в центре колеса остаётся только надпись **GTA Radio**, артист и трек
  не отображаются вообще (скриншот пользователя). Оригинальный трек Pop исчез — значит
  перенаправление работает, но текст в `GTARADIO_ARTIST/TITLE` не появляется.
- Уведомление игры при этом показывает правильный трек плеера
  (`GTA Radio: Надежда Кадышева - Пляшет веночек`).
- Отчёт: `diag/report_T2c_20261017_095959.txt`.

### Что проверить Opus

- `WriteLabel` пишет через указатель `GET_FILENAME_FOR_AUDIO_CONVERSATION`. Похоже, указатель
  указывает на копию/константу, и запись не влияет на строку, которую читает колесо.
  Альтернатива — `Game.SetLocalizedString` (с оговоркой про добивание пробелами) или
  `Game.AddCustomGxtEntry` для самих `GTARADIO_*` с текстом, а не только для `<id>A/S`.
- Проверить, что `_link.Artist/_link.Title` непустые в момент `UpdateWheel` (в Remote
  метаданные агент пишет в `SlowTick`, строка 225 `AgentApp.cs`).
- `state.txt` всё ещё от старого клиента 1.x (`active=0`, Spotify — Alaina Cross);
  в 2.0 он не используется, но может путать.

## Не сделано

- T3 (`Output=Agent`) и T4 (`Output=Game`) не запускались: сначала нужно решение по колесу.
- Пауза/play при выходе/входе в машину и Esc-пауза в T2c пользователем не подтверждены.

## Иконка станции (требование пользователя)

Проверены все три HUD-текстуры в `x64a.rpf`:

- `textures\radio_hud.ytd` — `wktt_col`, `massiveb_bw`, `lrr_bw`, `if99_bw`, `integrity_col`,
  `beat_bw`, `plr_bw`, `fusion_col`, `tuffgong_col`, `thejourney_col`, `vladivostok_col`,
  `thevibe_col`, `k109_bw`, `jnr_col`, `sanjuan_col`, `theclassics_bw`, `electrochoc_bw`, `lchc_bw`;
- `textures\radio_hud_colored.ytd` — те же `*_col` (16 штук), `pop` нет;
- `textures\radio_hud_noncolored.ytd` — те же `*_bw` (16 штук), `pop` нет.

Текстуры с именем `pop` нет ни в одной из них. Логотип Pop, вероятно, лежит в Scaleform
(`hud.gfx` / `scaleform_generic.rpf`), как в sahailee/GTA-Spotify-Radio и Community HUD,
либо иконка станции задаётся add-on станцией. Нужно решение Opus: править `hud.gfx`
или добавлять свою станцию с логотипом.
