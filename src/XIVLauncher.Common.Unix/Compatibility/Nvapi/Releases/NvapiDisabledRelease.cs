using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix.Compatibility.Nvapi.Releases;

public sealed class NvapiDisabledRelease : IToolRelease
{
    public string Folder { get; } = "";
    public string DownloadUrl { get; } = "";
    public bool TopLevelFolder { get; } = false;
    public string Name { get; } = "Disabled";
    public string Description { get; } = "Disable Nvapi. Useful for using Optiscaler with AMD cards.";
}