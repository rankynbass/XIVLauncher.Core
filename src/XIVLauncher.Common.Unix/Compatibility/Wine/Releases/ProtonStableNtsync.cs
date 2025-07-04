namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class ProtonStableNtsyncRelease(string parentFolder) : IWineRelease
{
    public string Label { get; } = "XIV-Proton 9-27 NTSync";
    public string Description { get; } = "GE-Proton with XIV patches. Based on 9-27";
    public string Name { get; } = "XIV-Proton9-27-ntsync";
    public string ParentFolder { get; } = parentFolder;
    public string DownloadUrl { get; } = "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton9-27/XIV-Proton9-27-ntsync.tar.xz";
    public string[] Checksums { get; } = [ "bea1ceb2cc1493e36a2401f5a163db12606abe97c0350b6fbb5afc2c771d9ebdd8fd6a49d2d216066269ee1a718730df4051e7f847c9e7b0c5d70f34a7bde58b" ];
    public bool lsteamclient { get; } = true;
    public string Checksum { get; } = "";
}