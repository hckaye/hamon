using System.Runtime.InteropServices;

namespace Hamon.MonoGame;

/// <summary>
/// Resolves a TTF/OTF font for text rendering (to avoid bundling a font by default). Resolution order:
/// <c>FontPath</c> → the <c>HAMON_FONT</c> environment variable → a <c>*.ttf</c> next to the executable
/// (for bundled assets) → an <b>OS system font</b> (preferring a single-face TTF/OTF with Japanese
/// support). <c>.ttc</c> (TrueType Collection) files are excluded from the candidates because
/// FontStashSharp cannot read them.
/// </summary>
public static class HamonFont
{
    /// <summary>Lazily enumerates font bytes in resolution order; <see cref="HamonApp"/> adopts the first one that can be read.</summary>
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

    /// <summary>Enumerates <c>*.ttf</c> files in the same directory as the executable (prioritizing NotoSansJP, then sorted by name).</summary>
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
    /// OS system font candidates (single-face TTF/OTF only, prioritizing Japanese support).
    /// On Windows/Linux, Latin-script text renders fine out of the box, but for correct Japanese rendering
    /// an explicit font is recommended, since most Japanese fonts on those platforms ship as <c>.ttc</c>
    /// files.
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
