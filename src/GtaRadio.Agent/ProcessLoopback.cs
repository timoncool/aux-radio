using System.Runtime.InteropServices;

namespace GtaRadio.Agent;

/// <summary>
/// Захват звука процесса и его дочерних (Application Loopback, Windows 10 2004+ / 20348+).
/// Схема как в официальном примере Microsoft ApplicationLoopback:
/// ActivateAudioInterfaceAsync(VAD\Process_Loopback) на MTA-потоке,
/// Initialize(LOOPBACK | EVENTCALLBACK | AUTOCONVERTPCM), чтение по событию.
/// </summary>
internal static class ProcessLoopback
{
    private const string VAD = @"VAD\Process_Loopback";
    private const int AUDCLNT_SHAREMODE_SHARED = 0;
    private const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
    private const uint AUDCLNT_STREAMFLAGS_EVENTCALLBACK = 0x00040000;
    private const uint AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY = 0x08000000;
    private const uint AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM = 0x80000000;
    private const uint AUDCLNT_BUFFERFLAGS_SILENT = 0x2;

    // AUDIOCLIENT_ACTIVATION_TYPE: DEFAULT = 0, PROCESS_LOOPBACK = 1.
    private const int AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK = 1;

    // PROCESS_LOOPBACK_MODE: INCLUDE_TARGET_PROCESS_TREE = 0, EXCLUDE_TARGET_PROCESS_TREE = 1.
    // В legacy-клиенте стояла 1 (EXCLUDE): захватывалось всё, кроме плеера, включая собственный
    // вывод клиента -> петля обратной связи -> зацикленный гул.
    private const int PROCESS_LOOPBACK_MODE_INCLUDE_TARGET_PROCESS_TREE = 0;

    private const ushort VT_BLOB = 65;

    [StructLayout(LayoutKind.Sequential)]
    private struct AUDIOCLIENT_ACTIVATION_PARAMS
    {
        public int ActivationType;
        public uint TargetProcessId;
        public int ProcessLoopbackMode;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public uint blobSize;
        [FieldOffset(16)] public IntPtr blobData;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [ComImport, Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceCompletionHandler
    {
        void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation);
    }

    [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceAsyncOperation
    {
        void GetActivateResult(out int activateResult, [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
    }

    // Обработчик вызывается из рабочего потока MMDevAPI; он обязан быть agile (IAgileObject).
    [ComImport, Guid("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAgileObject { }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr format, IntPtr audioSessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint frames);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

    [DllImport("Mmdevapi.dll", ExactSpelling = true)]
    private static extern int ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        [In] ref Guid riid,
        IntPtr activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation activationOperation);

    [ClassInterface(ClassInterfaceType.None)]
    private sealed class Handler : IActivateAudioInterfaceCompletionHandler, IAgileObject
    {
        public readonly ManualResetEventSlim Done = new(false);
        public IAudioClient? Client;
        public int Hr = -1;
        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation op)
        {
            try
            {
                op.GetActivateResult(out Hr, out object iface);
                Client = iface as IAudioClient;
            }
            catch (Exception ex) { Hr = ex.HResult; }
            Done.Set();
        }
    }

    public sealed class Handle
    {
        public required IAudioClient Client;
        public required IAudioCaptureClient Capture;
        public required WAVEFORMATEX Format;
        public required AutoResetEvent Ready;
        public string Error = "";
    }

    /// <summary>
    /// Открывает захват дерева процессов pid в формате float32 / channels / sampleRate.
    /// Вызывать на MTA-потоке. При ошибке возвращает null и текст в error.
    /// </summary>
    public static Handle? Open(uint pid, int sampleRate, int channels, out string error)
    {
        error = "";
        var parms = new AUDIOCLIENT_ACTIVATION_PARAMS
        {
            ActivationType = AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK,
            TargetProcessId = pid,
            ProcessLoopbackMode = PROCESS_LOOPBACK_MODE_INCLUDE_TARGET_PROCESS_TREE
        };
        int size = Marshal.SizeOf<AUDIOCLIENT_ACTIVATION_PARAMS>();
        IntPtr blob = Marshal.AllocHGlobal(size);
        IntPtr pvPtr = Marshal.AllocHGlobal(Marshal.SizeOf<PROPVARIANT>());
        IntPtr fmtPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WAVEFORMATEX>());
        try
        {
            Marshal.StructureToPtr(parms, blob, false);
            var pv = new PROPVARIANT { vt = VT_BLOB, blobSize = (uint)size, blobData = blob };
            Marshal.StructureToPtr(pv, pvPtr, false);

            var iid = typeof(IAudioClient).GUID;
            var handler = new Handler();
            int hr = ActivateAudioInterfaceAsync(VAD, ref iid, pvPtr, handler, out _);
            if (hr != 0) { error = $"ActivateAudioInterfaceAsync hr=0x{hr:X8}"; return null; }
            if (!handler.Done.Wait(5000)) { error = "activation timeout"; return null; }
            if (handler.Hr != 0 || handler.Client == null) { error = $"activate result hr=0x{handler.Hr:X8}"; return null; }
            var client = handler.Client;

            var fmt = new WAVEFORMATEX
            {
                wFormatTag = 3, // WAVE_FORMAT_IEEE_FLOAT
                nChannels = (ushort)channels,
                nSamplesPerSec = (uint)sampleRate,
                wBitsPerSample = 32,
                nBlockAlign = (ushort)(channels * 4),
                nAvgBytesPerSec = (uint)(sampleRate * channels * 4),
                cbSize = 0
            };
            Marshal.StructureToPtr(fmt, fmtPtr, false);

            // Длительности 0/0 и флаги — как в официальном примере; формат сконвертирует движок.
            hr = client.Initialize(AUDCLNT_SHAREMODE_SHARED,
                AUDCLNT_STREAMFLAGS_LOOPBACK | AUDCLNT_STREAMFLAGS_EVENTCALLBACK |
                AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM | AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY,
                0, 0, fmtPtr, IntPtr.Zero);
            if (hr != 0) { error = $"Initialize hr=0x{hr:X8}"; return null; }

            var ready = new AutoResetEvent(false);
            hr = client.SetEventHandle(ready.SafeWaitHandle.DangerousGetHandle());
            if (hr != 0) { error = $"SetEventHandle hr=0x{hr:X8}"; return null; }

            var capIid = typeof(IAudioCaptureClient).GUID;
            hr = client.GetService(ref capIid, out object capObj);
            if (hr != 0 || capObj is not IAudioCaptureClient cap) { error = $"GetService hr=0x{hr:X8}"; return null; }

            hr = client.Start();
            if (hr != 0) { error = $"Start hr=0x{hr:X8}"; return null; }

            return new Handle { Client = client, Capture = cap, Format = fmt, Ready = ready };
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(fmtPtr);
            Marshal.FreeHGlobal(pvPtr);
            Marshal.FreeHGlobal(blob);
        }
    }

    /// <summary>
    /// Читает пакеты до отмены. Пакеты с флагом SILENT отдаются нулями той же длины,
    /// чтобы не рвать тайминг. onData получает float-сэмплы (interleaved) и их число.
    /// </summary>
    public static void Pump(Handle h, Action<float[], int> onData, CancellationToken ct)
    {
        int ch = h.Format.nChannels;
        var buf = new float[48000 * ch];
        while (!ct.IsCancellationRequested)
        {
            h.Ready.WaitOne(100);
            while (true)
            {
                if (h.Capture.GetNextPacketSize(out uint packet) != 0 || packet == 0) break;
                if (h.Capture.GetBuffer(out IntPtr data, out uint frames, out uint flags, out _, out _) != 0) break;
                int samples = (int)frames * ch;
                if (samples > buf.Length) buf = new float[samples];
                if ((flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0 || data == IntPtr.Zero)
                    Array.Clear(buf, 0, samples);
                else
                    Marshal.Copy(data, buf, 0, samples);
                h.Capture.ReleaseBuffer(frames);
                if (samples > 0) onData(buf, samples);
            }
        }
    }

    public static void Close(Handle h)
    {
        try { h.Client.Stop(); } catch { }
        try { Marshal.ReleaseComObject(h.Capture); } catch { }
        try { Marshal.ReleaseComObject(h.Client); } catch { }
        try { h.Ready.Dispose(); } catch { }
    }
}
