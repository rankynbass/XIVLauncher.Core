namespace XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;

public sealed class DxvkStableRelease : IToolRelease
{
    public string Label { get; } = "Stable";
    public string Description { get; } = "Dxvk 2.7. No Async patches. For most graphics cards.";
    public string Name { get; } = "dxvk-2.7";
    public string DownloadUrl { get; } = "https://github.com/doitsujin/dxvk/releases/download/v2.7/dxvk-2.7.tar.gz";
    public string Checksum { get; } = "adfbe6ff61467dea212acf8b5e82007a2376d69bf21572d0020e49aaa4ab8315bcce67c4f01dfd133908bcf6ef20b17d8b0e88d784e2f42051ca972f902fe9ff";
}
