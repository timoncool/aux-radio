using CodeWalker.GameFiles;

if (args.Length < 3)
{
    Console.WriteLine("usage: YtdProbe <game> <rpf> <entry-name>");
    return;
}

var game = args[0];
var rpfPath = args[1];
var name = args[2].ToLowerInvariant();

GTA5Keys.LoadFromPath(game, true, null);
RpfManager.IsGen9 = true;

var rf = new RpfFile(rpfPath, Path.GetFileName(rpfPath));
rf.ScanStructure(null, null);

RpfFileEntry Find(RpfFile r)
{
    foreach (var e in r.AllEntries)
        if (e is RpfFileEntry fe && fe.NameLower == name) return fe;
    foreach (var c in r.Children ?? new List<RpfFile>())
    {
        var f = Find(c);
        if (f != null) return f;
    }
    return null;
}

var entry = Find(rf);
if (entry == null) { Console.WriteLine("entry not found: " + name); return; }
var data = entry.File.ExtractFile(entry);
Console.WriteLine($"entry: {entry.Path} size={data.Length} type={entry.GetType().Name}");

var ytd = new YtdFile(entry);
ytd.Load(data, entry);
var dict = ytd.TextureDict;
if (dict?.Textures?.data_items == null) { Console.WriteLine("no textures"); return; }
foreach (var t in dict.Textures.data_items)
{
    if (t == null) continue;
    Console.WriteLine($"{t.Name}\t{t.Width}x{t.Height}\t{t.Format}\tlevels={t.Levels}\tdata={(t.Data != null)}");
}
