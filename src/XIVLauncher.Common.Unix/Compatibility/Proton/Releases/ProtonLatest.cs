namespace XIVLauncher.Common.Unix.Compatibility.Proton.Releases;

public sealed class ProtonLatestRelease : IToolRelease
{
    public string Label { get; } = "XIV-Proton 10-4";
    public string Description { get; } = "GE-Proton with XIV patches. Based on 10-4";
    public string Name { get; } = "XIV-Proton10-4";
    public string DownloadUrl { get; } = "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton10-4/XIV-Proton10-4.tar.xz";
    public string Checksum { get; } = "0057eb5648729ad2e7920b769e6c62f5a1dc9712deb581d846ddd1bc1b2de651bb957cda6f6110b56b1bbb7da7b3803ee2749249e2a2f9ab64aa0caa04bb6126";
}