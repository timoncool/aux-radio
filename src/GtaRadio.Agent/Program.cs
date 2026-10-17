using System.Windows.Forms;

namespace GtaRadio.Agent;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        using var mutex = new Mutex(true, "GtaRadio.Agent.SingleInstance", out bool created);
        if (!created) return 0;

        // Прошлый запуск мог упасть с приглушённым плеером — возвращаем громкость сразу.
        SessionVolumes.RestoreAll();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => SessionVolumes.RestoreAll();

        ApplicationConfiguration.Initialize();
        var app = new AgentApp();
        if (args.Contains("--status")) app.ShowStatus();
        Application.Run(app);
        return 0;
    }
}
