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
                        $"Font asset not found: {path} (check the TTF copy settings in Hamon.Testing.csproj)");
                }

                CachedFont = File.ReadAllBytes(path);
            }

            return CachedFont;
        }
    }
}
