# DeepSeek: проверка падения и запуск (раунд 2)

Разбор твоего отчёта `DEEPSEEK-RESULT.md` (Claude):

- `ScriptHookVDotNet.log` пишется сразу на диск без буфера, и первая строка при старте SHVDN —
  `[INFO] ScriptHookV .Net Enhanced 3.9.0.6 (1.1.0.6)` (исходник SHVDN Enhanced, `ScriptDomain.cs:301`).
  Лог не обновился → **SHVDN в тех запусках не стартовал вообще**. Скрипты из `scripts\` грузятся
  только после этого, значит игра падала **раньше, чем мог загрузиться GtaRadio.Mod.dll**.
- NAudio-библиотеки в `scripts\` SHVDN пропускает (нет ссылок на API скриптов) — тоже не причина.
- Что менялось перед падениями: `mods\update\update.rpf` (3 ГБ) несколько раз переписан через
  `gta-patch put` (переименование → откат → переименование). `global.gxt2` игра читает на загрузке.
  Главный подозреваемый — он. Это надо доказать или опровергнуть по шагам ниже.
- «Без мода не падает» в отчёте не подтверждено логом: не видно, дошла ли игра до сюжетного режима.

Мод тоже сделан безопаснее: лог с первой строки, в конструкторе ни нативов, ни звука
(всё на первом тике после загрузки), исключения в тике не роняют скрипт.
Добавлен режим `Output` в `scripts\GtaRadio.Mod.ini`: `Game` / `Agent` / `Remote`.

## Правила

1. **Одно изменение на запуск.** После каждого запуска — отчёт `collect_diag.ps1`. Не чинить код.
2. «Игра загрузилась» = персонаж управляется в сюжетном режиме и прошло 60 секунд.
   Закрывать игру через меню (Выйти из игры), не убивать процесс.
3. Если игра упала — тоже запустить `collect_diag.ps1` сразу после падения.
4. Все прежние правила из `DEEPSEEK.md` в силе (никакого HUD, legacy не трогать, только `mods\`, без Co-Authored-By).

Команда отчёта (после каждого шага, `-Label` — номер шага):

```powershell
$g = "G:\GTA5\Grand.Theft.Auto.V.Enhanced-InsaneRamZes"
powershell -ExecutionPolicy Bypass -File diag\collect_diag.ps1 -GameDir $g -Label T0
```

Файлы `diag\report_*.txt` — закоммитить.

## Подготовка

```powershell
git pull
.\build.ps1 -GtaPatchDir "D:\Projects\TEMP\gta5-radio\tools\gta-patch" -ShvDir $g -Deploy $g
```

После деплоя **выключи мод**: `scripts\GtaRadio.Mod.dll` → `scripts\GtaRadio.Mod.dll.off`
(если остался старый `.off` — замени новым). Пробник собран в `out\probe\GtaRadio.Probe.dll`, пока не копировать.
В `scripts\` не должно быть `GtaRadioMod.dll` (старый мод 1.x).

## T0. Чистая игра с текущим update.rpf (мод выключен)

Запуск → дождаться загрузки → 60 с → выйти → отчёт `T0`.

- Загрузилась **и** в `ScriptHookVDotNet.log` есть строка `ScriptHookV .Net Enhanced 3.9.0.6` с текущим временем → **T1**.
- Упала, или висит на загрузке > 5 мин, или SHVDN-лог не обновился → **T0b**.

## T0b. Вернуть update.rpf до правок gta-patch

1. Закрыть игру.
2. `mods\update\update.rpf` → `D:\backup\update.rpf.after_gtaradio` (сохранить текущий, не удалять).
3. `D:\backup\update.rpf.before_gtaradio` → `mods\update\update.rpf`.
4. Запуск → отчёт `T0b`.

- Загрузилась → причина в переписанном update.rpf. **Стоп**, `patch_station.ps1` больше не запускать,
  отчёт пользователю. Название станции в этом файле — то, что было до 2.0 (GTA Radio от версии 1.x).
- Снова падает → причина вне GTA Radio (SHV/SHVDN/другие asi/репак). Стоп, отчёт.

## T1. Пробник SHVDN

`out\probe\GtaRadio.Probe.dll` → `scripts\`. Запуск → загрузка → 60 с → выйти → отчёт `T1`.
Ожидание в `probe.log`: `static ctor`, `ctor`, `first tick`, `alive ticks=...` каждые 10 с.
После шага пробник **убрать** из `scripts\`.

## T2. Мод в режиме Remote (без захвата звука)

`scripts\GtaRadio.Mod.dll.off` → `GtaRadio.Mod.dll`. В `scripts\GtaRadio.Mod.ini`: `Output=Remote`.
Включить музыку в Яндекс Музыке. Запуск → загрузка → в машину → выбрать GTA Radio → 1 мин →
выйти из машины → сесть → переключить станцию → выйти из игры → отчёт `T2`.

Ожидание в `mod.log`: `ctor: start`, `ctor: ok`, `start: game loaded`, `start: link ok`, `[OK] loaded`,
`mute scene: ...`; в трее появился агент. В игре: штатная Pop не звучит, Яндекс играет сам
(через Windows, без захвата), уведомление с треком; выход из машины → Яндекс на паузе; сел — играет.

## T3. Режим Agent (захват, звук играет агент)

`Output=Agent`. Тот же сценарий → отчёт `T3`. Плюс: без гула/эха/двойного звука, Яндекс в микшере
почти на нуле, в agent.log строки `[OK] capture` и `calibrate ...`, `[OK] agent output`.

## T4. Режим Game (захват, звук играет игра) — основной

`Output=Game`. Тот же сценарий → отчёт `T4`. В mod.log `start: audio ok (game output)`.

## Дальше

Тот режим, который прошёл (по приоритету Game → Agent → Remote), оставить в ini и пройти полную
таблицу из `DEEPSEEK.md` (шаг 4, пункты 1–13). Результаты — в `docs/DEEPSEEK-RESULT-2.md`, отчёты `diag\report_*.txt` — в коммит.
