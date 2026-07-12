using Hamon.Layout;
using System.Buffers.Binary;
using System.IO.Compression;

namespace Hamon.Testing.Rendering;

/// <summary>
/// Minimal PNG input/output without dependencies (8bit RGBA, color type 6, filter None, single IDAT).
/// zlib is<see cref="DeflateStream"/>+Handwritten zlib header/Adler32, chunks with CRC32.
/// The premise is to read back the PNG you wrote (other color types are<see cref="NotSupportedException"/>）。
/// </summary>
public static class PngCodec
{
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static byte[] Encode(RasterImage image)
    {
        using var ms = new MemoryStream();
        ms.Write(Signature);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], image.Width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.Slice(4, 4), image.Height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // color type: RGBA
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace
        WriteChunk(ms, "IHDR", ihdr);

        // 各行 filter byte 0（None）＋RGBA。
        byte[] raw = new byte[image.Height * ((image.Width * 4) + 1)];
        int p = 0;
        for (int y = 0; y < image.Height; y++)
        {
            raw[p++] = 0;
            Array.Copy(image.Rgba, y * image.Width * 4, raw, p, image.Width * 4);
            p += image.Width * 4;
        }

        WriteChunk(ms, "IDAT", ZlibCompress(raw));
        WriteChunk(ms, "IEND", ReadOnlySpan<byte>.Empty);
        return ms.ToArray();
    }

    public static RasterImage Decode(byte[] png)
    {
        for (int i = 0; i < Signature.Length; i++)
        {
            if (png[i] != Signature[i])
            {
                throw new InvalidDataException("PNG シグネチャ不正");
            }
        }

        int width = 0;
        int height = 0;
        using var idat = new MemoryStream();
        int pos = Signature.Length;
        while (pos < png.Length)
        {
            int len = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos, 4));
            string type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            int dataStart = pos + 8;
            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(dataStart, 4));
                    height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(dataStart + 4, 4));
                    if (png[dataStart + 8] != 8 || png[dataStart + 9] != 6)
                    {
                        throw new NotSupportedException("対応は 8bit RGBA(color type 6) のみ");
                    }

                    break;
                case "IDAT":
                    idat.Write(png, dataStart, len);
                    break;
            }

            pos = dataStart + len + 4; // データ＋CRC
            if (type == "IEND")
            {
                break;
            }
        }

        byte[] raw = ZlibDecompress(idat.ToArray());
        return Unfilter(raw, width, height);
    }

    private static RasterImage Unfilter(byte[] raw, int width, int height)
    {
        const int bpp = 4;
        int stride = width * bpp;
        byte[] rgba = new byte[height * stride];
        int src = 0;
        for (int y = 0; y < height; y++)
        {
            byte filter = raw[src++];
            int rowStart = y * stride;
            int prevStart = (y - 1) * stride;
            for (int x = 0; x < stride; x++)
            {
                int value = raw[src++];
                int a = x >= bpp ? rgba[rowStart + x - bpp] : 0;
                int b = y > 0 ? rgba[prevStart + x] : 0;
                int c = (x >= bpp && y > 0) ? rgba[prevStart + x - bpp] : 0;
                int recon = filter switch
                {
                    0 => value,
                    1 => value + a,
                    2 => value + b,
                    3 => value + ((a + b) / 2),
                    4 => value + Paeth(a, b, c),
                    _ => throw new NotSupportedException($"PNG filter {filter}"),
                };
                rgba[rowStart + x] = (byte)(recon & 0xFF);
            }
        }

        return new RasterImage(width, height, rgba);
    }

    private static int Paeth(int a, int b, int c)
    {
        int pp = a + b - c;
        int pa = Math.Abs(pp - a);
        int pb = Math.Abs(pp - b);
        int pc = Math.Abs(pp - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static byte[] ZlibCompress(byte[] raw)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78); // zlib header
        ms.WriteByte(0x9C);
        using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            ds.Write(raw, 0, raw.Length);
        }

        Span<byte> adler = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(adler, Adler32(raw));
        ms.Write(adler);
        return ms.ToArray();
    }

    private static byte[] ZlibDecompress(byte[] zlib)
    {
        using var input = new MemoryStream(zlib, 2, zlib.Length - 2); // 先頭2byte の zlib ヘッダを飛ばす
        using var ds = new DeflateStream(input, CompressionMode.Decompress);
        using var outMs = new MemoryStream();
        ds.CopyTo(outMs);
        return outMs.ToArray();
    }

    private static void WriteChunk(Stream s, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);

        Span<byte> typeBytes = stackalloc byte[4];
        for (int i = 0; i < 4; i++)
        {
            typeBytes[i] = (byte)type[i];
        }

        s.Write(typeBytes);
        s.Write(data);

        uint crc = Crc32(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        s.Write(crcBytes);
    }

    private static uint Adler32(byte[] data)
    {
        const uint mod = 65521;
        uint a = 1;
        uint b = 0;
        foreach (byte d in data)
        {
            a = (a + d) % mod;
            b = (b + a) % mod;
        }

        return (b << 16) | a;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte t in type)
        {
            c = CrcTable[(c ^ t) & 0xFF] ^ (c >> 8);
        }

        foreach (byte d in data)
        {
            c = CrcTable[(c ^ d) & 0xFF] ^ (c >> 8);
        }

        return c ^ 0xFFFFFFFF;
    }
}
