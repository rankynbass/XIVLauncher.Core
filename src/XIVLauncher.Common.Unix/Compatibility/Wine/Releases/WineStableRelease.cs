using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class WineStableRelease(WineReleaseDistro wineDistroId) : IToolRelease
{
    public string Folder { get; } = $"wine-xiv-staging-fsync-git-10.5.r0.g835c92a2";
    public string DownloadUrl { get; } = $"https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/10.5.r0.g835c92a2/wine-xiv-staging-fsync-git-{wineDistroId}-10.5.r0.g835c92a2.tar.xz";
    public bool TopLevelFolder { get; } = true;
    public string Name { get; } = "Stable 10.5";
    public string Description { get; } = "Based on Wine 10.5 - Recommended for most users.";
}
