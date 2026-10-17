using System.Text;

namespace GtaRadio.Gxt2;

/// <summary>
/// Правка одной строки в global.gxt2 (имя станции). Остальные строки, в том числе перевод
/// и радио-озвучка, переписываются байт в байт в исходном порядке.
///
/// Формат: "2TXG" | u32 count | count x (u32 hash, u32 offset) | "2TXG" | u32 fileSize | строки UTF-8 c \0.
/// Смещения абсолютные от начала файла.
///
/// GtaRadio.Gxt2.exe get &lt;file&gt; &lt;label|0xHASH&gt;
/// GtaRadio.Gxt2.exe set &lt;file&gt; &lt;label|0xHASH&gt; &lt;text&gt;
/// </summary>
internal static class Program
{
    private const uint Magic = 0x47585432; // "2TXG" в little-endian

    public static uint Joaat(string s)
    {
        uint h = 0;
        foreach (byte c in Encoding.UTF8.GetBytes(s.ToLowerInvariant()))
        {
            h += c; h += h << 10; h ^= h >> 6;
        }
        h += h << 3; h ^= h >> 11; h += h << 15;
        return h;
    }

    private static uint ParseLabel(string label) =>
        label.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToUInt32(label[2..], 16)
            : Joaat(label);

    public static List<(uint Hash, string Text)> Read(byte[] data)
    {
        if (data.Length < 16 || BitConverter.ToUInt32(data, 0) != Magic) throw new InvalidDataException("not a GXT2 file");
        int count = BitConverter.ToInt32(data, 4);
        int table = 8;
        int after = table + count * 8;
        if (count < 0 || after + 8 > data.Length) throw new InvalidDataException("bad entry count");
        if (BitConverter.ToUInt32(data, after) != Magic) throw new InvalidDataException("second magic missing");
        var list = new List<(uint, string)>(count);
        for (int i = 0; i < count; i++)
        {
            uint hash = BitConverter.ToUInt32(data, table + i * 8);
            int off = BitConverter.ToInt32(data, table + i * 8 + 4);
            if (off < after + 8 || off >= data.Length) throw new InvalidDataException("bad string offset");
            int end = Array.IndexOf(data, (byte)0, off);
            if (end < 0) throw new InvalidDataException("unterminated string");
            list.Add((hash, Encoding.UTF8.GetString(data, off, end - off)));
        }
        return list;
    }

    public static byte[] Write(List<(uint Hash, string Text)> entries)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(Magic);
        w.Write(entries.Count);
        int dataStart = 8 + entries.Count * 8 + 8;
        int off = dataStart;
        var blobs = new List<byte[]>(entries.Count);
        foreach (var (hash, text) in entries)
        {
            var b = Encoding.UTF8.GetBytes(text + "\0");
            blobs.Add(b);
            w.Write(hash);
            w.Write(off);
            off += b.Length;
        }
        w.Write(Magic);
        w.Write(off); // полный размер файла
        foreach (var b in blobs) w.Write(b);
        return ms.ToArray();
    }

    public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            if (args.Length >= 3 && args[0] == "get")
            {
                uint h = ParseLabel(args[2]);
                var e = Read(File.ReadAllBytes(args[1])).FirstOrDefault(x => x.Hash == h);
                if (e.Text == null) { Console.Error.WriteLine("[ERROR] label not found"); return 2; }
                Console.Out.Write(e.Text);
                return 0;
            }
            if (args.Length >= 4 && args[0] == "set")
            {
                uint h = ParseLabel(args[2]);
                var data = File.ReadAllBytes(args[1]);
                var list = Read(data);
                int idx = list.FindIndex(x => x.Hash == h);
                if (idx < 0) { Console.Error.WriteLine("[ERROR] label not found"); return 2; }
                list[idx] = (h, args[3]);
                var outBytes = Write(list);
                // Проверка: всё, кроме нашей строки, совпадает с исходником.
                var check = Read(outBytes);
                var orig = Read(data);
                for (int i = 0; i < orig.Count; i++)
                    if (i != idx && (check[i].Hash != orig[i].Hash || check[i].Text != orig[i].Text))
                        throw new InvalidDataException("verify failed at entry " + i);
                File.WriteAllBytes(args[1], outBytes);
                Console.Out.Write("[OK] " + args[2] + " = " + args[3]);
                return 0;
            }
            Console.Error.WriteLine("usage: GtaRadio.Gxt2 get <file> <label> | set <file> <label> <text>");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[ERROR] " + ex.Message);
            return 3;
        }
    }
}
