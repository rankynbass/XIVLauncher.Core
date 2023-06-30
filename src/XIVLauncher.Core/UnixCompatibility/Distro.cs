using System.Numerics;
using System.IO;
using System.Collections.Generic;
using XIVLauncher.Common;
using System.Runtime.InteropServices;

namespace XIVLauncher.Core.UnixCompatibility;

public enum DistroPackage
{
    ubuntu,

    fedora,

    arch,

    none,
}

public static class Distro
{
    public static DistroPackage Package { get; private set; }

    public static string Name { get; private set; }

    public static bool IsFlatpak = false;

    public static Platform Platform { get; private set; }

    public static void Initialize()
    {
        // There's no wine releases for MacOS or FreeBSD, and I'm not sure this will even compile on either
        // platform, but here's some code just in case. Can modify this as needed if it's useful in the future.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Platform = Platform.Mac;
            Name = "Mac OS";
            Package = DistroPackage.none;
            return;
        }
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            Platform = Platform.Mac;  // Don't have an option for this atm.
            Name = "FreeBSD";
            Package = DistroPackage.none;
            return;            
        }

        Platform = Platform.Linux;
        try
        {
            if (!File.Exists("/etc/os-release"))
            {
                Package = DistroPackage.ubuntu;
                Name = "Unknown Linux distribution";
                IsFlatpak = false;
                return;
            }
            var osRelease = File.ReadAllLines("/etc/os-release");
            var distro = DistroPackage.ubuntu;
            var flatpak = false;
            var OSInfo = new Dictionary<string, string>();
            foreach (var line in osRelease)
            {
                var keyValue = line.Split('=', 2);
                if (keyValue.Length == 1)
                    OSInfo.Add(keyValue[0], "");
                else
                    OSInfo.Add(keyValue[0], keyValue[1]);
            }

            var name = (OSInfo.ContainsKey("NAME") ? OSInfo["NAME"] : "").Trim('"');
            var pretty = (OSInfo.ContainsKey("PRETTY_NAME") ? OSInfo["PRETTY_NAME"] : "").Trim('"');
            var idLike = OSInfo.ContainsKey("ID_LIKE") ? OSInfo["ID_LIKE"] : "";
            if (idLike.Contains("arch"))
                distro = DistroPackage.arch;
            else if (idLike.Contains("fedora"))
                distro = DistroPackage.fedora;
            else
                distro = DistroPackage.ubuntu;

            var id = OSInfo.ContainsKey("ID") ? OSInfo["ID"] : "";
            if (id.Contains("tumbleweed") || id.Contains("fedora"))
                distro = DistroPackage.fedora;
            if (id == "org.freedesktop.platform")
                flatpak = true;

            Package = distro;
            Name = pretty == "" ? (name == "" ? "Unknown Linux distribution" : name) : pretty;
            IsFlatpak = flatpak;
        }
        catch
        {
            // If there's any kind of error opening the file or even finding it, just go with default.
            Package = DistroPackage.ubuntu;
            Name = "Unknown Linux distribution";
            IsFlatpak = false;
        }
    }    
}