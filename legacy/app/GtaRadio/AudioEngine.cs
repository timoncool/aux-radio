using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GtaRadio;

/// <summary>
/// Тракт: захват процесса плеера -> глушение его сессий -> воспроизведение через NAudio.
/// Ничего в системе не перенастраивается.
/// </summary>
internal sealed class FormatOverrideProvider : IWaveProvider
{
    private readonly IWaveProvider _inner;
    public WaveFormat WaveFormat { get; }
    public FormatOverrideProvider(IWaveProvider inner, WaveFormat wf) { _inner = inner; WaveFormat = wf; }
    public int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
}

public sealed class AudioEngine : IDisposable
{
    private ProcessLoopback.Handle? _cap;
    private CancellationTokenSource? _cts;
    private Thread? _thread;
    private BufferedWaveProvider? _buffer;
    private WasapiOut? _out;
    private VolumeSampleProvider? _volume;
    private readonly List<(uint Pid, bool WasMuted)> _muted = new();
    private readonly object _lock = new();

    public float Volume { get; set; } = 1f;
    public float DuckVolume { get; set; } = 0.15f;
    public bool IsRunning { get; private set; }
    public uint CurrentPid { get; private set; }
    public string? CurrentAppId { get; private set; }
    public string OutputName { get; private set; } = "";
    public string? DeviceMatch { get; set; }
    public double Level { get; private set; }
    private MMDeviceEnumerator? _en;

    private static string? FindDeviceIdForPid(uint pid)
    {
        if (pid == 0) return null;
        try
        {
            using var en = new MMDeviceEnumerator();
            foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                try
                {
                    var ss = d.AudioSessionManager.Sessions;
                    for (int i = 0; i < ss.Count; i++)
                        if (ss[i].GetProcessID == pid) return d.ID;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    public bool Start(uint pid, IEnumerable<uint> pids, string appId, uint gamePid = 0)
    {
        Stop();
        lock (_lock)
        {
            var pidSet = pids.Append(pid).Distinct().ToHashSet();
            MutePids(pidSet, true);

            ProcessLoopback.Handle? cap = null;
            var openThread = new Thread(() => { try { cap = ProcessLoopback.Open(pid); } catch { cap = null; } });
            openThread.SetApartmentState(ApartmentState.MTA);
            openThread.Start();
            openThread.Join(6000);
            if (cap == null)
            {
                RestoreMutes();
                return false;
            }
            Diag.W($"cap fmt tag={cap.Format.wFormatTag} bits={cap.Format.wBitsPerSample} ch={cap.Format.nChannels} sr={cap.Format.nSamplesPerSec} align={cap.Format.nBlockAlign}");
            _cap = cap;
            var wf = cap.Format.wFormatTag == 3
                ? WaveFormat.CreateIeeeFloatWaveFormat((int)cap.Format.nSamplesPerSec, cap.Format.nChannels)
                : new WaveFormat((int)cap.Format.nSamplesPerSec, cap.Format.wBitsPerSample, cap.Format.nChannels);

            _buffer = new BufferedWaveProvider(wf)
            {
                BufferDuration = TimeSpan.FromSeconds(4),
                DiscardOnBufferOverflow = true,
                ReadFully = true
            };
            _volume = new VolumeSampleProvider(_buffer.ToSampleProvider()) { Volume = Volume };
            var id = FindDeviceIdForPid(gamePid);
            _en = new MMDeviceEnumerator();
            MMDevice dev = null;
            if (!string.IsNullOrEmpty(DeviceMatch))
            {
                foreach (var d in _en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    if (d.FriendlyName.Contains(DeviceMatch, StringComparison.OrdinalIgnoreCase)) { dev = d; break; }
                }
            }
            if (dev == null)
            {
                if (id != null) dev = _en.GetDevice(id);
                else dev = _en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            OutputName = dev.FriendlyName;
            var mix = dev.AudioClient.MixFormat;
            Diag.W($"mix sr={mix.SampleRate} bits={mix.BitsPerSample} ch={mix.Channels} enc={mix.Encoding}");

            ISampleProvider sp = new VolumeSampleProvider(_buffer.ToSampleProvider()) { Volume = Volume };
            _volume = (VolumeSampleProvider)sp;
            if (sp.WaveFormat.SampleRate != mix.SampleRate)
                sp = new WdlResamplingSampleProvider(sp, mix.SampleRate);
            if (sp.WaveFormat.Channels == 1 && mix.Channels == 2)
                sp = new MonoToStereoSampleProvider(sp);
            else if (sp.WaveFormat.Channels == 2 && mix.Channels == 1)
                sp = sp.ToMono();

            bool isFloat = mix.Encoding == WaveFormatEncoding.IeeeFloat;
            var mixExt = mix as WaveFormatExtensible;
            if (!isFloat && mixExt != null &&
                mixExt.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71"))
                isFloat = true;

            IWaveProvider playback = isFloat
                ? sp.ToWaveProvider()
                : sp.ToWaveProvider16();
            if (isFloat) playback = new FormatOverrideProvider(playback, mix);
            _out = new WasapiOut(dev, AudioClientShareMode.Shared, true, 100);
            _out.Init(playback);
            _out.Play();

            CurrentPid = pid;
            CurrentAppId = appId;
            IsRunning = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var buffer = _buffer;
            var handle = cap;
            _thread = new Thread(() =>
            {
                try
                {
                    ProcessLoopback.Pump(handle, int.MaxValue, (buf, len) =>
                    {
                        try { buffer.AddSamples(buf, 0, len); } catch { }
                    }, token, lvl => Level = lvl);
                }
                catch { }
            }) { IsBackground = true, Name = "GtaRadioCapture" };
            _thread.Start();
            return true;
        }
    }

    public void SetDuck(bool duck)
    {
        if (_volume != null) _volume.Volume = duck ? DuckVolume : Volume;
    }

    public void ApplyVolume()
    {
        if (_volume != null) _volume.Volume = Volume;
    }

    public void Stop()
    {
        lock (_lock)
        {
            try { _cts?.Cancel(); } catch { }
            try { _out?.Stop(); } catch { }
            try { _out?.Dispose(); } catch { }
            _out = null;
            _volume = null;
            _buffer = null;
            if (_cap != null) { try { ProcessLoopback.Close(_cap); } catch { } _cap = null; }
            try { _cts?.Dispose(); } catch { }
            _cts = null;
            _thread = null;
            try { _en?.Dispose(); } catch { }
            _en = null;
            RestoreMutes();
            IsRunning = false;
            Level = 0;
        }
    }

    private void MutePids(HashSet<uint> pids, bool mute)
    {
        try
        {
            using var en = new MMDeviceEnumerator();
            foreach (var dev in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                try
                {
                    var sessions = dev.AudioSessionManager.Sessions;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        try
                        {
                            var s = sessions[i];
                            uint pid = s.GetProcessID;
                            if (!pids.Contains(pid)) continue;
                            var vol = s.SimpleAudioVolume;
                            if (mute)
                            {
                                _muted.Add((pid, vol.Mute));
                                vol.Mute = true;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private void RestoreMutes()
    {
        try
        {
            using var en = new MMDeviceEnumerator();
            foreach (var dev in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                try
                {
                    var sessions = dev.AudioSessionManager.Sessions;
                    foreach (var (pid, wasMuted) in _muted)
                    {
                        for (int i = 0; i < sessions.Count; i++)
                        {
                            try
                            {
                                var s = sessions[i];
                                if (s.GetProcessID == pid) s.SimpleAudioVolume.Mute = wasMuted;
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
        _muted.Clear();
    }

    public void Dispose()
    {
        Stop();
    }
}
