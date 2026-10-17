# DEEPSEEK-RESULT-2: T0 и T0b

Дата: 2026-10-17. Выполнялся `docs/DEEPSEEK-NEXT.md`. Код не менялся.

## Подготовка

- `git pull` выполнен, сборка `build.ps1 -Deploy` успешна, установщик `GtaRadio_Setup_2.0.exe` собран.
- Мод выключен: `scripts\GtaRadio.Mod.dll.off`.
- Старый `GtaRadioMod.dll` удалён.
- Пробник собран: `out\probe\GtaRadio.Probe.dll`, в `scripts` не копировался.

## T0 — чистая игра, текущий update.rpf, мод выключен

- Игра запущена, пользователь загрузил сюжет, играл, вышел через меню. **Падений нет.**
- `ScriptHookV.log`: `INIT: Success, game version is VER_EN_1_0_1158_13`,
  `Registering script 'NativeTrainer.asi'`, `Registering script 'ScriptHookVDotNet.asi'`.
- `ScriptHookVDotNet.log` **не обновился**: остался от 07:42, размер 986 байт.
- `mod.log` и `agent.log` не создавались.
- Отчёт: `diag/report_T0_20261017_085853.txt`.

Вывод T0: игра жива, но SHVDN не инициализируется. По правилам перешёл к T0b.

## T0b — возврат update.rpf из бэкапа

- Текущий `mods\update\update.rpf` сохранён как `D:\backup\update.rpf.after_gtaradio`.
- `D:\backup\update.rpf.before_gtaradio` возвращён в `mods\update\update.rpf` (3 122 517 504 байта).
- Игра запущена, пользователь загрузил сюжет, играл, вышел. **Падений нет.**
- `ScriptHookVDotNet.log` снова **не обновился** (тот же файл от 07:42).
- Отчёт: `diag/report_T0b_fixed_20261017_091148.txt`.

Вывод T0b: причина не в `update.rpf` — с откатанным файлом поведение то же.

## Дополнительно найденное

1. При чистке ранее был удалён `MinHook.x64.dll` из корня игры. Без него SHVDN-мод
   не грузится. Файл возвращён из `vendor\shvdn\MinHook.x64.dll`.
2. После возврата MinHook в процессе игры появились модули:
   `ScriptHookVDotNet.asi` и `MinHook.x64.dll` (проверено списком модулей процесса).
3. Но `ScriptHookVDotNet.log` всё равно не пишется. То есть ASI загружается,
   а домен SHVDN не инициализируется (или падает до первой записи в лог).
4. Хеши `ScriptHookVDotNet.asi` и `ScriptHookVDotNet3.dll` совпадают во всех копиях
   (vendor, installer payload, app payload, игра):
   - ASI: `c702e54a7e806acb8bb21de807ff3747e6c24269ce71797aa81e21580d14a08e`
   - DLL: `1e4d0bc4d34d05a6038fc87eede492d3dc4265522de473ec19cdf6746b5b0e48`
5. .NET Framework: Release `533320` (4.8.1).
6. `ScriptHookV.log` каждый запуск обновляется, `ScriptHookVDotNet.log` — нет.

## Почему остановился

По `DEEPSEEK-NEXT.md`: если в T0/T0b лог SHVDN не обновляется — стоп и отчёт.
T1 (пробник) не запускался, потому что без работающего SHVDN он тоже не загрузится.

## Что нужно от Opus

- Решить, почему SHVDN Enhanced 1.1.0.6 не инициализируется на этой сборке,
  хотя ASI загружен и .NET 4.8.1 есть.
- Варианты для проверки: свежая копия SHVDN Enhanced, запуск с открытой консолью SHVDN (F4),
  проверка `ScriptHookVDotNet.log` при пустом `scripts\`, версия game 1158.16 против SHV 1158.13.
- После работающего SHVDN — T1 (пробник), затем T2/T3/T4 по плану.

## Состояние

- Игра стабильна, мод выключен (`.off`), `update.rpf` — из `before_gtaradio`.
- `MinHook.x64.dll` в корне игры.
- Все отчёты `diag/report_*.txt` в репозитории.
