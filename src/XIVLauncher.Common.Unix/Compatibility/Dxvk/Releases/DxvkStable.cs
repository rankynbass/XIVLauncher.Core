using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;

public sealed class DxvkStableRelease : IToolRelease
{
    public string Folder { get; } = "dxvk-gplasync-v2.6-1";
    public string DownloadUrl { get; } = "https://gitlab.com/Ph42oN/dxvk-gplasync/-/raw/447db06ecff8a64f900b12741dbd8d1c8d8eae22/releases/dxvk-gplasync-v2.6-1.tar.gz";
    public bool TopLevelFolder { get; } = true;
    public string Name { get; } = "Stable";
    public string Description { get; } = "Dxvk 2.6 with GPLAsync patches. Works with most graphics cards.";
}
