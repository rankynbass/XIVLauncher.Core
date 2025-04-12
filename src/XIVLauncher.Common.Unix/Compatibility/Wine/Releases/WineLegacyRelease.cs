using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class WineLegacyRelease(WineReleaseDistro wineDistroId) : IToolRelease
{
    public string Folder { get; } = $"wine-xiv-staging-fsync-git-8.5.r4.g4211bac7";
    public string DownloadUrl { get; } = $"https://github.com/goatcorp/wine-xiv-git/releases/download/8.5.r4.g4211bac7/wine-xiv-staging-fsync-git-{wineDistroId}-8.5.r4.g4211bac7.tar.xz";
    public bool TopLevelFolder { get; } = true;
    public string Name { get; } = "Legacy 8.5";
    public string Description { get; } = "Based on Wine 8.5 - use for compatibility with some plugins.";
}
