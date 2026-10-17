# GTA Radio VIP

Замена радиостанции в **GTA V Enhanced** на внешний плеер (Яндекс.Музыка / Spotify)
без захвата и перевывода звука: плеер играет сам, мод работает как пульт.

Статус: **beta, требуется проверка в игре**. Схема сознательно не трогает аудиоустройства,
не захватывает процесс и не выводит собственный звук — поэтому не может давать гул/шум.

## Что делает

- Станция `RADIO_02_POP` переименована в **GTA Radio** (правка `mods/update/update.rpf`).
- Пока в машине выбрана GTA Radio, штатная музыка станции глушится (`SET_VEHICLE_RADIO_ENABLED`).
- При входе в машину плеер автоматически ставится на play, при выходе/переключении станции — на pause.
- На экране: `GTA Radio` / имя приложения / `Артист — Трек` из системных метаданных (SMTC).
- Клиент читает SMTC (Spotify, Яндекс.Музыка), пишет `%LOCALAPPDATA%\GtaRadio\state.txt`.

## Из чего состоит

```
app/GtaRadio/        клиент-трей (C# WinForms, .NET 8): SMTC -> state.txt
mod/GtaRadioMod/     SHVDN-мод (C#): станция, пауза/play, HUD
installer/           Inno Setup (по шаблону установщика озвучки)
tools/YtdProbe/      просмотр текстур YTD (логотип станции)
docs/MODS-MAP.md     разбор существующих радио-модов
```

## Сборка

Клиент:

```
cd app/GtaRadio
dotnet publish -c Release -r win-x64 --self-contained false ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out
```

Мод (нужен ScriptHookVDotNet 3.9 API, .NET Framework 4.8):

```
cd mod/GtaRadioMod
dotnet build -c Release
```

Установка в игру: `ScriptHookV.dll`, `dinput8.dll`, `xinput1_4.dll`,
`ScriptHookVDotNet.asi`, `ScriptHookVDotNet3.dll`, `GtaRadioMod.dll` в `scripts/`.
Бинарники SHV/SHVDN в репозиторий не входят — качаются с официальных сайтов.

## Переименование станции

Нужен `gta-patch` (CodeWalker-based, умеет Gen9 RPF). Правятся:

```
mods/update/update.rpf\x64\patch\data\lang\american_rel.rpf\global.gxt2
mods/update/update.rpf\x64\patch\data\lang\russian_rel.rpf\global.gxt2
```

Метка `RADIO_02_POP` = `0xB5DD6F19`, значение `Non-Stop-Pop FM` -> `GTA Radio`.
Оригиналы не трогаются: всё лежит в оверлее `mods/`.

## Чего в этой версии нет

- Логотип станции на колесе не заменён: текстуры `pop` в `radio_hud.ytd` нет,
  логотип лежит в Scaleform/UI-слоях.
- Установщик пока не автоматизирует RPF-правку и откат (лежит `installer/gta_radio.iss`).
- Автозапуск клиента вместе с игрой не подключён.

## Почему не захват звука

Проверены оба подхода. Захват процесса (WASAPI process loopback) технически работает,
но требует глушения исходной сессии и перевывода, из-за чего появлялись гул/наложение.
Рабочие моды (sahailee/GTA-Spotify-Radio, CiderGTA) звук не захватывают: они управляют
плеером и показывают метаданные. Этот репозиторий идёт тем же путём.

## Документация

- [docs/MODS-MAP.md](docs/MODS-MAP.md) — карта существующих радио-модов
- [docs/RESEARCH.md](docs/RESEARCH.md) — весь ресёрч: GXT2, RPF, текстуры, SMTC, захват звука
- [docs/HISTORY.md](docs/HISTORY.md) — история попыток и разбор ошибок
- [docs/PREFERENCES.md](docs/PREFERENCES.md) — требования и предпочтения
- [docs/BUILD.md](docs/BUILD.md) — сборка, установка, конфиг, проверка
