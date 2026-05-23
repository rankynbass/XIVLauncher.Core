using System.IO;
using System.Diagnostics;

using XIVLauncher.Common;

namespace XIVLauncher.Core;

public static class StorageHelper
{
    private static Platform platform;

    static StorageHelper()
    {
        if (OperatingSystem.IsWindows())
        {
            platform = Platform.Win32;
        }
        else if (OperatingSystem.IsLinux())
        {
            platform = Platform.Linux;
        }
        else if (OperatingSystem.IsMacOS())
        {
            platform = Platform.Mac;
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported platform");
        }
    }

    public static string? GetStoragePath(string appName)
    {
        if (!string.IsNullOrEmpty(CoreEnvironmentSettings.UserDir))
        {
            return CoreEnvironmentSettings.UserDir;
        }

        if (platform == Platform.Win32)
        {
            return null; // Use default storage on Windows.
        }

        var xdgStoragePath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName));       // new default storage path
        var oldStoragePath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".xlcore"));              // legacy path for xivlauncher
        
        if (xdgStoragePath.Exists)
        {
            return null; 
        }

        if (oldStoragePath.Exists)
        {
            try
            {
                oldStoragePath.MoveTo(xdgStoragePath.FullName); // Move ~/.xlcore to new XDG path.
                return null;
            }
            catch (System.Exception)
            {
                Console.WriteLine($"Failed to move directory from {oldStoragePath.FullName} to {xdgStoragePath.FullName}");
                return oldStoragePath.FullName; // Return the old storage path ~/.xlcore if the move failed.
            }
        }
        
        return null; // If no storage directory exists, return null to use the default path.
    }
}
