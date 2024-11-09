using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace XIVLauncher.Common.Unix.Compatibility;

public class DLSSSettings
{
    public bool Enabled { get; private set; }

    public bool NoOverwrite { get; private set; }

    public string FolderName { get; }

    public string DownloadUrl { get; }

    public string NvidiaWineFolder { get; }

    // May use this in the future for the FSR2 mod
    public List<string> NvidiaFiles { get; }

    public Dictionary<string, string> Environment { get; }

    // Constructor for Wine
    public DLSSSettings(bool enabled, bool noOverwrite, string folder, string url, string nvidiaFolder, List<string> nvidiaFiles = null)
    {
        Enabled = enabled;
        NoOverwrite = noOverwrite;
        FolderName = folder;
        DownloadUrl = url;
        NvidiaWineFolder = nvidiaFolder;
        NvidiaFiles = nvidiaFiles ?? new List<string> { "nvngx.dll", "_nvngx.dll" };
        Environment = new Dictionary<string, string>();
        if (Enabled)
            Environment.Add("DXVK_ENABLE_NVAPI", "1");
    }

    // Constructor for Proton
    public DLSSSettings(bool enabled, List<string> nvidiaFiles = null)
    {
        Enabled = enabled;
        NoOverwrite = false;
        FolderName = "";
        DownloadUrl = "";
        NvidiaFiles = nvidiaFiles ?? new List<string>();
        NvidiaWineFolder = "";
        Environment = new Dictionary<string, string>();
        if (!Enabled)
            Environment.Add("PROTON_DISABLE_NVAPI", "1");
    }
}