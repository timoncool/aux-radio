# DeepSeek: раунд 4 — трек в колесе, Agent/Game, полный чек-лист

## Что изменено (Claude, по DEEPSEEK-RESULT-3)

- **Центр колеса.** Колесо берёт артиста и трек из меток `<id>A` / `<id>S` текущего трека станции.
  SHVDN Enhanced умеет перенаправлять метки (`Game.AddCustomGxtEntry`, это его хук `GxtMinHooked` из лога).
  Установщик теперь добавляет в `global.gxt2` две свои метки `GTARADIO_ARTIST` / `GTARADIO_TITLE`
  (127 пробелов). Мод в эфире перенаправляет метки трека Pop на них и пишет туда артиста и трек плеера.
  Вне эфира перенаправление снимается, Pop показывает свои треки.
- `MuteStrategies=Scene,Freeze` теперь по умолчанию (твоя проверка: Pop молчит, в колесе GTA Radio).
- Установщик и build.ps1 кладут `MinHook.x64.dll`; `ScriptHookVDotNet2.dll` не ставится (конфликт лога).
- `state.txt` в 2.0 не используется — его пишет старый клиент 1.x, его надо выключить (шаг 0).

## Шаг 0. Старый клиент 1.x

1. Закрыть старый `GtaRadio.exe` (1.x) через меню трея «Выход».
2. Убрать его из автозапуска, если он там есть (Диспетчер задач → Автозагрузка; ярлыки в
   `shell:startup`). Сам exe не удалять — переместить в `D:\backup\gtaradio_1x\`.
3. В микшере Windows у Яндекс Музыки и Spotify — не mute, не 0.

## Шаг 1. Сборка и деплой

```powershell
git pull
$g = "G:\GTA5\Grand.Theft.Auto.V.Enhanced-InsaneRamZes"
.\build.ps1 -GtaPatchDir "D:\Projects\TEMP\gta5-radio\tools\gta-patch" -ShvDir $g -Deploy $g
```

После деплоя `scripts\GtaRadio.Mod.ini` перезаписан новым (`MuteStrategies=Scene,Freeze`, `Output=Game`).
Убедись, что в корне игры нет `ScriptHookVDotNet2.dll`, а `MinHook.x64.dll` есть.

## Шаг 2. Метки колеса в update.rpf

1. Копия: `mods\update\update.rpf` → `D:\backup\update.rpf.before_r4`.
2. `powershell -ExecutionPolicy Bypass -File "$g\GtaRadio\tools\patch_station.ps1" -GameDir $g -DryRun`
   → в логе для обоих языков: `wheel labels updated (not written)`.
3. Без `-DryRun` → `'GTA Radio' -> 'GTA Radio', wheel labels present` для обоих языков.
4. Проверка: extract russian `global.gxt2` и `GtaRadio.Gxt2.exe get <файл> GTARADIO_ARTIST` → exit 0 (127 пробелов).
5. Отчёт `collect_diag.ps1 -Label R4-2`.

## Шаг 3. Remote + колесо (T2c)

`scripts\GtaRadio.Mod.ini`: `Output=Remote`. Музыка в Яндексе играет.
Игра → машина → GTA Radio → открыть колесо (удерживать Q/колесо).

Ожидание:
- центр колеса: **артист и трек из Яндекса** (не Black Eyed Peas);
- в mod.log: `wheel labels present: True`, `wheel track <число> -> GTA Radio labels`;
- смена трека в Яндексе → через ~1 с в колесе новый трек;
- переключиться на другую станцию → у неё свои треки; вернуться на GTA Radio → снова трек Яндекса.

Проверить заодно: выйти из машины → через ~1.2 с Яндекс на паузе; сесть → играет; Esc-пауза игры → Яндекс на паузе.
Отчёт `-Label T2c`.

## Шаг 4. Agent (T3) и Game (T4)

`Output=Agent`, потом `Output=Game`: тот же сценарий, плюс:
- звук без гула, эха и двойного звука; Яндекс в микшере почти на нуле, пока в эфире;
- agent.log: `[OK] capture pid=...`, строка `calibrate before=... after=... makeup=...`
  (для T3 ещё `[OK] agent output`; для T4 в mod.log `start: audio ok (game output)`);
- громкость меняется ползунком «Музыка» в настройках звука игры;
- после выхода из игры громкость Яндекса в микшере вернулась.

Отчёты `-Label T3`, `-Label T4`. Если звук с гулом/эхом/тихий — записать строку `calibrate` и не продолжать этот режим.

## Шаг 5. Полный чек-лист

В режиме, который прошёл лучше (Game → Agent → Remote), — таблица из `DEEPSEEK.md`, шаг 4, пункты 1–13.

## Результат

`docs/DEEPSEEK-RESULT-4.md` + все `diag\report_*.txt` в коммит. Код не чинить; если что-то не так —
факты, строки логов, скриншоты колеса.
