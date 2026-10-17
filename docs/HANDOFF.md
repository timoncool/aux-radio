> **Устарело (версия 1.x, код в `legacy/`).** Актуально: [ARCHITECTURE.md](ARCHITECTURE.md), [DEEPSEEK.md](DEEPSEEK.md), [FACTCHECK.md](FACTCHECK.md).

# HANDOFF для Opus

## Задача

Довести до рабочего состояния замену радиостанции в **GTA V Enhanced 1.0.1158.16**
(репак InsaneRamZes) на Яндекс.Музыку/Spotify по схеме «пульт»:
плеер играет сам, мод управляет им и показывает трек, штатная станция молчит.
Обязательна совместимость с озвучкой и радио-дубляжом (правки только в `mods/`).

## Текущее состояние

- Станция `RADIO_02_POP` переименована в **AUX Radio** в
  `mods/update/update.rpf` (оба языка, GXT2, хеш `0xB5DD6F19`).
  Проверено извлечением и скриншотами пользователя.
- `mod/GtaRadioMod` собран и лежит в `<game>\scripts\GtaRadioMod.dll`.
  Умеет: глушить штатное радио на станции AUX Radio, HUD с треком,
  play/pause медиа-клавишей при входе/выходе из машины.
- `app/GtaRadio` (control-only) собран, запущен в трее, читает SMTC
  (`Spotify.exe`, `ru.yandex.desktop.music`) и пишет `state.txt`.
- SHV 3889.0 + SHVDN Enhanced 1.1.0.6 установлены.
- **В игре ещё не проверено**: поведение колеса при выключенном радио,
  реакция HUD, автозапуск клиента.

## Что известно и проверено

- `mods/x64b.rpf` игра не читает; работает `mods/update/update.rpf`.
- CodeWalker.Core не читает Gen9 RPF; рабочий инструмент — `gta-patch`
  (CodeWalker-based), команды `list/extract/put/relxml`.
- Захват звука процесса работает, но требует mute исходника и перевывода,
  из-за чего был гул/наложение. От него отказались.
- SMTC Яндекса появляется только после старта воспроизведения.
- Текстуры `pop` в `radio_hud.ytd` нет; логотип, вероятно, в Scaleform.

## Следующие шаги

1. Запустить игру, проверить: станция AUX Radio, тишина штатной музыки,
   HUD, play при входе в машину, pause при выходе.
2. Проверить, открывается ли колесо радио при `SET_VEHICLE_RADIO_ENABLED(false)`;
   если нет — включать радио на время удержания `VehicleRadioWheel` (уже есть в коде).
3. Логотип станции: искать в `hud.gfx` / `scaleform_generic.rpf`, заменить текстурой.
4. Установщик Inno Setup: встроить RPF-правку GXT2 и откат при удалении,
   автозапуск клиента вместе с игрой.
5. Убрать лишние ветки кода захвата звука или оставить как экспериментальную опцию.

## Файлы

```
app/GtaRadio/TrayApp.cs        SMTC -> state.txt, ControlOnly
app/GtaRadio/Sources.cs        список SMTC-сессий
app/GtaRadio/AppConfig.cs      конфиг, ControlOnly=true
mod/GtaRadioMod/GtaRadioMod.cs станция, radio off, HUD, медиа-клавиша
installer/gta_radio.iss        мастер установки
docs/RESEARCH.md               весь ресёрч и ошибки
docs/MODS-MAP.md               карта модов
docs/BUILD.md                  сборка/установка/конфиг
```

## Запуск для проверки

```
<game>\PlayGTAV.exe
%LOCALAPPDATA%\hermes\cache\scratch\pub\GtaRadio.exe
```

Логи: `<game>\ScriptHookVDotNet.log`, `%LOCALAPPDATA%\GtaRadio\agent.log`,
`%LOCALAPPDATA%\GtaRadio\state.txt`.

## Ограничения

- Никаких изменений в аудиоустройствах и настройках Windows.
- Никакого виртуального кабеля.
- Никаких правок оригинальных RPF — только оверлей `mods/`.
- Секретов в репозитории нет.
