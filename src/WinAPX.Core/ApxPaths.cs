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

    public static string UbuntuBaseTarPath => Path.Combine(Base, "ubuntu-base.tar");
    public static string InstanceDir(string envName) => Path.Combine(Instances, envName);
}
