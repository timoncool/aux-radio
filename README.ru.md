<div align="center">

# AUX Radio

**Твоя музыка из Spotify, Яндекс Музыки или любого плеера Windows — как радиостанция в GTA V Enhanced: настоящий трек в колесе радио, один установщик, без виртуальных кабелей.**

[![License](https://img.shields.io/github/license/timoncool/aux-radio?style=flat-square)](LICENSE)
[![Stars](https://img.shields.io/github/stars/timoncool/aux-radio?style=flat-square)](https://github.com/timoncool/aux-radio/stargazers)
[![Last Commit](https://img.shields.io/github/last-commit/timoncool/aux-radio?style=flat-square)](https://github.com/timoncool/aux-radio/commits)
[![Downloads](https://img.shields.io/github/downloads/timoncool/aux-radio/total?style=flat-square)](https://github.com/timoncool/aux-radio/releases)
[![Status](https://img.shields.io/badge/status-beta-orange?style=flat-square)](https://github.com/timoncool/aux-radio)

**[English](README.md)** · **Русский**

![AUX Radio в колесе радио GTA V](docs/screenshots/wheel.png)

</div>

## Что это

**AUX Radio** превращает **Non-Stop-Pop FM** (`RADIO_02_POP`) в **GTA V Enhanced** в твою станцию: в машине играет то, что сейчас играет в Spotify, Яндекс Музыке, AIMP, foobar2000, VLC, MusicBee или браузере.

Мод — это **пульт, а не звуковой движок**. Плеер играет сам через свой вывод: ничего не захватывается и не перевыводится, виртуальных кабелей нет, аудиоустройства Windows не трогаются. Мод глушит штатную станцию, ставит плеер на паузу и снимает её вместе с игрой, а в колесо радио, ленту уведомлений и дисплей машины выводит настоящего исполнителя и трек.

Перевод и радио-озвучка не страдают: установщик меняет **одну строку GXT2 и одну текстуру HUD** в `mods\update\update.rpf`, сначала делает бэкап, а при удалении возвращает всё обратно.

## Возможности

- **Любой плеер с медиасессией Windows** — Spotify, Яндекс Музыка Desktop, AIMP, foobar2000, VLC, MusicBee, браузеры.
- **Без захвата и виртуальных кабелей** — плеер играет сам; нет loopback, эха, гула и смены устройств.
- **Как настоящее радио** — F7 пауза/плей, F8 следующий трек, F6 предыдущий; пауза при выходе из машины и смене станции, продолжение при возврате, реакция на паузу игры и катсцены.
- **Трек в колесе** — центр колеса показывает `Исполнитель - Трек` вместо трека Pop, плюс уведомление `AUX: Исполнитель - Трек` и дисплей в машине.
- **Название и логотип** — в колесе станция **AUX** с логотипом *PLUG IN YOUR MUSIC*; ставится с бэкапом и удаляется при деинсталляции.
- **Безопасно для модов** — правится только оверлей `mods\`; оригинальные RPF игры, перевод и радио-озвучка не затрагиваются.
- **Агент живёт с игрой** — запускается при загрузке, закрывается через несколько секунд после выхода; автозапуска Windows нет.
- **RU / EN** — двуязычный установщик и переключатель языка в окне статуса.
- **Один установщик** — Script Hook V и ScriptHookVDotNet Enhanced ставит только если их нет, удаляет только свои файлы.

## Требования

- **GTA V Enhanced** на Windows 10/11 x64 (проверено на 1.0.1158.16).
- Плеер с **медиасессией Windows**, в котором играет музыка.
- **Script Hook V + ScriptHookVDotNet Enhanced** — установщик добавит, если их нет.

## Быстрый старт

1. **Скачай** `AUX_Radio_Setup_2.0.exe` из [Releases](https://github.com/timoncool/aux-radio/releases).
2. **Установи** — выбери папку GTA V Enhanced (где лежит `GTA5_Enhanced.exe`).
3. **Играй** — включи музыку в плеере, запусти игру, сядь в машину и выбери **AUX** в колесе радио.

## Управление

| Клавиша | Действие |
|---------|----------|
| **F7** | Пауза / плей плеера |
| **F8** | Следующий трек |
| **F6** | Предыдущий трек |

Клавиши работают только когда в машине включена станция **AUX**.

## Окно статуса

Двойной клик по значку в трее (или `GtaRadio.Agent.exe --status`):

![Окно статуса AUX Radio](docs/screenshots/status.png)

Там игра, режим вывода, текущий плеер и трек, все медиасессии Windows, горячие клавиши и ссылки. Кнопка **EN / РУ** переключает язык интерфейса.

## Как это работает

```text
Spotify / Яндекс Музыка / любой плеер
        │  играет через свой вывод (без изменений)
        ▼
GtaRadio.Agent (трей, .NET 8)
        │  медиасессии Windows (SMTC) + медиа-клавиши
        │  общая память: трек, состояние, команды
        ▼
GtaRadio.Mod.dll (Script Hook VDOT Net, внутри игры)
        │  занимает RADIO_02_POP, глушит штатную станцию,
        │  пишет трек в метки колеса и на дисплей
        ▼
GTA V Enhanced — колесо радио, уведомления, дисплей
```

- Станция глушится штатными аудиосценами игры и заморозкой трека — как в рабочих модах для Enhanced.
- Центр колеса использует таблицу меток треков игры: мод берёт самые длинные метки, перенаправляет их на трек плеера и возвращает исходный текст, когда эфир заканчивается.
- Установщик правит только `mods\update\update.rpf`: строку `RADIO_02_POP` в обоих языковых GXT2 и текстуру `gtav_radio_stations_texture_512` в `hud.ytd`. Бэкапы кладутся рядом с игрой, удаление возвращает текущие файлы.

## Сборка из исходников

```powershell
.\build.ps1 -GtaPatchDir <папка gta-patch> -ShvDir <папка с SHV/SHVDN> [-Deploy <папка игры>] [-Iscc <ISCC.exe>]
```

Нужны .NET SDK 8, Inno Setup 6 и Script Hook V + ScriptHookVDotNet Enhanced для payload. Подробнее: [docs/BUILD.md](docs/BUILD.md), [docs/DEEPSEEK.md](docs/DEEPSEEK.md).

## Документация

- [docs/BUILD.md](docs/BUILD.md) — сборка и установка
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — как устроены мод, агент и установщик
- [docs/MODS-MAP.md](docs/MODS-MAP.md) — моды, на которых учились
- [docs/HISTORY.md](docs/HISTORY.md) — от движка захвата 1.x к нынешнему пульту

## Другие проекты [@timoncool](https://github.com/timoncool)

| Проект | Описание |
|--------|----------|
| [Dub Studio](https://github.com/timoncool/dub-studio) | Оффлайн-студия AI-дубляжа видео для Windows |
| [ACE-Step Studio](https://github.com/timoncool/ACE-Step-Studio) | Портативная AI-студия музыки — песни, вокал, каверы |
| [VideoSOS](https://github.com/timoncool/videosos) | AI-видеопродакшн в браузере |
| [Bulka](https://github.com/timoncool/Bulka) | Платформа лайв-кодинга музыки с AI-агентом |
| [Foundation Music Lab](https://github.com/timoncool/Foundation-Music-Lab) | Генерация музыки с редактором таймлайна |
| [Qwen3-TTS](https://github.com/timoncool/Qwen3-TTS_portable_rus) | Портативный TTS с клонированием голоса |

## Авторы

- **Nerual Dreming** — [Telegram](https://t.me/nerual_dreming) | [neuro-cartel.com](https://neuro-cartel.com) | [ArtGeneration.me](https://artgeneration.me)

## Поддержать автора

Я создаю опенсорс софт и занимаюсь исследованиями в области ИИ. Большая часть всего, что я делаю, находится в открытом доступе. Ваши пожертвования позволяют мне создавать и исследовать больше, не отвлекаясь на поиск еды для продолжения существования =)

**[Все способы поддержки](https://github.com/timoncool/ACE-Step-Studio/blob/master/DONATE.md)** | **[dalink.to/nerual_dreming](https://dalink.to/nerual_dreming)** | **[boosty.to/neuro_art](https://boosty.to/neuro_art)**

- **BTC:** `1E7dHL22RpyhJGVpcvKdbyZgksSYkYeEBC`
- **ETH (ERC20):** `0xb5db65adf478983186d4897ba92fe2c25c594a0c`
- **USDT (TRC20):** `TQST9Lp2TjK6FiVkn4fwfGUee7NmkxEE7C`

## Star History

<a href="https://www.star-history.com/?repos=timoncool%2Faux-radio&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=timoncool/aux-radio&type=date&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=timoncool/aux-radio&type=date&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=timoncool/aux-radio&type=date&legend=top-left" />
 </picture>
</a>

## Лицензия

[MIT](LICENSE)
