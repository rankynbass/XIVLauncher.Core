namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class ProtonLatestRelease(string parentFolder) : IWineRelease
{
    public string Label { get; } = "XIV-Proton 10-final";
    public string Description { get; } = "Proton-CachyOS with XIV patches. Based on proton-cachyos-10-sunset";
    public string Name { get; } = "XIV-Proton-10-final";
    public string ParentFolder { get; } = parentFolder;
    public string DownloadUrl { get; } = "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton-10-final/XIV-Proton-10-final.tar.xz";
    public string[] Checksums { get; } =  [ "62c251622dfaed8ca507c943c81af4b410e8ddab976c672940f12415f797700cde5b7b7608207e9849d2564796b85dd2f05b99bf160faf026cbf316dbf4604bf" ];
    public bool Lsteamclient { get; } = true;
    public bool IsProton { get; } = true;
}