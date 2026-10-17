using System.IO.Pipes;
using System.Text;

namespace GtaRadio;

/// <summary>Мост к игровому моду: статус в игру, команды из игры.</summary>
public sealed class ModPipe : IDisposable
{
    public const string PipeName = "gta_radio_pipe";
    public Func<string>? StatusFactory { get; set; }
    public event Action<string>? Command;

    private readonly CancellationTokenSource _cts = new();

    public void Start() => Task.Run(AcceptLoop);

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var srv = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await srv.WaitForConnectionAsync(_cts.Token);
                _ = Task.Run(() => WriterLoop(srv, _cts.Token));
                using var reader = new StreamReader(srv, new UTF8Encoding(false));
                while (srv.IsConnected && !_cts.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line == null) break;
                    if (line.Length > 0) Command?.Invoke(line);
                }
            }
            catch { }
            try { await Task.Delay(800, _cts.Token); } catch { return; }
        }
    }

    private async Task WriterLoop(Stream s, CancellationToken ct)
    {
        try
        {
            using var w = new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true };
            while (!ct.IsCancellationRequested && s.CanWrite)
            {
                try { await w.WriteLineAsync(StatusFactory?.Invoke() ?? "active=0"); }
                catch { return; }
                try { await Task.Delay(400, ct); } catch { return; }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _cts.Dispose(); } catch { }
    }
}
