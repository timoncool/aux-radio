namespace GtaRadio;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var diagIdx = Array.FindIndex(args, a => a.Equals("--diag", StringComparison.OrdinalIgnoreCase));
        if (diagIdx >= 0)
        {
            var token = diagIdx + 1 < args.Length ? args[diagIdx + 1] : "Яндекс";
            var device = diagIdx + 2 < args.Length ? args[diagIdx + 2] : null;
            Diag.Run(token, device);
            return;
        }

        var verIdx = Array.FindIndex(args, a => a.Equals("--verify", StringComparison.OrdinalIgnoreCase));
        if (verIdx >= 0)
        {
            var token = verIdx + 1 < args.Length ? args[verIdx + 1] : "Яндекс";
            Diag.Verify(token);
            return;
        }

        var fixIdx = Array.FindIndex(args, a => a.Equals("--fix", StringComparison.OrdinalIgnoreCase));
        if (fixIdx >= 0)
        {
            var token = fixIdx + 1 < args.Length ? args[fixIdx + 1] : "Spotify";
            Diag.Fix(token);
            return;
        }

        var recIdx = Array.FindIndex(args, a => a.Equals("--rec", StringComparison.OrdinalIgnoreCase));
        if (recIdx >= 0)
        {
            var token = recIdx + 1 < args.Length ? args[recIdx + 1] : "Яндекс";
            Diag.Record(token, 5);
            return;
        }

        var mixIdx = Array.FindIndex(args, a => a.Equals("--mix", StringComparison.OrdinalIgnoreCase));
        if (mixIdx >= 0)
        {
            Diag.Mix(null);
            return;
        }

        var unmuteIdx = Array.FindIndex(args, a => a.Equals("--unmute", StringComparison.OrdinalIgnoreCase));
        if (unmuteIdx >= 0)
        {
            var token = unmuteIdx + 1 < args.Length ? args[unmuteIdx + 1] : "Spotify";
            Diag.Unmute(token);
            return;
        }

        using var mutex = new Mutex(true, "GtaRadio_SingleInstance", out bool created);
        if (!created) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp(args.Any(a => a.Equals("--status", StringComparison.OrdinalIgnoreCase))));
    }
}
