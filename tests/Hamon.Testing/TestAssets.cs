namespace Hamon.Testing;

/// <summary>Test shared assets (TTF for headless real fonts). </summary>
public static class TestAssets
{
    private static byte[]? CachedFont;

    /// <summary>Default font (NotoSansJP-Regular). </summary>
    public static byte[] DefaultFont
    {
        get
        {
            if (CachedFont is null)
            {
                string path = Path.Combine(AppContext.BaseDirectory, "NotoSansJP-Regular.ttf");
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        $"フォント資産が見つからない: {path}（Hamon.Testing.csproj の TTF コピー設定を確認）");
                }

                CachedFont = File.ReadAllBytes(path);
            }

            return CachedFont;
        }
    }
}
