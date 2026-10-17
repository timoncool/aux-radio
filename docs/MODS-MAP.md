# Карта радио-модов GTA V

Собрано по исходникам и описаниям. Цель — понять, каким способом моды выводят музыку
в игру и как занимают станцию.

## 1. Пульт (плеер играет сам)

| Мод | Язык / база | Источник звука | Станция | Метаданные | Enhanced | Лицензия/статус |
|---|---|---|---|---|---|---|
| [sahailee/GTA-Spotify-Radio](https://github.com/sahailee/GTA-Spotify-Radio) | C# SHVDN + Spotify Web API + NativeUI | Spotify играет сам; мод меняет его громкость через API | add-on `RADIO_47_SPOTIFY_RADIO` (Community HUD.gfx) | Scaleform `dashboard` `SET_RADIO("", станция, исполнитель, трек)` + уведомление | нет (Legacy) | исходник есть, LICENSE нет |
| [Amaru8/CiderGTA](https://github.com/Amaru8/CiderGTA) | C# SHVDN, по мотивам sahailee (не форк) | Apple Music (Cider) играет сам, управление через API Cider | `RADIO_47_SPOTIFY_RADIO` | Scaleform `SET_RADIO` | нет | AGPL-3.0 |
| [SiverKing/GTA5_CarRadioMusicMod](https://github.com/SiverKing/GTA5_CarRadioMusicMod) | Python (PyLoaderV) | любые плееры с системными медиа-клавишами | не занимает станцию | нет | да | открытый исходник |
| Radio Control (gta5-mods) | C# SHVDN | нативное радио, mute/unmute + память станции | нативные | нет | нет | закрытый |

Вывод: надёжный путь — не трогать аудио. Мод управляет плеером (API или медиа-клавиши)
и показывает трек. Шума быть не может.

## 2. Свой аудиодвижок внутри игры

| Мод | Язык | Декодер | Вывод | Станция | Особенности |
|---|---|---|---|---|---|
| [MichonGoddijn231849/gta-spotify-radio](https://github.com/MichonGoddijn231849/gta-spotify-radio) | C++ ASI (SHV) | librespot (Spotify Connect) | XAudio2 в процессе игры | занимает `RADIO_02_POP`, штатное радио OFF | эталон архитектуры; требует Spotify Premium |
| [op30mmd/MusicPlayerV](https://github.com/op30mmd/MusicPlayerV) | C++ ASI | FFmpeg / Media Foundation | XAudio2 | не занимает, глушит радио при игре | локальные файлы + YouTube через yt-dlp |
| [LfxB/CustomRadioStations-GTAV](https://github.com/LfxB/CustomRadioStations-GTAV) | C# SHVDN 2.x | irrKlang | свой движок | свои колёса поверх штатного | 2019, Enhanced не заявлен |
| [Internet Radio LS](https://github.com/trenkmannsteven-del/-Internet-Radio-LS) (Nexus, Enhanced) | C# SHVDN, исходник открыт для чтения (All Rights Reserved) | интернет-потоки через helper-процессы | свой | штатное радио OFF, своё меню | Spotify/YouTube Music — громкость сессии в микшере |

Вывод: свой движок даёт полный контроль, но требует декодера, работающего в процессе игры,
и сложен в сборке (C++/ASI, зависимости). Для Яндекс.Музыки готового декодера нет —
её звук можно получить только захватом процесса, а это возвращает проблему двойного звука.

## 3. Захват звука процесса

| Проект | Что делает | Итог |
|---|---|---|
| [bozbez/win-capture-audio](https://github.com/bozbez/win-capture-audio) | плагин-источник OBS на process loopback (виртуального устройства не создаёт) | GPL-2.0, beta; референс API |
| Клиент 1.x (legacy/) | process loopback -> NAudio -> устройство | гул — из-за режима EXCLUDE (захват собственного вывода), см. FACTCHECK.md |

## 4. Правки станций и логотипов

| Инструмент/мод | Назначение |
|---|---|
| Community HUD.gfx (WildBrick142) | add-on станции и логотипы для колеса |
| `gta-patch` (CodeWalker.Core, Gen9) | правка `global.gxt2` в `mods/update/update.rpf`, извлечение/запись RPF |
| OpenIV | классический RPF-редактор (Legacy) |

## Итоговая архитектура AUX Radio VIP (1.x, устарело — см. ARCHITECTURE.md)

1. Плеер (Яндекс/Spotify) играет сам, без захвата.
2. SHVDN-мод читает `state.txt` клиента (SMTC-метаданные).
3. На станции AUX Radio мод глушит штатную музыку и показывает трек.
4. Вход в машину -> play, выход/переключение -> pause (медиа-клавиша).
5. Переименование станции — правка GXT2 в оверлее `mods/`, с бэкапом и откатом.
