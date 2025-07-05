namespace XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;

public sealed class DxvkStable22Release : IToolRelease
{
    public string Label { get; } = "Stable 2.2";
    public string Description { get; } = "Dxvk 2.2. May work better with ReShade + REST";
    public string Name { get; } = "dxvk-2.2";
    public string DownloadUrl { get; } = "https://github.com/doitsujin/dxvk/releases/download/v2.2/dxvk-2.2.tar.gz";
    public string Checksum { get; } = "e4bd21c576bb6a109d4a355007af359d365a61d21966a578703bedefe9afa23c09e5200eaf2d020435a9dcc0e80f3b1ecc2ca681091dceac0b29f10a2540a8a9";
}
