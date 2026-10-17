# Ресёрч: GTA V Enhanced и внешнее радио

## 1. Целевая сборка

- GTA V **Enhanced**, репак InsaneRamZes, версия **1.0.1158.16**.
- ScriptHookV 3889.0 (собран под 1158.13) грузится и работает.
- ScriptHookVDotNet **Enhanced 1.1.0.6**, API 3.9.0.
- `version.dll` в корне игры — кряк репака, не трогать.
- Озвучка/радио-дубляж пользователя already используют CodeWalker-совместимый тулинг;
  любые правки — только в оверлей `mods/`, оригиналы не изменяются.

## 2. Станция и её имя

Станция: `RADIO_02_POP` (Pop / Non-Stop-Pop FM).

Имя хранится в GXT2:

```
x64b.rpf\data\lang\american_rel.rpf\global.gxt2   (647 179 байт)
x64b.rpf\data\lang\russian_rel.rpf\global.gxt2    (824 087 байт)
```

Игра читает оверлей:

```
mods/update/update.rpf\x64\patch\data\lang\american_rel.rpf\global.gxt2  (127 817 байт)
mods/update/update.rpf\x64\patch\data\lang\russian_rel.rpf\global.gxt2   (286 242 байт)
```

`mods/x64b.rpf` игра **не читает** — проверено скриншотом.

### Формат GXT2

- `u32 count` по смещению 4;
- далее пары `(u32 hash, u32 offset)` с offset 8;
- строка по offset до `\0`.

Метка `RADIO_02_POP` = `0xB5DD6F19`, значение `Non-Stop-Pop FM`.
Замена на `AUX Radio` проверена извлечением:

```
[OK] update.rpf\x64\patch\data\lang\american_rel.rpf\global.gxt2 written (127817 bytes)
[OK] update.rpf\x64\patch\data\lang\russian_rel.rpf\global.gxt2 written (286242 bytes)
verify: 'AUX Radio'
```

На скриншотах пользователя название появилось.

## 3. RPF-инструменты

- **CodeWalker.Core** (исходники `D:\Projects\_tools\CodeWalker`) на Enhanced **не работает**:
  `keys generate failed: Value cannot be null. (Parameter 'src')`,
  NRE в `GTA5Hash.CalculateHash` -> `GTACrypto.GetNGKey` -> `DecryptNG` -> `RpfFile.ReadHeader`,
  `root entries: 0`.
- Рабочий инструмент — **`gta-patch`** из проекта дубляжа
  (`music-studios/game-dub/games/gta5/tools/gta-patch`), CodeWalker-based, умеет Gen9.
  Команды:

```
gta-patch.exe list    <game> <rpf> <filter>
gta-patch.exe extract <game> <rpf> <outdir> <entry>
gta-patch.exe put     <game> <rpf> <file...>
gta-patch.exe relxml  <game> <rpf> <entry>
```

## 4. Текстуры станций

`x64a.rpf\textures\radio_hud.ytd` (94 708 байт): `wktt_col`, `massiveb_bw`, `lrr_bw`,
`if99_bw`, `integrity_col`, `beat_bw`, `plr_bw`, `fusion_col`, `tuffgong_col`,
`thejourney_col`, `vladivostok_col`, `thevibe_col`, `k109_bw`, `jnr_col`, `sanjuan_col`,
`theclassics_bw`, `electrochoc_bw`, `lchc_bw` и т.д.

Текстур с `pop` в `radio_hud.ytd` **нет** (`grep -i pop` -> exit 1).
Рядом: `radio_hud_colored.ytd` (65 662), `radio_hud_noncolored.ytd` (29 089).
Логотип Pop, вероятно, в Scaleform/UI (`hud.gfx`, `scaleform_generic.rpf`), а не в textures.

## 5. SMTC (источник метаданных)

- Spotify: `Spotify.exe`, отдаёт `Artist — Title`.
- Яндекс.Музыка Desktop 5.122.0 (`ru.yandex.desktop.music`,
  `%LOCALAPPDATA%\Programs\YandexMusic\Яндекс Музыка.exe`, 5–6 процессов Electron).
- SMTC-сессия Яндекса появляется только после старта воспроизведения.
- Chrome исключён фильтром `Sources.IsMusicApp`.

## 6. Захват звука процесса: что выяснено

Реализован WASAPI process loopback (`ActivateAudioInterfaceAsync` +
`AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK`).

Ошибки и фиксы:

1. `InvalidCastException ... IAudioClient ... E_NOINTERFACE (0x80004002)` —
   устройство бралось из закрытого `MMDeviceEnumerator`; лечится выбором по ID
   и новым enumerator с `Dispose`.
2. Тот же `E_NOINTERFACE` на STA-потоке WinForms — активацию loopback нужно делать
   на **MTA**-потоке (`SetApartmentState(ApartmentState.MTA)`).
3. Формат устройства: `mix sr=48000 bits=32 ch=2 enc=Extensible`,
   `SubFormat=00000003-0000-0010-8000-00aa00389b71` (IEEE float).
   Если считать его 16-битным — получается гул. Нужно проверять `SubFormat`.
4. [НЕДЕЙСТВИТЕЛЬНО, см. FACTCHECK.md: замер в режиме EXCLUDE] Захват **не режется** мьютом сессии: `RMS до 794` -> `muted: 1` -> `RMS 1028`,
   полный тракт `samples=766080 RMS=10211.3 peak=32344 -> SIGNAL`.
5. Запись захвата (5 с, Яндекс) дала `RMS 0.00514`, `peak 0.0261`,
   спектральная плоскостность 0.17–0.25 — это музыка, не шум.
6. Вывод через NAudio (`WasapiOut`, float + `WaveFormatExtensible`) в тесте
   на неслышимой громкости дал музыкальный спектр (flatness 0.08), но при реальном
   использовании пользователь слышал гул/наложение. Причина — двойное воспроизведение
   (исходная сессия + наш вывод) и повторное включение штатного радио игрой.

Вывод: захват технически возможен, но требует глушения исходной сессии и
перевывода — источник шума и конфликтов. Поэтому выбран путь «пульт».

## 7. Архитектура рабочих модов

- `sahailee/GTA-Spotify-Radio`: C# SHVDN, Spotify Web API, add-on станция
  `RADIO_47_SPOTIFY_RADIO` (Community HUD.gfx), Scaleform `dashboard` `SET_RADIO`,
  громкость Spotify через API (0 = mute). Звук не захватывается.
- `Amaru8/CiderGTA`: та же схема для Apple Music (Cider), самостоятельный код, AGPL-3.0.
- `MichonGoddijn231849/gta-spotify-radio`: C++ ASI, librespot, XAudio2,
  занимает `RADIO_02_POP`, штатное радио OFF.
- `op30mmd/MusicPlayerV`: C++ ASI, FFmpeg/Media Foundation, XAudio2, локальные файлы.
- `LfxB/CustomRadioStations-GTAV`: C# SHVDN, irrKlang, добавление станций.
- `SiverKing/GTA5_CarRadioMusicMod`: Python, системные медиа-клавиши, станцию не занимает.
- Internet Radio LS (Nexus, Enhanced): C# SHVDN, исходник открыт для чтения (github.com/trenkmannsteven-del/-Internet-Radio-LS); Spotify/YouTube Music — громкостью сессии в микшере.

## 8. Нативы

- `SET_VEHICLE_RADIO_ENABLED(veh, false)` — глушит штатное радио (используется в моде).
- `GET_PLAYER_RADIO_STATION_NAME` — возвращает ID станции (`RADIO_02_POP`).
- `SET_RADIO_STATION_MUSIC_ONLY(station, true)` — убирает рекламу/новости.
- Аудио-флаги (`SET_AUDIO_FLAG`): `DisableRadio` отсутствует; есть
  `FrontendRadioDisabled`, `AllowRadioDuringSwitch`, `AllowRadioOverScreenFade`,
  `ForceSeamlessRadioSwitch`, `MobileRadioInGame` и др. — глушения только музыки станции нет.

## 9. Полезные ссылки

- https://github.com/sahailee/GTA-Spotify-Radio
- https://github.com/Amaru8/CiderGTA
- https://github.com/MichonGoddijn231849/gta-spotify-radio
- https://github.com/op30mmd/MusicPlayerV
- https://github.com/LfxB/CustomRadioStations-GTAV
- https://github.com/SiverKing/GTA5_CarRadioMusicMod
- https://github.com/bozbez/win-capture-audio
- https://www.nexusmods.com/gta5enhanced/mods/1451 (Internet Radio LS)
- https://gta5-mods.com/scripts/radio-control
- https://github.com/DurtyFree/gta-v-data-dumps/issues/2 (audio flags)
