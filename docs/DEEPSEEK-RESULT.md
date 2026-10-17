# Результат выполнения DEEPSEEK.md (для Opus)

Кто выполнял: агент-руки на ПК (DeepSeek). Дата: 2026-10-17.
Задача: собрать v2.0 по `docs/DEEPSEEK.md`, поставить, проверить в игре, вернуть факты.
Ничего в архитектуре не менял. Ниже — только факты, логи и точные места.

## 1. Сборка

Команда:

```powershell
.\build.ps1 -GtaPatchDir "D:\Projects\TEMP\gta5-radio\tools\gta-patch" `
            -ShvDir "G:\GTA5\Grand.Theft.Auto.V.Enhanced-InsaneRamZes" `
            -Deploy "G:\GTA5\Grand.Theft.Auto.V.Enhanced-InsaneRamZes"
```

Результат: успешно, установщик собран:

```
installer\output\GtaRadio_Setup_2.0.exe
```

Проблема при первом запуске: `build.ps1` сохранён в UTF-8 **без BOM**, PowerShell 5.1
неверно читает кириллицу и падает на строке 78 (`TerminatorExpectedAtEndOfString`).
Лечение: пересохранил `build.ps1` в UTF-8 **с BOM**. Код не менялся.

После `-Deploy` в игре:

```
scripts\GtaRadio.Mod.dll
scripts\GtaRadio.Mod.ini
scripts\NAudio.Core.dll
scripts\NAudio.Wasapi.dll
GtaRadio\GtaRadio.Agent.exe
GtaRadio\tools\GtaRadio.Gxt2.exe
GtaRadio\tools\gta-patch\...
GtaRadio\tools\patch_station.ps1
```

## 2. Проверка GXT2-утилиты (шаг 2)

`gta-patch extract` заработал только с полным путём внутри RPF:

```
update.rpf\x64\patch\data\lang\russian_rel.rpf\global.gxt2
```

(без префикса `update.rpf\` — `FileNotFoundException`).

Извлечённый файл: 286 242 байта (russian), 127 817 (american).

```
GtaRadio.Gxt2.exe get <file> RADIO_02_POP
=> GTA Radio
```

Оба языка — `GTA Radio`. Утилита на настоящем файле работает.

## 3. Правка имени станции (шаг 3)

Первая версия `patch_station.ps1` падала на extract: в `Extract-Entry` из пути срезался
префикс `update.rpf\`, а `gta-patch` требует полный entry. Исправлено:

```powershell
& $gtaPatch extract $GameDir $fromRpf $outDir (Entry $lang)
$file = Get-ChildItem -Path $outDir -Recurse | Where-Object { $_.Name -like "*global.gxt2" } | Select-Object -First 1
```

Вторая проблема: при уже переименованной станции скрипт брал «оригинал» из
`update\update.rpf`, но там метки `RADIO_02_POP` **нет** (`label not found`).
Оригинал лежит в `x64b.rpf`. Добавлено:

```powershell
function EntryBase($lang) { "x64b.rpf\data\lang\${lang}_rel.rpf\global.gxt2" }
$vanilla = Extract-Entry $lang (...) (Join-Path $GameDir "x64b.rpf") (EntryBase $lang)
```

Проверено на реальном `mods\update\update.rpf` (бэкап:
`D:\backup\update.rpf.before_gtaradio`, 3 122 517 504 байта):

- `-DryRun`: `original saved: 'Non-Stop-Pop FM'`, оба языка `already 'GTA Radio'`.
- `-Uninstall`: `'GTA Radio' -> 'Non-Stop-Pop FM'`, независимая проверка извлечением — `Non-Stop-Pop FM`.
- повторный запуск без ключей: `'Non-Stop-Pop FM' -> 'GTA Radio'`, проверка — `GTA Radio` (оба языка).

То есть установка/откат имени работают, озвучка не затрагивается.

## 4. Проверка в игре (шаг 4) — БЛОКЕР

С модом `scripts\GtaRadio.Mod.dll` игра **падает**.

Факты:

- Запуск: `GTA5_Enhanced` стартует, окно «Grand Theft Auto V».
- `ScriptHookV.log`: `INIT: Success, game version is VER_EN_1_0_1158_13`,
  `Registering script 'ScriptHookVDotNet.asi'` — SHV загружается.
- `ScriptHookVDotNet.log` **не обновляется** (остаётся от 07:42), процесса
  `GtaRadio.Agent` нет, `%LOCALAPPDATA%\GtaRadio\mod.log` **не создаётся**.
- Через некоторое время процесс `GTA5_Enhanced` исчезает (падение/закрытие).
- Без `GtaRadio.Mod.dll` (переименован в `.off`) игра **не падает**, процесс живёт.

Вывод: падение происходит на этапе загрузки/инициализации мода, до первой записи
`mod.log`. `ScriptHookVDotNet.log` не успевает обновиться, поэтому исключение
не попадает в лог.

### Что пробовал (по указанию «не чинить» — откатил)

Гипотеза: `WasapiOut`/нативы в конструкторе скрипта валят игру на старте.
Сделал патч: конструктор только читает ini, инициализация (`GamePlayer.Create`,
`LOCK_RADIO_STATION`, `SET_USER_RADIO_CONTROL_ENABLED`) перенесена в первый `OnTick`
после появления `Game.Player.Character`, весь тик обёрнут в try/catch.
Собрал (0 ошибок), поставил — игра **всё равно упала**, `mod.log` снова не появился.
Патч откатил (`git checkout -- src/GtaRadio.Mod/StationMod.cs`), DLL убрал из игры
(лежит как `scripts\GtaRadio.Mod.dll.off`), игра оставлена стабильной.

### Что это значит для Opus

Падение не похоже на ошибку в логике станции: мод не успевает даже записать
`[OK] loaded`. Проверять в таком порядке:

1. Минимальный мод: пустой `Script` с одной строкой `File.AppendAllText(...)` в конструкторе.
   Если игра падает — проблема в загрузке сборки (net48/NAudio/依赖), не в коде станции.
2. Убрать `NAudio.*.dll` из `scripts` и собрать мод без `GamePlayer` — проверить,
   не валит ли игра загрузку NAudio через SHVDN.
3. Проверить, не конфликтует ли `GtaRadio.Mod.dll` с `NativeTrainer.asi` (он в логе есть).
4. Логи SHVDN могут не сбрасываться при падении — добавить запись в файл **до** любых
   вызовов нативов, в статическом конструкторе или в первых строках экземпляра.
5. Смотреть `%LOCALAPPDATA%\CrashDumps` и Event Log (`Application Error`) на момент падения:
   при последнем запуске свежих записей по GTA5 не было, но проверять после каждого запуска.

## 5. Состояние на момент передачи

- Игра: мод **отключён** (`scripts\GtaRadio.Mod.dll.off`), игра стабильна.
- Имя станции: **GTA Radio** (оба языка), проверено.
- `GtaRadio.Agent.exe` в игре есть, но не запускался (его поднимает мод).
- `GtaRadio_Setup_2.0.exe` собран.
- `patch_station.ps1` и `build.ps1` поправлены (см. выше), не закоммичены на момент отчёта.
- Старый код v1 — в `legacy/`, не использовался.

## 6. Логи и пути

```
G:\GTA5\Grand.Theft.Auto.V.Enhanced-InsaneRamZes\ScriptHookV.log
G:\GTA5\Grand.Theft.Auto.V.Enhanced-InsaneRamZes\ScriptHookVDotNet.log
%LOCALAPPDATA%\GtaRadio\mod.log        (не создан)
%LOCALAPPDATA%\GtaRadio\agent.log      (не создан)
G:\...\GtaRadio\patch_station.log      (есть, установка/откат)
D:\backup\update.rpf.before_gtaradio   (бэкап)
```

## 7. Что вернуть Opus

- Ответ на гипотезу: падает ли минимальный мод без NAudio.
- Если минимальный мод живёт — интегрировать `GamePlayer` по частям
  (сначала shared memory без WasapiOut, потом WasapiOut).
- После стабильного запуска — пройти таблицу 13 пунктов из `docs/DEEPSEEK.md`.
