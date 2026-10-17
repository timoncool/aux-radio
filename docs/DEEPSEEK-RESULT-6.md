# DEEPSEEK-RESULT-6: Borrow-колесо работает, логотип заблокирован Gen9 YTD

Дата: 2026-10-17. Задание `docs/DEEPSEEK-NEXT-6.md`.

## Шаг 1. T2e — колесо (Borrow)

Сборка `-Deploy` успешна, `Output=Remote`, игра запущена.

`mod.log`:

```
10:22:03.216 wheel track 1276 ('FEATHERS - Dark Matter') mode=Direct
10:22:03.222 wheel track 1242 ('CORONA - Rhythm Of The Night') mode=Direct
10:22:03.526 wheel verify mode=Direct seen='Сер - Свобода' ok=True
10:22:04.130 wheel borrow pair 0: 14604S (488 b), 10485S (199 b)
10:22:04.132 wheel track 1242 ('CORONA - Rhythm Of The Night') mode=Borrow
10:22:04.434 wheel verify mode=Borrow seen='Сергей Шнуров, Кипелов - Свобода' ok=True
```

В игре: колесо показывает **GTA Radio / Сергей Шнуров, Кипелов / Свобода** целиком.
Пользователь: «вроде работает, хоть и странно разъезжается, но сойдёт».
Отчёт: `diag/report_T2e_20261017_102302.txt`.

## Шаг 2. T3 / T4

Не запускались: пользователь остановил на логотипе, игра была занята проверкой колеса.
`Output` в ini остался `Remote`.

## Шаг 3. Логотип — БЛОКЕР

Что найдено:

- Логотипы станций в колесе лежат в Scaleform-атласах `hud.ytd`:
  `gtav_radio_stations_texture_512`, `gta_radio_stations_texture02_512`, `...03_128` ... `...12_128`.
- `hud.ytd` находится в `update.rpf\x64\data\cdimages\scaleform_generic.rpf\hud.ytd`
  (и в `x64b.rpf`).
- `radio_hud.ytd` / `radio_hud_colored.ytd` / `radio_hud_noncolored.ytd` логотипа Pop **не содержат**
  (проверено пробником, текстуры `pop` нет).
- В `hud.gfx` есть строки `RADIO_02_POP`, `GTAV_Radio_Stations_Texture_512`,
  `GTA_Radio_Stations_Texture02_512` и т.д. — колесо рисует логотип из этих атласов.

Что пробовал:

1. `YtdProbe` по `hud.ytd` из RPF — читает Gen9 нормально (список текстур), но только чтение.
2. Написал `tools/YtdTool` (CodeWalker.Core): экспорт текстур в DDS и импорт обратно.
   `gta-patch extract` отдаёт **распакованный ресурс без RSC7-заголовка**
   (`InvalidDataException: unsupported compression method` при `YtdFile.Load`).
   Попытка загрузить через `YtdFile.Load(data, new RpfResourceFileEntry())` даёт пустой
   `TextureDict` (0 текстур) — заголовки/флаги ресурса неизвестны.
3. Прямой путь: грузить `hud.ytd` через `RpfFile` CodeWalker с ключами
   (`GTA5Keys.LoadFromPath(game, true, base64key)`), как делает `gta-patch`.
   Это следующий шаг, но он требует отдельного кода записи ресурса в RPF
   (`RpfFile` + `RpfResourceFileEntry` + RSC7-заголовок), то есть архитектурной работы.

Итог: заменить логотип мелкими правками не удалось. Нужен Opus: либо инструмент
на CodeWalker RPF с ключами, либо готовый редактор Gen9 YTD/Scaleform.

Готовые файлы логотипа лежат в `installer/art/gta_radio_logo_col.png` и `gta_radio_logo_bw.png`.

## Шаг 4. Установщик

- `GtaRadio_Setup_2.0.exe` собран (93 558 788 байт,
  sha256 `a2fbebef49a2f7fc71d0afb4be39adc44fbfbe660a694747feaaca1ef831c074`).
- Тихая установка поверх выполнена: `Installation process succeeded`, файлы и ini на месте,
  SHV/SHVDN/MinHook не тронуты (`onlyifdoesntexist`).
- Удаление и повторная установка **не проверены** — остановились на логотипе.

## Состояние

- Игра закрыта.
- `scripts\GtaRadio.Mod.dll` — сборка 10:20 (Borrow), `Output=Remote`.
- `mods\update\update.rpf` — с метками `GTARADIO_*` (Borrow их не использует, но патч их оставляет).
- `tools/YtdTool` — новый инструмент, в репозиторий не коммитился (код в рабочей копии).

## Нужен Opus

Логотип: Gen9 YTD/Scaleform, нужен рабочий способ замены текстуры в `hud.ytd`
(или готовый редактор). После этого — T3/T4 и полный тест установщика.
