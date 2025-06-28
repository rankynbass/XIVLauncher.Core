namespace XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;

public sealed class DxvkStableRelease : IDxvkRelease
{
    public string Label { get; } = "Stable";
    public string Description { get; } = "Dxvk 2.6.2. No Async patches. For most graphics cards.";
    public string Name { get; } = "dxvk-2.6.2";
    public string DownloadUrl { get; } = "https://github.com/doitsujin/dxvk/releases/download/v2.6.2/dxvk-2.6.2.tar.gz";
    public string Checksum { get; } = "9f70ec8129c1fed10b43f7a49cff588d7aff5b147e4e9d8043de81ed3d77ee4819d69359e797b596c9dfee7b69f193ad36bd91a62184664872a2a0e85dad90c2";
}
