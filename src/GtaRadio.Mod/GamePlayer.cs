using System;
using System.Threading;
using GtaRadio.Shared;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace GtaRadio.Mod
{
    /// <summary>
    /// Проигрывание потока агента из процесса игры (сессия звука — сама GTA V).
    /// Громкость/фильтр приходят из игры каждый кадр; плавные переходы и сторож «игра не тикает»
    /// — как в XAudioPlayer у MichonGoddijn231849/gta-spotify-radio.
    /// </summary>
    internal sealed class GamePlayer : ISampleProvider, IDisposable
    {
        private const int TargetLatencyMs = 80;
        private const int MaxLatencyMs = 250;
        private const int HeartbeatTimeoutMs = 400;

        private readonly RadioLink _link;
        private readonly WasapiOut _out;
        private readonly int _rate;
        private readonly int _channels;
        private readonly float[] _stereo;
        private readonly BiQuadFilter[] _lp = new BiQuadFilter[2];

        private long _readPos = -1;
        private volatile float _targetGain;
        private volatile float _targetCutoff = 22050f;
        private long _lastBeat;
        private float _gain;
        private float _cutoff = 22050f;
        private float _appliedCutoff = -1f;
        private readonly float _fadeStep;

        public WaveFormat WaveFormat { get; }
        public bool Silent => _gain <= 0f && _targetGain <= 0f;

        private GamePlayer(RadioLink link, MMDevice dev, WaveFormat mix, int fadeMs)
        {
            _link = link;
            _rate = mix.SampleRate;
            _channels = mix.Channels;
            _stereo = new float[_rate * RadioLink.Channels];
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_rate, _channels);
            _fadeStep = 1f / Math.Max(1, _rate * fadeMs / 1000);
            for (int c = 0; c < 2; c++) _lp[c] = BiQuadFilter.LowPassFilter(_rate, 20000f, 0.707f);
            _out = new WasapiOut(dev, AudioClientShareMode.Shared, true, 50);
            _out.Init(new MixFormatProvider(this, mix));
            _out.Play();
            Beat();
        }

        /// <summary>Устройство по умолчанию — туда же, куда играет сама игра.</summary>
        public static GamePlayer Create(int fadeMs, out RadioLink link)
        {
            var en = new MMDeviceEnumerator();
            var dev = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            var mix = dev.AudioClient.MixFormat;
            link = RadioLink.Create(mix.SampleRate);
            return new GamePlayer(link, dev, mix, fadeMs);
        }

        /// <summary>Вызывается каждый кадр со скрипт-потока.</summary>
        public void SetTarget(float gain, float cutoff)
        {
            _targetGain = Math.Max(0f, gain);
            _targetCutoff = Math.Min(22050f, Math.Max(100f, cutoff));
            Beat();
        }

        private void Beat() => Interlocked.Exchange(ref _lastBeat, DateTime.UtcNow.Ticks);

        public int Read(float[] buffer, int offset, int count)
        {
            int frames = count / _channels;
            if (frames > _stereo.Length / 2) frames = _stereo.Length / 2;

            // Нет кадров игры (загрузка, alt-tab, зависание) — глушим, как сторож у образца.
            bool stale = (DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastBeat)) / TimeSpan.TicksPerMillisecond > HeartbeatTimeoutMs;
            float want = stale ? 0f : _targetGain;

            long write = _link.PublishedFrames;
            bool streaming = _link.AgentAlive && _link.AgentState == RadioLink.StateStreaming;
            long target = _rate * TargetLatencyMs / 1000;
            if (_readPos < 0 || write - _readPos > _rate * MaxLatencyMs / 1000 || write - _readPos > RadioLink.RingFrames / 2 || _readPos > write)
                _readPos = Math.Max(0, write - target);

            int have = streaming ? (int)Math.Min(frames, Math.Max(0, write - _readPos)) : 0;
            if (have > 0)
            {
                _link.ReadFrames(_readPos, _stereo, have);
                _readPos += have;
            }
            Array.Clear(_stereo, have * 2, (frames - have) * 2);

            // Срез фильтра двигается геометрически, чтобы переход звучал ровно.
            float ratio = (float)Math.Pow(_targetCutoff / Math.Max(_cutoff, 1f), Math.Min(frames / (float)_rate / 0.12f, 1f));
            _cutoff = Math.Min(22050f, Math.Max(100f, _cutoff * ratio));
            bool filter = _cutoff < _rate * 0.45f;
            if (filter && Math.Abs(_cutoff - _appliedCutoff) > _appliedCutoff * 0.01f)
            {
                for (int c = 0; c < 2; c++) _lp[c].SetLowPassFilter(_rate, _cutoff, 0.707f);
                _appliedCutoff = _cutoff;
            }

            int o = offset;
            for (int i = 0; i < frames; i++)
            {
                if (_gain < want) _gain = Math.Min(want, _gain + _fadeStep);
                else if (_gain > want) _gain = Math.Max(want, _gain - _fadeStep);
                float l = _stereo[i * 2], r = _stereo[i * 2 + 1];
                if (filter) { l = _lp[0].Transform(l); r = _lp[1].Transform(r); }
                l *= _gain; r *= _gain;
                if (_channels == 1)
                {
                    buffer[o++] = (l + r) * 0.5f;
                }
                else
                {
                    buffer[o++] = l;
                    buffer[o++] = r;
                    for (int c = 2; c < _channels; c++) buffer[o++] = 0f;
                }
            }
            return frames * _channels;
        }

        public void Dispose()
        {
            try { _out.Stop(); } catch { }
            try { _out.Dispose(); } catch { }
            try { _link.Dispose(); } catch { }
        }

        /// <summary>Отдаёт поток в формате устройства (float32, те же каналы и частота), без ресемплера.</summary>
        private sealed class MixFormatProvider : IWaveProvider
        {
            private readonly IWaveProvider _inner;
            public WaveFormat WaveFormat { get; }
            public MixFormatProvider(ISampleProvider src, WaveFormat mix)
            {
                _inner = new NAudio.Wave.SampleProviders.SampleToWaveProvider(src);
                WaveFormat = mix.Encoding == WaveFormatEncoding.IeeeFloat || mix.Encoding == WaveFormatEncoding.Extensible && mix.BitsPerSample == 32
                    ? mix
                    : src.WaveFormat;
            }
            public int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        }
    }
}
