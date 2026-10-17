using System;
using System.IO;
using GTA;

namespace GtaRadio.Probe
{
    /// <summary>
    /// Диагностика загрузки: пустой скрипт без нативов, звука и сторонних библиотек.
    /// Пишет %LOCALAPPDATA%\GtaRadio\probe.log. Если игра падает с ним — дело не в GtaRadio.Mod.
    /// </summary>
    public class Probe : Script
    {
        private static readonly string File_ = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaRadio", "probe.log");
        private long _ticks;
        private DateTime _last = DateTime.MinValue;

        static Probe() { W("static ctor"); }

        public Probe()
        {
            W("ctor");
            Tick += (_, __) =>
            {
                _ticks++;
                if (_ticks == 1) W("first tick, loading=" + Game.IsLoading);
                if ((DateTime.UtcNow - _last).TotalSeconds >= 10)
                {
                    _last = DateTime.UtcNow;
                    W("alive ticks=" + _ticks + " loading=" + Game.IsLoading);
                }
            };
            Aborted += (_, __) => W("aborted");
        }

        private static void W(string m)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(File_));
                File.AppendAllText(File_, DateTime.Now.ToString("HH:mm:ss.fff ") + m + Environment.NewLine);
            }
            catch { }
        }
    }
}
