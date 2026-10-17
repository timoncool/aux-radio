> **Устарело (версия 1.x, код в `legacy/`).** Актуально: [ARCHITECTURE.md](ARCHITECTURE.md), [DEEPSEEK.md](DEEPSEEK.md), [FACTCHECK.md](FACTCHECK.md).

# Что было сделано: заметки по коду

Ниже — карта моего кода и ключевые решения, чтобы следующий агент понимал,
что уже проверено и где грабли.

## app/GtaRadio — клиент-трей (C# .NET 8, WinForms)

| Файл | Что делает |
|---|---|
| `Program.cs` | точка входа; режимы `--status`, `--diag <token>`, `--verify`, `--fix`, `--unmute`, `--mix`, `--rec`, `--stop` |
| `TrayApp.cs` | главный цикл: раз в 2 с читает SMTC, выбирает источник, пишет `state.txt`, heartbeat `engine.on`, трей-меню |
| `Sources.cs` | обёртка над `GlobalSystemMediaTransportControlsSessionManager`: список сессий, `AppId`, `Name`, `Artist`, `Title`, `Playing`, `TryPlayAsync` |
| `AppConfig.cs` | `config.json`: `Enabled`, `SourceAppId`, `Volume`, `DuckVolume`, `ExitWithGame`, `AllowAnyApp`, `WaitForGameSeconds`, `GameDir`, `OutputDevice`, `ControlOnly` |
| `AudioEngine.cs` | (устаревшая ветка) захват процесса через NAudio `WasapiOut`, `VolumeSampleProvider`, `DuckVolume`; сюда же входили фиксы MTA и float |
| `ProcessLoopback.cs` | P/Invoke `ActivateAudioInterfaceAsync` + `AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK` |
| `Diag.cs` | диагностика: `Run`, `Record`, `Verify`, `Fix`, `Unmute`, `Mix`; пишет `%LOCALAPPDATA%\GtaRadio\diag.log` |
| `StatusForm.cs` | окно статуса из трея |
| `GameWatcher.cs` | поиск процесса `GTA5_Enhanced` |
| `ModPipe.cs` | именованный канал (не используется, оставлен как альтернатива state.txt) |
| `Installer.cs` | копирование файлов в игру (черновая версия, установщик теперь Inno Setup) |

### Ключевые решения клиента

- **ControlOnly = true** (по умолчанию): клиент не запускает `AudioEngine`, не трогает
  звук. Только SMTC -> `state.txt`.
- Источник выбирается с гистерезисом: играющий музыкальный сеанс удерживается,
  чтобы не было переключений Spotify <-> Яндекс каждые 2 секунды.
- `state.txt`:

```
active=1
app=Яндекс Музыка
artist=Saint Of Sin
title=I Am Not My Thoughts
playing=1
```

- `active` = игра запущена и найден выбранный плеер (даже на паузе).
- При старте игры клиент делает `TryPlayAsync`, если плеер на паузе (автозапуск).

### Грабли, уже пройденные

1. `E_NOINTERFACE` при активации `IAudioClient`: устройство из закрытого
   `MMDeviceEnumerator`; лечится выбором по ID и новым enumerator.
2. Тот же `E_NOINTERFACE` на STA-потоке WinForms: открывать loopback на MTA-потоке.
3. Формат `WaveFormatExtensible` с `SubFormat=00000003-...` — это float32.
   Если играть как PCM16 — получается гул.
4. Принудительное убийство клиента оставляет сессии замьюченными
   (`--unmute <token>` снимает).
5. Захват процесса не режется мьютом сессии, но требует перевывода,
   поэтому от ветки отказались.

## mod/GtaRadioMod — SHVDN-мод (C# net48, API 3.9)

Файл `GtaRadioMod.cs`, ~150 строк.

- `RadioId = "RADIO_02_POP"`.
- Каждые 350 мс:
  - читает `state.txt` (если файл старше 4 с — считается неактуальным);
  - `_want = active && в машине && двигатель работает && текущая станция == RADIO_02_POP`;
  - при переходе `false -> true` и `playing=0` посылает `VK_MEDIA_PLAY_PAUSE`;
  - при переходе `true -> false` и `playing=1` посылает ту же клавишу (пауза);
  - пока `_want` и не открыто колесо (`Control.VehicleRadioWheel`) —
    `SET_VEHICLE_RADIO_ENABLED(veh, false)`, иначе включает радио обратно;
  - рисует HUD: `AUX Radio`, имя приложения, `Артист — Трек` или `ПАУЗА`.
- `Aborted` возвращает радио в исходное состояние.

### Почему так

- Имя станции — из GXT2, динамически не меняется; переименовали один раз.
- Штатную музыку станции нельзя выключить аудио-флагом (флага `DisableRadio` нет),
  поэтому используется `SET_VEHICLE_RADIO_ENABLED(false)` на время активности.
- Колесо может не открываться при выключенном радио — поэтому на удержании
  `VehicleRadioWheel` радио включается, после выбора снова выключается.

## installer/gta_radio.iss

Inno Setup по шаблону `neurodub.iss`: тёмный мастер, арт `installer/art`,
admin, `OutputBaseFilename=GtaRadio_Setup_1.0`, кладёт SHV/SHVDN/мод/клиент.
RPF-правка GXT2 и откат пока не встроены — следующий шаг.

## tools

- `YtdProbe` — читает YTD из RPF через CodeWalker.Core и печатает список текстур
  (`radio_hud.ytd`: `wktt_col`, `lrr_bw`, ...; текстуры `pop` нет).
- `RpfProbe` — ранний пробник RPF; на Gen9 падает (`keys generate failed`),
  оставлен как история.

## Что не работает / не проверено

- Логотип станции не заменён.
- Установщик не делает RPF-правку и откат.
- Автозапуск клиента вместе с игрой не подключён.
- В игре текущая схема ещё не проверялась после перехода на «пульт».
- Ветка `AudioEngine`/`ProcessLoopback` — мёртвый код, можно удалить.
