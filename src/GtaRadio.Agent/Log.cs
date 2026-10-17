namespace GtaRadio.Agent;

internal static class Log
{
    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaRadio");
    private static readonly object Lock = new();

    public static void W(string msg)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Dir);
                var path = Path.Combine(Dir, "agent.log");
                if (File.Exists(path) && new FileInfo(path).Length > 2_000_000) File.Delete(path);
                File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff ") + msg + Environment.NewLine);
            }
        }
        catch { }
    }
}
