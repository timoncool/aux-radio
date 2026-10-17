using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using CodeWalker.GameFiles;
using CodeWalker.Utils;

namespace GtaRadio.Logo;

/// <summary>
/// Замена логотипа станции в атласах колеса радио (hud.ytd, Gen9 / GTA V Enhanced).
///
/// YTD читается прямо из RPF через CodeWalker.Core: флаги ресурса (system/graphics pages)
/// хранятся в записи архива, поэтому файл из «gta-patch extract» без них не грузится
/// (DEEPSEEK-RESULT-6). Пиксели атласа меняются только внутри прямоугольника логотипа,
/// mip-уровни и сжатие пересобирает texconv (Microsoft DirectXTex) в тот же формат,
/// размер данных проверяется до записи.
///
///   list   --game G --rpf R --entry E
///   export --game G --rpf R --entry E --out DIR
///   patch  --game G --rpf R --entry E --texconv T --out DIR --item TEX:BOX:LOGO.png [--item ...]
///   put    --game G --rpf R --entry E --file PATCHED.ytd
///
/// R — путь к RPF относительно папки игры (например mods\update\update.rpf),
/// E — путь внутри него (например x64\data\cdimages\scaleform_generic.rpf\hud.ytd),
/// BOX — номер логотипа из export (boxes.txt) или x,y,w,h.
/// Ключи: --key BASE64 (AES-ключ, как у gta-patch); без него — из gta5_enhanced.exe.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var a = Args.Parse(args);
            if (a.Command == null) { Usage(); return 2; }
            string game = a.Req("game");
            GTA5Keys.LoadFromPath(game, true, a.Opt("key"));
            RpfManager.IsGen9 = true;

            switch (a.Command)
            {
                case "list": return List(a);
                case "export": return Export(a);
                case "patch": return Patch(a);
                case "put": return Put(a);
            }
            Usage();
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[ERROR] " + ex);
            return 1;
        }
    }

    private static void Usage() => Console.Error.WriteLine(
        "usage: list|export|patch|put --game <dir> --rpf <rel.rpf> --entry <path in rpf> [--out DIR] " +
        "[--texconv texconv.exe --item TEX:BOX:logo.png] [--file patched.ytd] [--key base64]");

    // ---------- RPF ----------

    private static (RpfFile root, RpfFileEntry entry) Open(Args a)
    {
        string game = a.Req("game"), rel = a.Req("rpf"), inner = a.Req("entry");
        string full = Path.Combine(game, rel);
        if (!File.Exists(full)) throw new FileNotFoundException(full);
        var root = new RpfFile(full, rel);
        root.ScanStructure(null, e => Console.Error.WriteLine("scan: " + e));
        string want = "\\" + inner.Replace('/', '\\').Trim('\\').ToLowerInvariant();
        RpfFileEntry? found = null;
        void Walk(RpfFile f)
        {
            if (f.AllEntries != null)
                foreach (var e in f.AllEntries)
                    if (e is RpfFileEntry fe && ("\\" + fe.Path.ToLowerInvariant()).EndsWith(want)) found ??= fe;
            if (f.Children != null) foreach (var c in f.Children) Walk(c);
        }
        Walk(root);
        if (found == null) throw new Exception("entry not found: " + inner + " in " + rel);
        Console.WriteLine("entry: " + found.Path);
        return (root, found);
    }

    private static YtdFile LoadYtd(RpfFileEntry entry)
    {
        var ytd = RpfFile.GetFile<YtdFile>(entry);
        if (ytd?.TextureDict?.Textures?.data_items == null || ytd.TextureDict.Textures.data_items.Length == 0)
            throw new Exception("ytd has no textures (wrong entry or keys?)");
        return ytd;
    }

    private static int List(Args a)
    {
        var (_, entry) = Open(a);
        foreach (var t in LoadYtd(entry).TextureDict.Textures.data_items)
            Console.WriteLine($"{t.Name}\t{t.Width}x{t.Height}\t{t.Format}\tlevels={t.Levels}\tbytes={t.Data?.FullData?.Length}");
        return 0;
    }

    // ---------- export ----------

    private static int Export(Args a)
    {
        var (_, entry) = Open(a);
        string outDir = a.Req("out");
        Directory.CreateDirectory(outDir);
        foreach (var t in LoadYtd(entry).TextureDict.Textures.data_items)
        {
            string name = t.Name ?? "tex";
            File.WriteAllBytes(Path.Combine(outDir, name + ".dds"), DDSIO.GetDDSFile(t));
            using var bmp = ToBitmap(t);
            bmp.Save(Path.Combine(outDir, name + ".png"), ImageFormat.Png);

            var boxes = FindLogos(bmp);
            using (var sheet = new Bitmap(bmp))
            using (var g = Graphics.FromImage(sheet))
            using (var pen = new Pen(Color.Red, 1))
            using (var font = new Font("Arial", 10, FontStyle.Bold))
            {
                for (int i = 0; i < boxes.Count; i++)
                {
                    g.DrawRectangle(pen, boxes[i]);
                    g.DrawString((i + 1).ToString(), font, Brushes.Red, boxes[i].X + 1, boxes[i].Y + 1);
                }
                sheet.Save(Path.Combine(outDir, name + "_boxes.png"), ImageFormat.Png);
            }
            File.WriteAllLines(Path.Combine(outDir, name + "_boxes.txt"),
                boxes.Select((b, i) => $"{i + 1} {b.X},{b.Y},{b.Width},{b.Height}"));
            Console.WriteLine($"{name}\t{t.Width}x{t.Height}\t{t.Format}\tlevels={t.Levels}\tlogos={boxes.Count}");
        }
        return 0;
    }

    /// <summary>Логотипы на прозрачном фоне: связные области по альфе (с расширением, чтобы буквы слились).</summary>
    private static List<Rectangle> FindLogos(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height, r = Math.Max(2, w / 128);
        var px = Read(bmp);
        var solid = new bool[w * h];
        for (int i = 0; i < w * h; i++) solid[i] = px[i * 4 + 3] > 24;
        var grow = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!solid[y * w + x]) continue;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx >= 0 && yy >= 0 && xx < w && yy < h) grow[yy * w + xx] = true;
                    }
            }
        var seen = new bool[w * h];
        var boxes = new List<Rectangle>();
        var q = new Queue<int>();
        for (int i = 0; i < w * h; i++)
        {
            if (!grow[i] || seen[i]) continue;
            int x0 = w, y0 = h, x1 = -1, y1 = -1, n = 0;
            seen[i] = true; q.Enqueue(i);
            while (q.Count > 0)
            {
                int p = q.Dequeue(), x = p % w, y = p / w;
                if (solid[p]) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); n++; }
                foreach (int nb in new[] { p - 1, p + 1, p - w, p + w })
                {
                    if (nb < 0 || nb >= w * h || seen[nb] || !grow[nb]) continue;
                    if ((nb == p - 1 && x == 0) || (nb == p + 1 && x == w - 1)) continue;
                    seen[nb] = true; q.Enqueue(nb);
                }
            }
            if (n > 40 && x1 >= x0) boxes.Add(new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1));
        }
        return boxes.OrderBy(b => b.Y / Math.Max(1, h / 16)).ThenBy(b => b.X).ToList();
    }

    // ---------- patch ----------

    private static int Patch(Args a)
    {
        var (_, entry) = Open(a);
        string outDir = a.Req("out"), texconv = a.Req("texconv");
        Directory.CreateDirectory(outDir);
        var ytd = LoadYtd(entry);

        foreach (var item in a.All("item"))
        {
            var parts = item.Split(':', 3);
            if (parts.Length != 3) throw new Exception("--item TEX:BOX:LOGO.png, got " + item);
            var tex = ytd.TextureDict.Textures.data_items.FirstOrDefault(t => string.Equals(t.Name, parts[0], StringComparison.OrdinalIgnoreCase))
                      ?? throw new Exception("texture not found: " + parts[0]);
            using var bmp = ToBitmap(tex);
            Rectangle box = ParseBox(parts[1], bmp);
            using (var logo = new Bitmap(parts[2]))
                DrawLogo(bmp, box, logo);
            string png = Path.Combine(outDir, tex.Name + "_patched.png");
            bmp.Save(png, ImageFormat.Png);

            // texconv: тот же формат и число mip-уровней.
            var fmt = DDSIO.GetDXGIFormat(tex.Format).ToString().Replace("DXGI_FORMAT_", "");
            Run(texconv, $"-nologo -y -f {fmt} -m {tex.Levels} -o \"{outDir}\" \"{png}\"");
            string dds = Path.Combine(outDir, Path.GetFileNameWithoutExtension(png) + ".dds");
            var src = DDSIO.GetTexture(File.ReadAllBytes(dds));
            if (src.Width != tex.Width || src.Height != tex.Height || src.Levels != tex.Levels || src.Format != tex.Format ||
                src.Data.FullData.Length != tex.Data.FullData.Length)
                throw new Exception($"texconv output differs: {src.Width}x{src.Height} {src.Format} l{src.Levels} " +
                                    $"{src.Data.FullData.Length}b vs {tex.Width}x{tex.Height} {tex.Format} l{tex.Levels} {tex.Data.FullData.Length}b");
            tex.Data.FullData = src.Data.FullData; // меняются только пиксели; заголовок текстуры (в т.ч. Gen9-поля) прежний
            Console.WriteLine($"[OK] {tex.Name}: box {box} <- {Path.GetFileName(parts[2])}");
        }

        byte[] data = ytd.Save();
        string outYtd = Path.Combine(outDir, entry.Name);
        File.WriteAllBytes(outYtd, data);
        // Самопроверка: собранный ресурс читается обратно.
        var check = new YtdFile();
        check.Load(data);
        Console.WriteLine($"[OK] {outYtd} ({data.Length} b), textures back: {check.TextureDict?.Textures?.data_items?.Length}");
        return 0;
    }

    private static Rectangle ParseBox(string s, Bitmap bmp)
    {
        if (s.Contains(','))
        {
            var v = s.Split(',').Select(int.Parse).ToArray();
            return new Rectangle(v[0], v[1], v[2], v[3]);
        }
        var boxes = FindLogos(bmp);
        int n = int.Parse(s);
        if (n < 1 || n > boxes.Count) throw new Exception($"box {n} out of range 1..{boxes.Count}");
        return boxes[n - 1];
    }

    /// <summary>Стереть старый логотип в прямоугольнике и вписать новый по центру, с сохранением пропорций.</summary>
    private static void DrawLogo(Bitmap atlas, Rectangle box, Bitmap logo)
    {
        var clear = Rectangle.Inflate(box, 2, 2);
        clear.Intersect(new Rectangle(0, 0, atlas.Width, atlas.Height));
        using var g = Graphics.FromImage(atlas);
        g.CompositingMode = CompositingMode.SourceCopy;
        using (var transparent = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
            g.FillRectangle(transparent, clear);
        g.CompositingMode = CompositingMode.SourceOver;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float k = Math.Min((float)box.Width / logo.Width, (float)box.Height / logo.Height);
        int w = (int)Math.Round(logo.Width * k), h = (int)Math.Round(logo.Height * k);
        g.DrawImage(logo, new Rectangle(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h));
    }

    // ---------- put ----------

    private static int Put(Args a)
    {
        var (_, entry) = Open(a);
        byte[] data = File.ReadAllBytes(a.Req("file"));
        if (data.Length < 16 || BitConverter.ToUInt32(data, 0) != 0x37435352)
            throw new Exception("file is not an RSC7 resource (use the output of patch)");
        var dir = entry.Parent ?? throw new Exception("entry has no parent dir");
        RpfFile.CreateFile(dir, entry.Name, data, true);
        Console.WriteLine("[OK] written " + entry.Path);
        return 0;
    }

    // ---------- pixels ----------

    /// <summary>Mip 0 -> Bitmap (DDSIO.GetPixels отдаёт BGRA, как Format32bppArgb в памяти).</summary>
    private static Bitmap ToBitmap(Texture t)
    {
        byte[] px = DDSIO.GetPixels(t, 0);
        var bmp = new Bitmap(t.Width, t.Height, PixelFormat.Format32bppArgb);
        var d = bmp.LockBits(new Rectangle(0, 0, t.Width, t.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < t.Height; y++)
            Marshal.Copy(px, y * t.Width * 4, d.Scan0 + y * d.Stride, t.Width * 4);
        bmp.UnlockBits(d);
        return bmp;
    }

    private static byte[] Read(Bitmap bmp)
    {
        var px = new byte[bmp.Width * bmp.Height * 4];
        var d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < bmp.Height; y++)
            Marshal.Copy(d.Scan0 + y * d.Stride, px, y * bmp.Width * 4, bmp.Width * 4);
        bmp.UnlockBits(d);
        return px;
    }

    private static void Run(string exe, string args)
    {
        var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })!;
        string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new Exception("texconv failed: " + o);
    }

    private sealed class Args
    {
        public string? Command;
        private readonly List<(string K, string V)> _kv = new();
        public static Args Parse(string[] a)
        {
            var r = new Args();
            int i = 0;
            if (a.Length > 0 && !a[0].StartsWith("--")) r.Command = a[i++].ToLowerInvariant();
            for (; i + 1 < a.Length; i += 2) r._kv.Add((a[i].TrimStart('-').ToLowerInvariant(), a[i + 1]));
            return r;
        }
        public string? Opt(string k) => _kv.LastOrDefault(x => x.K == k).V;
        public string Req(string k) => Opt(k) ?? throw new Exception("missing --" + k);
        public IEnumerable<string> All(string k) => _kv.Where(x => x.K == k).Select(x => x.V);
    }
}
