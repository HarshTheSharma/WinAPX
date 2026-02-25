using System;

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

    // Convert C:\Users\X -> /mnt/c/Users/X
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

    // Convert C:\Users\X -> C:/Users/X and escape spaces for fstab (\040).
    // Avoids backslashes because bash printf treats \U as a unicode escape.
    public static string WinPathToDrvfsFstabPath(string windowsPath)
    {
        var p = CleanName(windowsPath);

        if (p.Length >= 3 &&
            char.IsLetter(p[0]) &&
            p[1] == ':' &&
            (p[2] == '\\' || p[2] == '/'))
        {
            p = p.Replace('\\', '/');
            p = p.Replace(" ", "\\040");
            return p;
        }

        throw new ArgumentException("Expected a Windows drive path like C:\\Users\\...\\Project");
    }

    public static string EscapeForBashSingleQuotes(string value)
        => value.Replace("'", "'\\''");
}