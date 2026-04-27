namespace WinAPX.Core;

public static class ApxPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "apx-wsl");

    public static string Base => Path.Combine(Root, "base");
    public static string BaseTarPath(string distroId) => Path.Combine(Base, $"{distroId}-base.tar");

    public static string Instances => Path.Combine(Root, "instances");
    public static string InstanceDir(string envName) => Path.Combine(Instances, envName);

    public static string Meta => Path.Combine(Root, "meta");
    public static string MetaDir(string envName) => Path.Combine(Meta, envName);
    public static string DefaultDirFile(string envName) => Path.Combine(MetaDir(envName), "default-dir.txt");
    public static string PackagesFile(string envName) => Path.Combine(MetaDir(envName), "packages.txt");

    public static string Apps => Path.Combine(Root, "apps");
    public static string AppsForEnv(string envName) => Path.Combine(Apps, envName);
    public static string AppDir(string envName, string appId) => Path.Combine(AppsForEnv(envName), appId);
    public static string AppMetaFile(string envName, string appId) => Path.Combine(AppDir(envName, appId), "app.json");
    public static string AppMasterShortcut(string envName, string appId) => Path.Combine(AppDir(envName, appId), "shortcut.lnk");
    public static string AppIconFile(string envName, string appId) => Path.Combine(AppDir(envName, appId), "icon.ico");

    public static void EnsureBaseDirs()
    {
        Directory.CreateDirectory(Base);
        Directory.CreateDirectory(Instances);
        Directory.CreateDirectory(Apps);
    }

    public static string CleanName(string? value)
    {
        if (value is null)
        {
            return "";
        }
        var cleaned = value.Trim();

        if (cleaned.Length >= 2 && cleaned.StartsWith('"') && cleaned.EndsWith('"'))
        {
            cleaned = cleaned[1..^1];
        }

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
}
