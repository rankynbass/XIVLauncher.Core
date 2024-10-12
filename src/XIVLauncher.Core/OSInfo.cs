using System.Numerics;
using System.IO;
using System.Collections.Generic;
using XIVLauncher.Common;
using System.Runtime.InteropServices;

namespace XIVLauncher.Core;

public enum LinuxDistroPackage
{
    ubuntu,

    fedora,

    arch,

    none,
}

public enum ContainerType
{
    none,
    flatpak,
    snap,
}

public static class OSInfo
{
    public static LinuxDistroPackage Package { get; private set; }

    public static string Name { get; private set; }

    public static ContainerType Container { get; private set; } = ContainerType.none;

    public static Platform Platform { get; private set; }

    static OSInfo()
    {
        var os = System.Environment.OSVersion;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Package = LinuxDistroPackage.none;
            Name = os.VersionString;
            Platform = Platform.Win32;
            return;
        }

        // There's no wine releases for MacOS or FreeBSD, and I'm not sure this will even compile on either
        // platform, but here's some code just in case. Can modify this as needed if it's useful in the future.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Platform = Platform.Mac;
            Name = os.VersionString;
            Package = LinuxDistroPackage.none;
            return;
        }
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            Platform = Platform.Mac;  // Don't have an option for this atm.
            Name = os.VersionString;
            Package = LinuxDistroPackage.none;
            return;            
        }

        Platform = Platform.Linux;
        try
        {
            if (!File.Exists("/etc/os-release"))
            {
                Package = LinuxDistroPackage.ubuntu;
                Name = "Unknown distribution";
                return;
            }
            var osRelease = File.ReadAllLines("/etc/os-release");
            var osInfo = new Dictionary<string, string>();
            foreach (var line in osRelease)
            {
                var keyValue = line.Split('=', 2);
                if (keyValue.Length == 1)
                    osInfo.Add(keyValue[0], "");
                else
                    osInfo.Add(keyValue[0], keyValue[1]);
            }

            var name = (osInfo.ContainsKey("NAME") ? osInfo["NAME"] : "").Trim('"');
            var pretty = (osInfo.ContainsKey("PRETTY_NAME") ? osInfo["PRETTY_NAME"] : "").Trim('"');
            Name = pretty == "" ? (name == "" ? "Unknown distribution" : name) : pretty;

            if (CheckFlatpak(osInfo))
            {
                Container = ContainerType.flatpak;
                Package = LinuxDistroPackage.ubuntu;
                return;
            }

            if (CheckSnap(osInfo))
            {
                Container = ContainerType.snap;
                Package = LinuxDistroPackage.ubuntu;
                return;
            }

            Package = CheckDistro(osInfo);
            return;
        }
        catch
        {
            // If there's any kind of error opening the file or even finding it, just go with default.
            Package = LinuxDistroPackage.ubuntu;
            Name = "Unknown distribution";
        }
    }

    private static bool CheckFlatpak(Dictionary<string, string> osInfo)
    {
        if (osInfo.ContainsKey("ID"))
            if (osInfo["ID"] == "org.freedesktop.platform")
                return true;
        return false;
    }

    private static bool CheckSnap(Dictionary<string, string> osInfo)
    {
        if (osInfo.ContainsKey("ID") && osInfo.ContainsKey("HOME_URL"))
            if (osInfo["ID"] == "ubuntu-core" && osInfo["HOME_URL"] == "https://snapcraft.io")
                return true;
        return false;
    }

    private static LinuxDistroPackage CheckDistro(Dictionary<string, string> osInfo)
    {
        foreach (var kvp in osInfo)
        {
            if (kvp.Value.ToLower().Contains("fedora"))
                return LinuxDistroPackage.fedora;
            if (kvp.Value.ToLower().Contains("tumbleweed"))
                return LinuxDistroPackage.fedora;
            if (kvp.Value.ToLower().Contains("ubuntu"))
                return LinuxDistroPackage.ubuntu;
            if (kvp.Value.ToLower().Contains("debian"))
                return LinuxDistroPackage.ubuntu;
            if (kvp.Value.ToLower().Contains("arch"))
                return LinuxDistroPackage.arch;
        }
        return LinuxDistroPackage.ubuntu;
    }
}