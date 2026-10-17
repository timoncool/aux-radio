# GTA Radio VIP

Станция **Non-Stop-Pop FM** (`RADIO_02_POP`) в **GTA V Enhanced** становится **GTA Radio**:
в машине играет то, что звучит у тебя в Яндекс Музыке, Spotify или любом другом плеере
с медиасессией Windows (AIMP, foobar2000, VLC, MusicBee, браузер и т.д.).

Статус: **2.0, собрано, ждёт проверки в игре** — чек-лист в [docs/DEEPSEEK.md](docs/DEEPSEEK.md).

## Как работает

```
Плеер ──(захват звука процесса, WASAPI process loopback)──> GtaRadio.Agent (трей)
                                                               │ общая память: PCM + трек + команды
                                                               ▼
                                         scripts\GtaRadio.Mod.dll (SHVDN, внутри игры)
```

- **Агент** находит плеер через системные медиасессии, захватывает звук только его процессов,
  на время эфира приглушает плеер в микшере Windows (и компенсирует это в захвате, чтобы не было
  двойного звука), отдаёт поток в игру. Вне эфира — пауза плеера и возврат его громкости.
- **Мод** занимает `RADIO_02_POP` по схеме рабочего Enhanced-мода
  [MichonGoddijn231849/gta-spotify-radio](https://github.com/MichonGoddijn231849/gta-spotify-radio):
  штатная музыка глушится аудиосценой, заморозкой станции и радио машины в OFF; колесо радио
  продолжает работать. Поток проигрывается из процесса игры, громкость следует игре: ползунок
  музыки, пауза, катсцены, смена персонажа, разговоры и звонки, закрытый салон, вода.
- **Трек** — в штатных местах игры: лента уведомлений при смене трека и дисплей радио в машине
  (Scaleform `dashboard` / `SET_RADIO`, как у [sahailee/GTA-Spotify-Radio](https://github.com/sahailee/GTA-Spotify-Radio)).
  Своего интерфейса поверх игры нет.
- **Название** «GTA Radio» в колесе — одна строка GXT2 в оверлее `mods\update\update.rpf`.
  Перевод и радио-озвучка не затрагиваются; бэкап и откат одной строки при удалении.

## Состав

```
src/Shared/RadioLink.cs    общая память агент <-> мод
src/GtaRadio.Agent/        агент в трее (.NET 8): SMTC, захват процесса, приглушение плеера
src/GtaRadio.Mod/          мод SHVDN (net48): станция, проигрывание в игре, трек
src/GtaRadio.Gxt2/         правка одной строки GXT2 (для установщика)
installer/                 Inno Setup + patch_station.ps1 (имя станции, бэкап, откат)
build.ps1                  сборка всего и быстрая установка в игру (-Deploy)
legacy/                    версия 1.x (не используется, оставлена для истории)
```

## Сборка

```powershell
.\build.ps1 -GtaPatchDir <папка gta-patch> -ShvDir <папка с SHV/SHVDN> [-Deploy <папка игры>] [-Iscc <ISCC.exe>]
```

Требования: Windows 10 2004+ (process loopback), .NET SDK 8, ScriptHookV + ScriptHookVDotNet Enhanced,
Inno Setup 6 для установщика. Подробно — [docs/DEEPSEEK.md](docs/DEEPSEEK.md).

## Документация

- [docs/DEEPSEEK.md](docs/DEEPSEEK.md) — сборка, установка, чек-лист проверки в игре
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — устройство и решения со ссылками на образцы
- [docs/FACTCHECK.md](docs/FACTCHECK.md) — проверка ссылок и фактов из прежних заметок
- [docs/RESEARCH.md](docs/RESEARCH.md), [docs/MODS-MAP.md](docs/MODS-MAP.md), [docs/HISTORY.md](docs/HISTORY.md) — ресёрч и история 1.x
