# DEEPSEEK-RESULT-3: SHVDN поднят, T1 и T2

Дата: 2026-10-17. Выполнялся `docs/DEEPSEEK-NEXT-3.md`. Код не менялся.

## R3-0. Отчёт «как есть»

- `ScriptHookVDotNet2.dll` — MISSING, остальные файлы SHVDN совпали с официальным релизом.
- Лог писать можно (`append: OK`), ACL в порядке, игра от администратора.
- VC++ 2015-2022 x64: установлен, `v14.51.36247.00`.
- .NET Framework: `533320` (4.8.1).
- `ScriptHookVDotNet.log` на момент отчёта старый (07:42).
- Отчёт: `diag/report_R3-0_20261017_091514.txt`.

## R3-1. Официальная установка SHVDN

Скачан `ScriptHookVDotNetEnhanced-v1.1.0.6.zip`, в корень игры положены:
`ScriptHookVDotNet.asi`, `.ini`, `ScriptHookVDotNet2.dll`, `ScriptHookVDotNet3.dll`, `MinHook.x64.dll`.
Старые файлы, старый лог и `NativeTrainer.asi` — в `D:\backup\shvdn_before_r3\`.

## R3-2. Проверка

- Игра запущена, загружен сюжет, **F4 открыл консоль SHVDN** (скриншот пользователя:
  `--- Script Hook V .Net Enhanced 3.9.0.6 (1.1.0.6) ---`, приглашение `$`).
- `ScriptHookVDotNet.log` создан заново: `09:16:08`, загрузка `ScriptHookVDotNet2.dll` и `3.dll`,
  созданы хуки. Отчёт: `diag/report_R3-2_20261017_091630.txt`.
- Обнаружено: при наличии **обоих** API (`2.dll` и `3.dll`) SHVDN не может писать лог —
  `IOException: процесс не может получить доступ к файлу ...ScriptHookVDotNet.log`.
  В рабочем логе 07:42 грузился только `3.dll`.
  `ScriptHookVDotNet2.dll` убран в бэкап, после этого лог пишется нормально.

## T1. Пробник

- `GtaRadio.Probe.dll` в `scripts`, мод выключен.
- Игра загрузилась, пробник отработал. `%LOCALAPPDATA%\GtaRadio\probe.log`:

```
09:17:35.658 static ctor
09:17:35.659 ctor
09:17:35.679 first tick, loading=False
09:17:35.679 alive ticks=1 loading=False
09:17:45.680 alive ticks=1395 loading=False
09:17:55.684 alive ticks=2829 loading=False
09:18:05.685 alive ticks=4254 loading=False
09:18:15.689 alive ticks=5687 loading=False
```

Вывод: SHVDN и скрипты работают, проблема была в неполной установке SHVDN.
Отчёт: `diag/report_T1_20261017_091828.txt`. Пробник убран.

## T2. Мод в режиме Remote

Первый запуск: `MuteStrategies=Scene,Freeze,VehicleOff`.

- Мод загрузился: `[OK] loaded, station=RADIO_02_POP audio=False output=2`.
- Агент запущен, `game link open, rate=48000`, сцена `CAR_MOD_RADIO_MUTE_SCENE`.
- **Проблема:** колесо показывало `OFF` — стратегия `VehicleOff` ставит радио машины в OFF.
  Музыки не слышно (плеер дополнительно был замьючен с прошлых версий).

Второй запуск: `MuteStrategies=Scene,Freeze`, mute снят у Spotify и Яндекса
(`--fix`, 2 и 1 сессия).

- **Звук пошёл корректно**, станция в колесе — **GTA Radio**, штатная Pop не звучит.
- Уведомление игры показывает наш трек: `GTA Radio: Надежда Кадышева - Пляшет веночек`.
- **Остался дефект:** в центре колеса радио показывается **оригинальный трек Pop**
  (`THE BLACK EYED PEAS — Meet Me Halfway`), а не трек из плеера.
  Наш `Dashboard` (`SET_RADIO`) обновляет дисплей, но колесо при открытии берёт трек станции.
- Отчёт: `diag/report_T2b_20261017_094108.txt`.

### Наблюдения по логам

- `mod.log`: `ctor: ok ... output=Remote`, `start: game loaded`, `start: link ok, rate=48000`,
  `[OK] loaded ... audio=False output=2`, `agent started`, `mute scene: CAR_MOD_RADIO_MUTE_SCENE`.
- `agent.log`: `agent start`, `game link open, rate=48000`.
- `state.txt` (от старого клиента 1.x) не обновляется агентом: `active=0`, app=Spotify.

## Что осталось

1. Колесо/центр: показывать трек плеера вместо оригинального трека Pop
   (нужен другой вызов Scaleform или обновление в момент открытия колеса).
2. Привести `state.txt` к агенту (сейчас файл от старого клиента).
3. T3 (`Output=Agent`) и T4 (`Output=Game`) не запускались — сначала решение по колесу.
4. Проверить паузу/play при выходе/входе в машину в режиме Remote (пользователь ещё не подтверждал).
