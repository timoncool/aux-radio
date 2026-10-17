# DeepSeek: раунд 5 — колесо, T3/T4, поиск логотипа

## Что изменено (Claude, по DEEPSEEK-RESULT-4)

Пустой центр колеса: перенаправление меток работает (трек Pop исчез), но колесо не видит наш текст.
Вероятная причина — названия треков радио лежат в другой таблице текстов, чем наши метки в global.gxt2.
Мод теперь сам проверяет результат и сам переключается:

1. `mode=Redirect` — свои метки GTARADIO_*; через ~0.3 с проверка чтением `<id>A` так же, как читает игра.
2. Если проверка не прошла — `mode=Direct`: текст пишется прямо в метки трека Pop
   (длина ограничена исходной строкой, длинные имена обрежутся), вне эфира исходный текст возвращается.

## Шаг 1. Сборка и колесо (T2d)

```powershell
git pull
$g = "G:\GTA5\Grand.Theft.Auto.V.Enhanced-InsaneRamZes"
.\build.ps1 -GtaPatchDir "D:\Projects\TEMP\gta5-radio\tools\gta-patch" -ShvDir $g -Deploy $g
```

(игра должна быть закрыта). `scripts\GtaRadio.Mod.ini`: `Output=Remote`.
Игра → машина → GTA Radio → открыть колесо → скриншот центра колеса → сменить трек в Яндексе →
через 1–2 с снова открыть колесо → скриншот. Переключиться на другую станцию и обратно.

В mod.log нужны строки: `wheel track ... mode=...`, `wheel verify: ... seen='...' ok=...`,
при переключении — `switching to direct mode` и `wheel verify (direct): seen='...'`. Отчёт `-Label T2d`.

## Шаг 2. T3 и T4 (параллельно с колесом, независимы)

`Output=Agent`, затем `Output=Game` — сценарий из DEEPSEEK-NEXT-4, шаг 4 (звук без гула/эха,
строка `calibrate` в agent.log, ползунок «Музыка», громкость Яндекса вернулась после выхода).
Отчёты `-Label T3`, `-Label T4`.

## Шаг 3. Только поиск (ничего не менять)

```powershell
$p = "$g\GtaRadio\tools\gta-patch\gta-patch.exe"
& $p list $g "$g\mods\update\update.rpf" trackid
& $p list $g "$g\x64b.rpf" trackid
& $p list $g "$g\mods\update\update.rpf" hud.gfx
& $p list $g "$g\update\update.rpf" hud.gfx
& $p list $g "$g\x64b.rpf" hud.gfx
```

И: извлечь `russian_rel.rpf\global.gxt2` (как в шаге 2 раунда 4) и выполнить
`GtaRadio.Gxt2.exe get <файл> 1549A` — есть ли метки треков в global.gxt2 (exit 0) или нет (exit 2).
Вывод команд — в `docs/DEEPSEEK-RESULT-5.md`.

## Результат

`docs/DEEPSEEK-RESULT-5.md` + `diag\report_*.txt` в коммит. Код не чинить.
