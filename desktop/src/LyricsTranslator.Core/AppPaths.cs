namespace LyricsTranslator.Core;

public static class AppPaths
{
    public const string AppFolderName = "LyricsTranslator";

    public static string GetDataDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        return Path.Combine(root, AppFolderName);
    }
}
