namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class ProtonLatestRelease(string parentFolder) : IWineRelease
{
    public string Label { get; } = "XIV-Proton 10-8";
    public string Description { get; } = "GE-Proton with XIV patches. Based on 10-8";
    public string Name { get; } = "XIV-Proton10-8";
    public string ParentFolder { get; } = parentFolder;
    public string DownloadUrl { get; } = "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton10-8/XIV-Proton10-8.tar.xz";
    public string[] Checksums { get; } =  [ "e37ba4962a354d0d1e4794afbb78385ecedfc8d95b1db3ea969ccf601ada7af61f987691e3e696cedbbfdde4e7d2ac72732e61a54c0f04110c847a18fe7c9288" ];
    public bool Lsteamclient { get; } = true;
    public bool IsProton { get; } = true;
}