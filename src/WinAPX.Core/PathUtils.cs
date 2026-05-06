namespace WinAPX.Core;

public static class PathUtils
{
    public const string TemplatePrefix = "WinAPX-Template-";

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "list", "create", "enter", "delete", "export", "import",
        "help", "--help", "-h", "info", "version"
    };

    public static string? ValidateEnvName(string envName)
    {
        if (envName.Length == 0)
            return "error: missing env name";

        if (ReservedNames.Contains(envName))
            return $"error: '{envName}' is a reserved command name; pick another";

        if (envName.StartsWith(TemplatePrefix, StringComparison.OrdinalIgnoreCase))
            return $"error: env names starting with '{TemplatePrefix}' are reserved for templates";

        return null;
    }
}
