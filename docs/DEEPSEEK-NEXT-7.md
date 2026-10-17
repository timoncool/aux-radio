# DeepSeek: раунд 7 — колесо без «разъезжания», логотип, T3/T4

## Что изменено (Claude, по DEEPSEEK-RESULT-6)

1. **Колесо «разъезжалось»**: мод занял метку на 488 байт, а SHVDN добивает строку пробелами до её длины.
   Теперь мод собирает все метки треков с длиной и под каждый текст берёт метку ровно нужной длины
   (или чуть длиннее); при смене трека возвращает прежней метке её текст. В mod.log:
   `wheel: N track labels collected`, `wheel verify mode=Borrow ... ok=True labels=<ключ>/<ключ>`.
2. **Логотип**: `gta-patch extract` отдаёт ресурс без флагов страниц — поэтому YTD не грузился.
   Новый инструмент `src/GtaRadio.Logo` читает `hud.ytd` прямо из RPF через CodeWalker.Core
   (флаги берутся из записи архива), меняет пиксели только в прямоугольнике логотипа, пересобирает
   mip-уровни и сжатие через texconv в **тот же** формат (размер данных проверяется) и кладёт ресурс
   обратно в RPF через `RpfFile.CreateFile`.

## Шаг 1. Колесо (T2f)

Сборка с `-Deploy` (игра закрыта), `Output=Remote`. В машине на GTA Radio открыть колесо через 5 с.
Ожидание: артист и трек целиком, без бегущей строки из пробелов; короткие — по центру.
Сменить трек, переключиться на другую станцию и обратно. Отчёт `-Label T2f`, скриншоты.

## Шаг 2. Логотип

Подготовка:
- texconv: https://github.com/microsoft/DirectXTex/releases/latest/download/texconv.exe → `D:\Projects\_tools\texconv.exe`
- сборка: `dotnet build src\GtaRadio.Logo -c Release -p:CodeWalkerCore=D:\Projects\_tools\CodeWalker\CodeWalker.Core\CodeWalker.Core.csproj`
- `$L = "src\GtaRadio.Logo\bin\Release\net8.0-windows\GtaRadio.Logo.exe"`
- общие аргументы: `--game $g --rpf mods\update\update.rpf --entry x64\data\cdimages\scaleform_generic.rpf\hud.ytd`
  (если ключи не грузятся из exe — добавить `--key <base64 AES-ключ>`, тот же, что использует gta-patch)

1. `& $L list <общие>` — список текстур атласа (имена, размеры, формат, mip-уровни).
2. `& $L export <общие> --out D:\backup\logo_export` — на каждую текстуру: `.png`, `.dds`,
   `_boxes.png` (логотипы обведены и пронумерованы), `_boxes.txt`.
3. С пользователем по `_boxes.png` найти логотип Non-Stop-Pop: в цветном атласе и в сером/белом (если есть).
   Записать: имя текстуры и номер прямоугольника для каждого.
4. Бэкап: `mods\update\update.rpf` → `D:\backup\update.rpf.before_logo`.
5. `& $L patch <общие> --texconv D:\Projects\_tools\texconv.exe --out D:\backup\logo_patch `
   `--item <цветная_текстура>:<номер>:installer\art\gta_radio_logo_col.png --item <серая_текстура>:<номер>:installer\art\gta_radio_logo_bw.png`
   → в папке `*_patched.png` (показать пользователю) и `hud.ytd`; строка `[OK] ... textures back: N`.
6. `& $L put <общие> --file D:\backup\logo_patch\hud.ytd` → `[OK] written ...`.
7. Игра: колесо — на месте Pop наш логотип, остальные станции без изменений. Отчёт `-Label L1`, скриншот.
   Если игра падает/колесо пустое — вернуть бэкап из п.4, отчёт.

## Шаг 3. T3/T4

Как в DEEPSEEK-NEXT-4, шаг 4. Отчёты `-Label T3`, `-Label T4`. Лучший режим — по умолчанию в ini.

## Результат

`docs/DEEPSEEK-RESULT-7.md`, отчёты в коммит. Правила прежние; две неудачи — стоп, «нужен Opus».
