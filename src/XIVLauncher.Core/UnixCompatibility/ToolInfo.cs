namespace XIVLauncher.Core.UnixCompatibility;

public class ToolInfo
{
    public string Label { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public bool IsDownloaded { get; set; }

    public string DownloadUrl { get; private set; }

    public ToolInfo(string name, string desc, string label = "Custom", string url = "")
    {
        Label = label;
        Name = name;
        Description = desc;
        IsDownloaded = false;
        DownloadUrl = url;
    }
}