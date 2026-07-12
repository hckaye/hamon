namespace Hamon.Testing.Regression;

/// <summary>
/// golden<b>sauce</b>Resolve position. <c>[CallerFilePath]</c>) upwards
/// <c>*.csproj</c>Locate the project root by searching for<c>Goldens/&lt;category&gt;/&lt;name&gt;.png</c>return.
/// It does not depend on copying to the output directory, and both reading and writing (updating) points to the same source PNG.
/// </summary>
public static class TestPaths
{
    public static string GoldenPath(string callerFile, string category, string name) =>
        Path.Combine(ProjectDir(callerFile), "Goldens", category, name + ".png");

    public static string ProjectDir(string callerFile)
    {
        string? dir = Path.GetDirectoryName(Path.GetFullPath(callerFile));
        while (dir is not null)
        {
            if (Directory.EnumerateFiles(dir, "*.csproj").Any())
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException($"プロジェクトルート（*.csproj）が見つからない: {callerFile}");
    }
}
