using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;

public sealed class DxvkLegacyRelease : IToolRelease
{
    public string Folder { get; } = "dxvk-async-1.10.3";
    public string DownloadUrl { get; } = "https://github.com/Sporif/dxvk-async/releases/download/1.10.3/dxvk-async-1.10.3.tar.gz";
    public bool TopLevelFolder { get; } = true;
    public string Name { get; } = "Legacy";
    public string Description { get; } = "Dxvk 1.10.3 with Async patches. For older graphics cards.";
}
