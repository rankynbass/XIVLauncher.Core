namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class ProtonLatestNtsyncRelease(string parentFolder) : IWineRelease
{
    public string Label { get; } = "XIV-Proton 10-8 NTSync";
    public string Description { get; } = "GE-Proton with XIV patches. Based on 10-8";
    public string Name { get; } = "XIV-Proton10-8-ntsync";
    public string ParentFolder { get; } = parentFolder;
    public string DownloadUrl { get; } = "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton10-8/XIV-Proton10-8-ntsync.tar.xz";
    public string[] Checksums { get; } = [ "7e7af92e1f08c9c37d44854da1f9301d0128f3e80cfa5a91f0494fa1a723bbe1fe92e77d62bbb2e1ab0e8e3b7060250611737fa653ecc5d4caec9664761c8307" ];
    public bool lsteamclient { get; } = true;
    public string Checksum { get; } = "";
}