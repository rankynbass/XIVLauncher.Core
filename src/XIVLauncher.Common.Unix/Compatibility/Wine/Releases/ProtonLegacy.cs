namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class ProtonLegacyRelease(string parentFolder) : IWineRelease
{
    public string Label { get; } = "XIV-Proton 8-30";
    public string Description { get; } = "GE-Proton with XIV patches. Based on 8-30";
    public string Name { get; } = "XIV-Proton8-30";
    public string ParentFolder { get; } = parentFolder;
    public string DownloadUrl { get; } = "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton8-30/XIV-Proton8-30.tar.gz";
    public string[] Checksums { get; } = [ "91aad1e4ca8f5985cbe3c2c48e17c0d8e1a22f950a57373a3ed8a3c088458c3a2d0d644ec4a8cc2a6337ad4a24c656e30dd7c376d06914619fbd35ba706ccd65" ];
    public bool lsteamclient { get; } = true;
    public string Checksum { get; } = "";
}