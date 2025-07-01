namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class ProtonStableRelease(string parentFolder) : IWineRelease
{
    public string Label { get; } = "XIV-Proton 9-27";
    public string Description { get; } = "GE-Proton with XIV patches. Based on 9-27";
    public string Name { get; } = "XIV-Proton9-27";
    public string ParentFolder { get; } = parentFolder;
    public string DownloadUrl { get; } = "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton9-27/XIV-Proton9-27.tar.xz";
    public string Checksum { get; } = [ "31e000158fb1450c95a3de4caea86df985c7d35875aec23d5f173c4fcd63ea3060846ffe0d12744678db090dab648d9af322ef1609d9d298e9a523f4dee17657" ];
    public bool lsteamclient { get; } = true;
    public string Checksum { get; } = "";
}