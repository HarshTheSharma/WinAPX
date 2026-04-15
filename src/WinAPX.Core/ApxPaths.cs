namespace WinAPX.Core;

public static class ApxPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "apx-wsl");

    public static string Base => Path.Combine(Root, "base");
    public static string Instances => Path.Combine(Root, "instances");

    public static void EnsureBaseDirs()
    {
        Directory.CreateDirectory(Base);
        Directory.CreateDirectory(Instances);
    }

public static string BaseTarPath(string distroId) => Path.Combine(Base, $"{distroId}-base.tar");
    public static string InstanceDir(string envName) => Path.Combine(Instances, envName);

    public static string Meta => Path.Combine(Root, "meta");
    public static string MetaDir(string envName) => Path.Combine(Meta, envName);
    public static string DefaultDirFile(string envName) => Path.Combine(MetaDir(envName), "default-dir.txt");
    public static string PackagesFile(string envName) => Path.Combine(MetaDir(envName), "packages.txt");
}
