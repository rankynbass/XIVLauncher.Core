using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public sealed class UmuLauncherRelease(string path, string download) : IToolRelease
{
    public string Label { get; } = "Umu Launcher";
    public string Description { get; } = "Open Wine Components Umu Launcher";
    public string Name { get; } = path;
    public string DownloadUrl { get; } = download;
    public string Checksum { get; } = "skip";
}