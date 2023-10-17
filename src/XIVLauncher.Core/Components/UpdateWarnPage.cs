using System.Numerics;
using ImGuiNET;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components;

public class UpdateWarnPage : Page
{
    private readonly TextureWrap updateWarnTexture;

    public UpdateWarnPage(LauncherApp app)
        : base(app)
    {
        this.updateWarnTexture = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("xlcore_updatewarn.png"));
    }

    public async void DownloadUpdate()
    {
        var version = (UpdateCheck.Update ?? new Version(AppUtil.GetAssemblyVersion())).ToString();
        var downloadUrl = $"https://github.com/rankynbass/XIVLauncher-SCT/releases/download/v{version}/XIVLauncher-SCT-{version}.tar.gz"; 
        using var client = new HttpClient();
        var tempPath = Path.Combine(Program.SteamInstallPath, "compatibilitytools.d", "XIVLauncher", "update.tar.gz");
        var success = false;
        
        App.StartLoading("Downloading latest version of XIVLauncher-SCT", $"Getting version {version}");
        try
        {
            File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(downloadUrl));
            success = true;
            Log.Information($"Successfully downloaded {downloadUrl}");
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, $"Could not download file {downloadUrl}.");
            success = false;
        }

        if (success)
        {
            var XIVLauncherDir = new DirectoryInfo(Path.Combine(Program.SteamInstallPath, "compatibilitytools.d", "XIVLauncher"));
            try
            {
                Directory.CreateDirectory(Path.Combine(XIVLauncherDir.FullName, "temp"));
                PlatformHelpers.Untar(tempPath, Path.Combine(XIVLauncherDir.FullName, "temp"));

                var UpdateDir = new DirectoryInfo(Path.Combine(XIVLauncherDir.FullName, "temp", "XIVLauncher"));
                var files = UpdateDir.GetFiles();
                var dirs = UpdateDir.GetDirectories();
                foreach(var file in files)
                {
                    file.MoveTo(Path.Combine(XIVLauncherDir.FullName, file.Name), true);
                }
                foreach(var dir in dirs)
                {
                    if (Directory.Exists(Path.Combine(XIVLauncherDir.FullName, dir.Name)))
                        Directory.Delete(Path.Combine(XIVLauncherDir.FullName, dir.Name), true);
                    dir.MoveTo(Path.Combine(XIVLauncherDir.FullName, dir.Name));
                }
                File.Delete(tempPath);
                Directory.Delete(Path.Combine(XIVLauncherDir.FullName, "temp"), true);
            }
            catch (Exception e)
            {
                Log.Error(e, "Something unexpected happened.");
                File.Delete(tempPath);
                Directory.Delete(Path.Combine(XIVLauncherDir.FullName, "temp"), true);
            }
        }
        else
        {
            Log.Error("Could not download the latest update.");
        }
        App.StopLoading();
        App.State = success ? LauncherApp.LauncherState.UpdateComplete : LauncherApp.LauncherState.UpdateFailed; 

    }

    public override async void Draw()
    {
        ImGui.SetCursorPos(new Vector2(0));

        ImGui.Image(this.updateWarnTexture.ImGuiHandle, new Vector2(1280, 800) * ImGuiHelpers.GlobalScale);

        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);

        ImGui.SetCursorPos(new Vector2(316, 481) * ImGuiHelpers.GlobalScale);

        if (ImGui.Button("###openGuideButton", new Vector2(649, 101) * ImGuiHelpers.GlobalScale))
        {
            // Environment.Exit(0);
            DownloadUpdate();
            //App.State = LauncherApp.LauncherState.SteamDeckPrompt;
        }

        ImGui.SetCursorPos(new Vector2(316, 598) * ImGuiHelpers.GlobalScale);

        if (ImGui.Button("###finishFtsButton", new Vector2(649, 101) * ImGuiHelpers.GlobalScale))
        {
            App.FinishFromUpdateWarn();
        }

        ImGui.PopStyleColor(3);

        base.Draw();
    }
}