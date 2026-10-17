# DeepSeek: раунд 3 — запустить SHVDN

## Что известно (разбор DEEPSEEK-RESULT-2, Claude)

- В 07:42 SHVDN работал: полный лог, загружен старый мод. В 07:59 в корне игры перезаписаны
  `ScriptHookV.dll`, `dinput8.dll`, `xinput1_4.dll`, `args.txt`, `NativeTrainer.asi`,
  `ScriptHookVDotNet.asi/.ini/3.dll`, а `MinHook.x64.dll` был удалён. С тех пор лог SHVDN не пишется.
- `ScriptHookVDotNet.asi`, `ScriptHookVDotNet3.dll`, `.ini`, `MinHook.x64.dll` у тебя совпадают
  с официальным релизом SHVDN Enhanced 1.1.0.6 байт в байт (Claude скачал архив и сверил sha256).
  Не хватает только `ScriptHookVDotNet2.dll` (в 07:42 SHVDN работал без неё, но релиз требует ставить
  asi + 2.dll + 3.dll вместе).
- По исходнику SHVDN: если записать в лог не удалось, ошибка **молча** игнорируется.
  Значит «лог не обновился» = либо SHVDN не стартует, либо файл лога нельзя записать
  (атрибуты/права после действий от администратора). Отличить можно в игре: **F4 открывает консоль
  SHVDN только если SHVDN работает**.
- update.rpf не виноват (T0b). Наш мод тоже ни при чём — он выключен.

## R3-0. Отчёт «как есть»

```powershell
git pull
$g = "G:\GTA5\Grand.Theft.Auto.V.Enhanced-InsaneRamZes"
powershell -ExecutionPolicy Bypass -File diag\collect_diag.ps1 -GameDir $g -Label R3-0
```

В отчёте теперь есть: сверка файлов SHVDN с официальным релизом, запись в `ScriptHookVDotNet.log`
от текущего пользователя, его атрибуты и права, VC++ runtime, версия .NET.

## R3-1. Привести SHVDN к официальной установке

1. Скачать официальный архив:
   https://github.com/Chiheb-Bacha/ScriptHookVDotNetEnhanced/releases/download/v1.1.0.6/ScriptHookVDotNetEnhanced-v1.1.0.6.zip
2. Скопировать в корень игры **все** файлы из корня архива: `ScriptHookVDotNet.asi`,
   `ScriptHookVDotNet2.dll`, `ScriptHookVDotNet3.dll`, `ScriptHookVDotNet.ini`, `MinHook.x64.dll`
   (с заменой; старые — в `D:\backup\shvdn_before_r3\`). Папки Docs/Debug/Licenses и README не нужны.
3. Старый лог: `ScriptHookVDotNet.log` → `D:\backup\shvdn_before_r3\` (не удалять). SHVDN создаст новый.
4. `NativeTrainer.asi` (появился в 07:59, нам не нужен) → `D:\backup\shvdn_before_r3\`.
5. Если в R3-0 VC++ runtime = NOT FOUND — поставить
   https://aka.ms/vs/17/release/vc_redist.x64.exe (требование SHVDN Enhanced).
6. Ничего не запускать от администратора, кроме установки VC++.

## R3-2. Проверка SHVDN

Мод по-прежнему выключен (`.off`), в `scripts\` только NAudio и ini — это нормально.
Запуск игры обычным способом → загрузиться в сюжет → в игре нажать **F4**.

- Открылась консоль SHVDN (полоска с вводом сверху) → SHVDN работает. Закрыть F4, выйти из игры.
- Не открылась → SHVDN не стартует.

Отчёт: `collect_diag.ps1 -Label R3-2`. В отчёт (`docs/DEEPSEEK-RESULT-3.md`) записать: открылась ли консоль,
есть ли свежий `ScriptHookVDotNet.log` и его первые строки.

## R3-3. Если SHVDN заработал — дальше по DEEPSEEK-NEXT.md

T1 (пробник) → T2 (Remote) → T3 (Agent) → T4 (Game), как там описано, с отчётом после каждого шага.

## Если SHVDN не заработал

Стоп. Отчёт R3-2 и `docs/DEEPSEEK-RESULT-3.md`. Ничего больше не менять.

## Правила

Как раньше: одно изменение на запуск, всё старое — в бэкап, а не в удаление; код не чинить;
отчёты `diag\report_*.txt` и `docs/DEEPSEEK-RESULT-3.md` — в коммит.
