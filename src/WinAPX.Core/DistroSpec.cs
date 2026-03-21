namespace WinAPX.Core;

public sealed record DistroSpec
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }

    public required string SourceWslDistro { get; init; }

    public static readonly DistroSpec Ubuntu = new()
    {
        Id = "ubuntu",
        DisplayName = "Ubuntu",
        SourceWslDistro = "Ubuntu"
    };

    public static readonly DistroSpec Arch = new()
    {
        Id = "arch",
        DisplayName = "Arch Linux",
        SourceWslDistro = "archlinux"
    };

    public static readonly IReadOnlyList<DistroSpec> All = [Ubuntu, Arch];

    public static DistroSpec? Find(string id)
        => All.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
}
