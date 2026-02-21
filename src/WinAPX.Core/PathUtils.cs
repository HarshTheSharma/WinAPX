namespace WinAPX.Core;

public static class PathUtils
{
    public static string CleanName(string? value)
    {
        if (value is null) return "";
        var cleaned = value.Trim();

        if (cleaned.Length >= 2 && cleaned.StartsWith('"') && cleaned.EndsWith('"'))
            cleaned = cleaned[1..^1];

        cleaned = cleaned.Replace("\r", "").Replace("\n", "").Replace("\t", "");
        return cleaned.Trim();
    }

    public static string WinPathToWslPath(string windowsPath)
    {
        var p = CleanName(windowsPath);

        if (p.Length >= 3 &&
            char.IsLetter(p[0]) &&
            p[1] == ':' &&
            (p[2] == '\\' || p[2] == '/'))
        {
            var driveLetter = char.ToLowerInvariant(p[0]);
            var rest = p[2..].Replace('\\', '/');
            return $"/mnt/{driveLetter}{rest}";
        }

        throw new ArgumentException("Expected a Windows drive path like C:\\Users\\...\\Project");
    }

    public static string EscapeForBashSingleQuotes(string value)
        => value.Replace("'", "'\\''");
}
