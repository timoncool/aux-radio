> **Устарело (версия 1.x, код в `legacy/`).** Актуально: [ARCHITECTURE.md](ARCHITECTURE.md), [DEEPSEEK.md](DEEPSEEK.md), [FACTCHECK.md](FACTCHECK.md).

# Сборка и установка

## Требования

- Windows 10/11 x64
- .NET SDK 8.0
- .NET Framework 4.8 (для SHVDN-мода)
- Inno Setup 6 (для установщика), например `F:\AI\InnoSetup\ISCC.exe`
- GTA V Enhanced с установленным ScriptHookV + ScriptHookVDotNet 3.9

## Клиент

```
cd app/GtaRadio
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out
```

Запуск: `GtaRadio.exe`. Режим по умолчанию — `ControlOnly=true`:
клиент не захватывает и не выводит звук, только читает SMTC.

Конфиг: `%LOCALAPPDATA%\GtaRadio\config.json`

```json
{
  "Enabled": true,
  "SourceAppId": "ru.yandex.desktop.music",
  "Volume": 1,
  "DuckVolume": 0.18,
  "ExitWithGame": true,
  "AllowAnyApp": false,
  "WaitForGameSeconds": 900,
  "GameDir": "G:\\GTA5\\Grand.Theft.Auto.V.Enhanced-InsaneRamZes",
  "OutputDevice": null,
  "ControlOnly": true
}
```

Состояние: `%LOCALAPPDATA%\GtaRadio\state.txt`

```
active=1
app=Яндекс Музыка
artist=Artist
title=Track
playing=1
```

## Мод

```
cd mod/GtaRadioMod
dotnet build -c Release
```

Скопировать `bin/Release/GtaRadioMod.dll` в `<game>\scripts\`.

Файлы в корне игры:

```
ScriptHookV.dll
dinput8.dll
xinput1_4.dll
ScriptHookVDotNet.asi
ScriptHookVDotNet3.dll
ScriptHookVDotNet.ini
scripts/GtaRadioMod.dll
```

Бинарники SHV/SHVDN скачиваются с официальных источников и в репозиторий не входят.

## Переименование станции

`gta-patch` (CodeWalker-based, Gen9):

```
gta-patch.exe extract <game> <game>/x64b.rpf <out> data/lang/russian_rel.rpf/global.gxt2
gta-patch.exe put     <game> <game>/mods/update/update.rpf <file>
```

Правятся оба языка в `mods/update/update.rpf`:

```
x64/patch/data/lang/american_rel.rpf/global.gxt2
x64/patch/data/lang/russian_rel.rpf/global.gxt2
```

Метка `RADIO_02_POP` (`0xB5DD6F19`): `Non-Stop-Pop FM` -> `GTA Radio`.

## Установщик

```
ISCC.exe installer\gta_radio.iss
```

Шаблон взят с установщика озвучки NeuroDub (тёмный мастер, арт, admin).
RPF-правка и откат пока делаются вручную; это следующий шаг.

## Проверка

1. Запустить игру, сесть в машину.
2. Включить станцию **GTA Radio**.
3. Ожидаемо: штатная музыка молчит, играет Яндекс.Музыка, внизу HUD с треком.
4. Выйти из машины — плеер ставится на паузу.
5. Переключить станцию — пауза, штатное радио возвращается.

Логи:

```
<game>\ScriptHookVDotNet.log
%LOCALAPPDATA%\GtaRadio\agent.log
%LOCALAPPDATA%\GtaRadio\state.txt
```
