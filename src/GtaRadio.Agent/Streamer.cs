using GtaRadio.Shared;

namespace GtaRadio.Agent;

/// <summary>
/// Поток плеера в игру: process loopback (INCLUDE дерева процессов плеера) -> компенсация
/// приглушения -> кольцо RadioLink. Сам плеер приглушается в микшере Windows (громкость
/// приложения, не устройства) до Attenuation, чтобы не было двойного звука.
///
/// Захват процесса идёт после громкости приложения (так ведёт себя и захват приложений в OBS:
/// приглушённое в микшере приложение приглушено и в захвате), поэтому полный мьют нельзя,
/// а приглушение компенсируется усилением 1/Attenuation в float32 без потери качества.
/// Если на этой машине захват окажется до громкости приложения, это видно по уровню сразу
/// после приглушения, и компенсация отключается.
/// </summary>
internal sealed class Streamer : IDisposable
{
    private readonly RadioLink _link;
    private readonly float _attenuation;
    private ProcessLoopback.Handle? _cap;
    private Thread? _thread;
    private CancellationTokenSource? _cts;
    private HashSet<uint> _tree = new();
    private uint _gamePid;
    private static float? _calibratedMakeup;

    public uint RootPid { get; private set; }
    public bool Running { get; private set; }
    public string Error { get; private set; } = "";
    public double Level { get; private set; }

    public Streamer(RadioLink link, float attenuation)
    {
        _link = link;
        _attenuation = Math.Clamp(attenuation, 0.0001f, 1f);
    }

    public bool Start(uint rootPid, uint gamePid)
    {
        Stop();
        Error = "";
        _gamePid = gamePid;
        int rate = _link.SampleRate > 8000 ? _link.SampleRate : 48000;

        ProcessLoopback.Handle? cap = null;
        string err = "";
        var open = new Thread(() =>
        {
            try { cap = ProcessLoopback.Open(rootPid, rate, RadioLink.Channels, out err); }
            catch (Exception ex) { err = ex.Message; }
        });
        open.SetApartmentState(ApartmentState.MTA);
        open.Start();
        open.Join(7000);
        if (cap == null)
        {
            Error = "capture: " + err;
            Log.W("[ERROR] " + Error);
            return false;
        }

        _cap = cap;
        RootPid = rootPid;
        _tree = Tree(rootPid);
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _thread = new Thread(() => Pump(cap, ct)) { IsBackground = true, Name = "GtaRadioCapture" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
        Running = true;
        Log.W($"[OK] capture pid={rootPid} rate={rate} tree={_tree.Count}");
        return true;
    }

    private HashSet<uint> Tree(uint root)
    {
        var t = ProcessTree.Descendants(root);
        t.Remove((uint)Environment.ProcessId);
        if (_gamePid != 0) t.Remove(_gamePid);
        return t;
    }

    private void Pump(ProcessLoopback.Handle cap, CancellationToken ct)
    {
        // Фаза 1: плеер звучит как обычно, меряем уровень захвата (в игру ничего не пишем).
        // Фаза 2: приглушаем плеер, меряем снова. Отношение уровней показывает, где стоит захват.
        // Фаза 3: поток в игру с компенсацией.
        int rate = (int)cap.Format.nSamplesPerSec;
        int phase = 1;
        if (_calibratedMakeup.HasValue)
        {
            // Уже знаем, где стоит захват на этой машине: сразу приглушаем и стримим.
            SessionVolumes.Set(_tree, _attenuation);
            phase = 3;
        }
        double sum = 0; long n = 0;
        double before = 0;
        long phaseStart = Environment.TickCount64;
        float makeup = _calibratedMakeup ?? 1f / _attenuation;
        var outBuf = new float[rate * RadioLink.Channels];
        double lvlSum = 0; long lvlN = 0; long lvlStart = Environment.TickCount64;

        try
        {
            ProcessLoopback.Pump(cap, (samples, count) =>
            {
                long now = Environment.TickCount64;
                if (phase < 3)
                {
                    for (int i = 0; i < count; i++) { sum += samples[i] * samples[i]; n++; }
                    double rms = n > 0 ? Math.Sqrt(sum / n) : 0;
                    if (phase == 1 && now - phaseStart >= 300 && (rms > 0.002 || now - phaseStart >= 3000))
                    {
                        before = rms;
                        SessionVolumes.Set(_tree, _attenuation);
                        phase = 2; sum = 0; n = 0; phaseStart = now;
                    }
                    else if (phase == 2 && now - phaseStart >= 250)
                    {
                        // Захват до громкости приложения: уровень почти не упал -> компенсация не нужна.
                        if (before > 0.002 && rms > before * 0.2) makeup = 1f;
                        Log.W($"calibrate before={before:F5} after={rms:F6} makeup={makeup:F0}");
                        if (before > 0.002) _calibratedMakeup = makeup;
                        phase = 3;
                    }
                    return;
                }

                if (outBuf.Length < count) outBuf = new float[count];
                for (int i = 0; i < count; i++)
                {
                    float v = samples[i] * makeup;
                    // Мягкое ограничение выше 0.9 вместо жёсткого клиппинга.
                    float a = Math.Abs(v);
                    if (a > 0.9f) v = Math.Sign(v) * (0.9f + 0.1f * MathF.Tanh((a - 0.9f) / 0.1f));
                    outBuf[i] = v;
                    lvlSum += outBuf[i] * outBuf[i]; lvlN++;
                }
                _link.WriteFrames(outBuf, count / RadioLink.Channels);
                if (now - lvlStart >= 500)
                {
                    Level = lvlN > 0 ? Math.Sqrt(lvlSum / lvlN) : 0;
                    lvlSum = 0; lvlN = 0; lvlStart = now;
                }
            }, ct);
        }
        catch (Exception ex) { Log.W("[ERROR] pump " + ex.Message); }
        finally { ProcessLoopback.Close(cap); } // COM-объекты захвата закрываются в своём MTA-потоке
    }

    /// <summary>Electron/CEF пересоздают аудиосессии при смене трека — приглушаем новые.</summary>
    public void Maintain()
    {
        if (!Running || RootPid == 0) return;
        _tree = Tree(RootPid);
        SessionVolumes.Set(_tree, _attenuation);
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _thread?.Join(1000); } catch { }
        _cap = null;
        try { _cts?.Dispose(); } catch { }
        _cts = null;
        _thread = null;
        if (Running) SessionVolumes.RestoreAll();
        Running = false;
        RootPid = 0;
        Level = 0;
    }

    public void Dispose() => Stop();
}
