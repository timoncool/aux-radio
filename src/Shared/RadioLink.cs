using System;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;

namespace GtaRadio.Shared
{
    /// <summary>
    /// Общая память агента и мода: заголовок + кольцевой буфер PCM (float32, stereo).
    /// Агент пишет звук и метаданные, мод пишет состояние станции и команды.
    /// Создаёт мод (живёт вместе с игрой), агент открывает существующую.
    /// </summary>
    public sealed class RadioLink : IDisposable
    {
        public const string MapName = @"Local\GtaRadio.v1";
        public const int Magic = 0x44525447; // "GTRD"
        public const int Version = 1;
        public const int Channels = 2;
        public const int RingFrames = 96000;          // 2 с при 48 кГц
        private const int HeaderSize = 4096;
        private const int RingBytes = RingFrames * Channels * 4;
        public const long TotalSize = HeaderSize + RingBytes;

        // Смещения заголовка.
        private const int OffMagic = 0;
        private const int OffVersion = 4;
        private const int OffSampleRate = 8;          // мод: частота устройства вывода игры
        private const int OffWritePos = 16;           // агент: всего записано кадров
        private const int OffAgentBeat = 24;          // агент: UTC ticks
        private const int OffAgentState = 32;         // агент: 0 нет плеера, 1 готов, 2 идёт поток, 3 закрыт пользователем
        private const int OffPlaying = 36;            // агент: плеер играет
        private const int OffGameBeat = 40;           // мод: UTC ticks
        private const int OffOwned = 48;              // мод: эфир нужен (наша станция, в машине, игра не на паузе)
        private const int OffCmdSeq = 56;             // мод: счётчик команд
        private const int OffCmd = 60;                // мод: 1 play/pause, 2 next, 3 prev
        private const int OffMetaSeq = 64;            // агент: счётчик метаданных
        private const int OffApp = 128;               // 64 символа UTF-16
        private const int OffArtist = 256;            // 256 символов
        private const int OffTitle = 768;             // 256 символов
        private const int AppChars = 64, TextChars = 256;

        public const int StateNone = 0, StateReady = 1, StateStreaming = 2, StateUserExit = 3;
        public const int CmdPlayPause = 1, CmdNext = 2, CmdPrev = 3;

        private readonly MemoryMappedFile _map;
        private readonly MemoryMappedViewAccessor _v;

        private RadioLink(MemoryMappedFile map)
        {
            _map = map;
            _v = map.CreateViewAccessor(0, TotalSize);
        }

        /// <summary>Мод: создать (или открыть уже созданную) общую память.</summary>
        public static RadioLink Create(int sampleRate)
        {
            var link = new RadioLink(MemoryMappedFile.CreateOrOpen(MapName, TotalSize));
            link._v.Write(OffVersion, Version);
            link._v.Write(OffSampleRate, sampleRate);
            link._v.Write(OffMagic, Magic);
            return link;
        }

        /// <summary>Агент: открыть, если игра с модом запущена; иначе null.</summary>
        public static RadioLink? TryOpen()
        {
            try
            {
                var link = new RadioLink(MemoryMappedFile.OpenExisting(MapName));
                if (link._v.ReadInt32(OffMagic) != Magic || link._v.ReadInt32(OffVersion) != Version)
                {
                    link.Dispose();
                    return null;
                }
                return link;
            }
            catch { return null; }
        }

        public int SampleRate => _v.ReadInt32(OffSampleRate);

        // ---- агент ----

        /// <summary>Пишет кадры в кольцо, затем публикует новую позицию.</summary>
        public void WriteFrames(float[] interleaved, int frames)
        {
            long pos = _v.ReadInt64(OffWritePos);
            int samples = frames * Channels;
            int start = (int)(pos % RingFrames) * Channels;
            int first = Math.Min(samples, RingFrames * Channels - start);
            _v.WriteArray(HeaderSize + (long)start * 4, interleaved, 0, first);
            if (first < samples) _v.WriteArray(HeaderSize, interleaved, first, samples - first);
            Thread.MemoryBarrier();
            _v.Write(OffWritePos, pos + frames);
        }

        public void AgentBeat(int state, bool playing)
        {
            _v.Write(OffAgentState, state);
            _v.Write(OffPlaying, playing ? 1 : 0);
            _v.Write(OffAgentBeat, DateTime.UtcNow.Ticks);
        }

        public void WriteMeta(string app, string artist, string title)
        {
            WriteString(OffApp, AppChars, app);
            WriteString(OffArtist, TextChars, artist);
            WriteString(OffTitle, TextChars, title);
            Thread.MemoryBarrier();
            _v.Write(OffMetaSeq, _v.ReadInt32(OffMetaSeq) + 1);
        }

        public bool GameAlive => DateTime.UtcNow.Ticks - _v.ReadInt64(OffGameBeat) < TimeSpan.TicksPerMillisecond * 1500;
        public bool Owned => _v.ReadInt32(OffOwned) != 0;
        public int CmdSeq => _v.ReadInt32(OffCmdSeq);
        public int Cmd => _v.ReadInt32(OffCmd);

        // ---- мод ----

        public bool AgentAlive => DateTime.UtcNow.Ticks - _v.ReadInt64(OffAgentBeat) < TimeSpan.TicksPerMillisecond * 1500;
        public int AgentState => _v.ReadInt32(OffAgentState);
        public bool Playing => _v.ReadInt32(OffPlaying) != 0;
        public long PublishedFrames => _v.ReadInt64(OffWritePos);

        public void GameBeat(bool owned)
        {
            _v.Write(OffOwned, owned ? 1 : 0);
            _v.Write(OffGameBeat, DateTime.UtcNow.Ticks);
        }

        public void SendCommand(int cmd)
        {
            _v.Write(OffCmd, cmd);
            Thread.MemoryBarrier();
            _v.Write(OffCmdSeq, _v.ReadInt32(OffCmdSeq) + 1);
        }

        /// <summary>Читает кадры [from, from+frames) из кольца. Вызывающий следит, чтобы данные не были перезаписаны.</summary>
        public void ReadFrames(long from, float[] dst, int frames)
        {
            int samples = frames * Channels;
            int start = (int)(from % RingFrames) * Channels;
            int first = Math.Min(samples, RingFrames * Channels - start);
            _v.ReadArray(HeaderSize + (long)start * 4, dst, 0, first);
            if (first < samples) _v.ReadArray(HeaderSize, dst, first, samples - first);
        }

        public int MetaSeq => _v.ReadInt32(OffMetaSeq);
        public string App => ReadString(OffApp, AppChars);
        public string Artist => ReadString(OffArtist, TextChars);
        public string Title => ReadString(OffTitle, TextChars);

        private void WriteString(int off, int maxChars, string? s)
        {
            s ??= "";
            if (s.Length > maxChars - 1) s = s.Substring(0, maxChars - 1);
            var bytes = Encoding.Unicode.GetBytes(s + "\0");
            _v.WriteArray(off, bytes, 0, bytes.Length);
        }

        private string ReadString(int off, int maxChars)
        {
            var bytes = new byte[maxChars * 2];
            _v.ReadArray(off, bytes, 0, bytes.Length);
            var s = Encoding.Unicode.GetString(bytes);
            int z = s.IndexOf('\0');
            return z >= 0 ? s.Substring(0, z) : s;
        }

        public void Dispose()
        {
            try { _v.Dispose(); } catch { }
            try { _map.Dispose(); } catch { }
        }
    }
}
