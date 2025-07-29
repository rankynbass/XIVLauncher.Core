using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class UmuLauncherRelease(string path, bool download) : IToolRelease
{
    public string Label { get; } = "Umu Launcher";
    public string Description { get; } = "Open Wine Components Umu Launcher";
    public string Name { get; } = path;
    public string DownloadUrl { get; } = download ? "https://github.com/Open-Wine-Components/umu-launcher/releases/download/1.2.9/umu-launcher-1.2.9-zipapp.tar" : "";
    public string Checksum { get; } = "skip";
}