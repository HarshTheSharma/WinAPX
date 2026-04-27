namespace WinAPX.Core;

public sealed record ExportedApp
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string EnvName { get; init; }
    public required string Command { get; init; }
    public required string MasterShortcutPath { get; init; }
    public required string IconPath { get; init; }
    public List<string> ExportedCopies { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; }
}
