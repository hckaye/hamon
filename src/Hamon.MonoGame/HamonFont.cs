using System.Runtime.InteropServices;

namespace Hamon.MonoGame;

/// <summary>
/// TTF/OTF font resolution for text.
/// (Avoid bloat). <c>FontPath</c>→ Environment variables<c>HAMON_FONT</c> →
/// next to the executable file<c>*.ttf</c>(Bundled assets terms) →<b>OS system font</b>(Single face TTF/OTF/Japanese support preferred).
/// FontStashSharp is<c>.ttc</c>(TrueType Collection) is excluded from the candidates because it cannot be read.
/// </summary>
public static class HamonFont
{
    /// <summary>Lazy enumeration of font bytes in order of attempted resolution (first<see cref="HamonApp"/>The one that can be read is adopted).</summary>
    public static IEnumerable<byte[]> Candidates(HamonAppOptions options)
    {
        if (options.Font is { Length: > 0 } bytes)
        {
            yield return bytes;
        }

        foreach (string path in Paths(options))
        {
            byte[]? data = TryRead(path);
            if (data is not null)
            {
                yield return data;
            }
        }
    }

    private static IEnumerable<string> Paths(HamonAppOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.FontPath))
        {
            yield return options.FontPath!;
        }

        string? env = Environment.GetEnvironmentVariable("HAMON_FONT");
        if (!string.IsNullOrWhiteSpace(env))
        {
            yield return env!;
        }

        foreach (string p in TtfNextToExecutable())
        {
            yield return p;
        }

        foreach (string p in SystemFonts())
        {
            yield return p;
        }
    }

    /// <summary>in the same directory as the executable file<c>*.ttf</c>(Prioritize NotoSansJP, then by name). </summary>
    private static IEnumerable<string> TtfNextToExecutable()
    {
        string dir = AppContext.BaseDirectory;
        string[] ttfs;
        try
        {
            ttfs = Directory.GetFiles(dir, "*.ttf");
        }
        catch
        {
            yield break;
        }

        Array.Sort(ttfs, static (a, b) =>
        {
            // 日本語対応の同梱定番（Noto Sans JP）を最優先、それ以外は名前順で安定化。
            bool an = Path.GetFileName(a).StartsWith("NotoSansJP", StringComparison.OrdinalIgnoreCase);
            bool bn = Path.GetFileName(b).StartsWith("NotoSansJP", StringComparison.OrdinalIgnoreCase);
            return an != bn ? (an ? -1 : 1) : string.CompareOrdinal(a, b);
        });

        foreach (string p in ttfs)
        {
            yield return p;
        }
    }

    /// <summary>
    /// OS system font candidates (single face ttf/otf only/Japanese support first).
    /// On Windows/Linux, Roman languages ​​are output without any settings (explicit fonts are recommended for strict Japanese = most Japanese fonts on each OS are .ttc).
    /// </summary>
    private static IEnumerable<string> SystemFonts()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            yield return "/System/Library/Fonts/Supplemental/Arial Unicode.ttf"; // 日本語＋CJK 対応の単一 ttf
            yield return "/Library/Fonts/Arial Unicode.ttf";
            yield return "/System/Library/Fonts/SFNS.ttf";                       // システム欧文
            yield return "/System/Library/Fonts/Geneva.ttf";
            yield return "/System/Library/Fonts/Supplemental/Arial.ttf";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            yield return Path.Combine(fonts, "segoeui.ttf");
            yield return Path.Combine(fonts, "arial.ttf");
            yield return Path.Combine(fonts, "tahoma.ttf");
            yield return Path.Combine(fonts, "verdana.ttf");
        }
        else
        {
            yield return "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
            yield return "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf";
            yield return "/usr/share/fonts/truetype/freefont/FreeSans.ttf";
            yield return "/usr/share/fonts/google-noto/NotoSans-Regular.ttf";
            yield return "/usr/share/fonts/TTF/DejaVuSans.ttf";
        }
    }

    private static byte[]? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch
        {
            return null;
        }
    }
}
