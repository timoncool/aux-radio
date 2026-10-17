using System;
using System.IO;
using CodeWalker.GameFiles;
using CodeWalker.Utils;

// YtdTool: экспорт текстур из Gen9 YTD в DDS и замена текстуры из DDS.
// export <hud.ytd> <outDir>
// import <hud.ytd> <textureName> <new.dds> <out.ytd>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("usage: export <ytd> <outDir> | import <ytd> <name> <dds> <outYtd>");
            return 2;
        }

        RpfManager.IsGen9 = true;
        var ytd = new YtdFile();
        var raw = File.ReadAllBytes(args[1]);
        if (raw.Length > 4 && BitConverter.ToUInt32(raw, 0) == 0x37435352)
        {
            ytd.Load(raw);
        }
        else
        {
            // gta-patch отдаёт уже распакованный ресурс без RSC7-заголовка.
            var entry = new RpfResourceFileEntry();
            ytd.Load(raw, entry);
        }

        if (args[0] == "export")
        {
            Directory.CreateDirectory(args[2]);
            foreach (var kv in ytd.TextureDict.Dict)
            {
                var t = kv.Value;
                var name = t.Name ?? kv.Key.ToString("X8");
                var dds = DDSIO.GetDDSFile(t);
                var path = Path.Combine(args[2], name + ".dds");
                File.WriteAllBytes(path, dds);
                Console.WriteLine($"{name}\t{t.Width}x{t.Height}\t{t.Format}\tlevels={t.Levels}\t{dds.Length}");
            }
            return 0;
        }

        if (args[0] == "import" && args.Length >= 5)
        {
            var name = args[2];
            var tex = ytd.TextureDict.Lookup(GTA5Hash.CalculateHash(name));
            if (tex == null)
            {
                Console.WriteLine("texture not found: " + name);
                return 3;
            }
            var src = DDSIO.GetTexture(File.ReadAllBytes(args[3]));
            if (src == null)
            {
                Console.WriteLine("bad dds");
                return 4;
            }
            tex.Data = src.Data;
            tex.Width = src.Width;
            tex.Height = src.Height;
            tex.Depth = src.Depth;
            tex.Levels = src.Levels;
            tex.Format = src.Format;
            tex.Stride = src.Stride;
            File.WriteAllBytes(args[4], ytd.Save());
            Console.WriteLine("written " + args[4]);
            return 0;
        }

        Console.WriteLine("unknown command");
        return 2;
    }
}
