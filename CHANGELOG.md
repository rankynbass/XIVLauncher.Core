# Changelog
### Mon August 18 2025 Rankyn Bass <rankyn@proton.me>
1.2.1.3
- Added a fix for a crashing issue with wine-staging 10.12 and later
- Added toggle for hiding wine exports in Troubleshooting tab. Enabled by default.
- Added hack to disable lsteamclient in Troubleshooting tab. Fixes Internal Dalamud error with some wine versions.
- Added ability to disable downloading compatibility tool lists. This does not delete existing lists.
- Added environment variable `XL_IGNORE_LISTS` to ignore tool lists and use the built-in compatibility tools.

### Sat August 16 2025 Rankyn Bass <rankyn@proton.me>
1.2.1.1
Changes and Added Features
- Refactored against 1.2.1
- launcher.ini values that are different from official XL.Core, or unique to XIVLauncher-RB, have been changed. They will now be prefixed with `RB_`. So `WineVersion` will become `RB_WineVersion`, `DxvkHudType` will become `RB_HudType`, etc.
- Proton Dxvk/Nvapi options have their own settings.
- Wine 7/10 toggle buttons removed, back to the Win7 checkbox. Updated to track windows version, so after the first run, it won't update unless you change it or delete the prefix. Speeds up launching slightly after first launch.
- WineD3D can now be enabled with the experimental vulkan renderer. Tracked, so the registry won't be updated on every launch unless you change it or delete the prefix.
- Extra WINEDLLOVERRIDES moved to Game tab below Additional Arguments.
- NTSync checkbox. It only works for very new Proton and ValveBE wine. Other wine/proton releases need to have ntsync baked in; the checkbox does nothing for those versions.
- Custom Wine can now be pointed at a proton release as well. Proton or wine will be detected correctly.
- "Auto-start" tab renamed to "Apps"

Removed Features
- UI Scaling removed. It was mostly broken as of SDL3.
- Built-in compatibility tool installer removed (Use XLM instead)
- Command line options removed
- XL_NVNGX_TO_PREFIX and XL_FORCE_DLSS removed

Planned features
- Tracking of files from Dxvk/Nvapi so they can be removed. (Not all dxvk versions have the same files)
- Actual working wayland and x11 UI scaling

### Sat Jun 21 2025 Rankyn Bass <rankyn@proton.me>
1.1.2.4
- Updated wine and dxvk
- Updated the default version to the new official Wine-XIV 10.8 release

### Sat Jun 14 2025 Rankyn Bass <rankyn@proton.me>
1.1.2.3
- Updated wine, dxvk, proton versions
- Changed the wayland toggle to use PROTON_ENABLE_WAYLAND when using proton runner
- Proton selection will now hide some incompatible proton versions. Basically, proton 8 is safe, and proton 9 and 10 are broken unless patched. Custom (non-GE, non-XIV) proton will still be shown, in case the user wants to use their own patched version of proton.

### Fri May 16 2025 Rankyn Bass <rankyn@proton.me>
1.1.2.2
- Updated wine, dxvk, proton versions
- Cherry-picked some fixes from upstream
- Still no auto-updating. Will probably add with 1.1.3 changes.

### Tue Apr 01 2025 Rankyn Bass <rankyn@proton.me>
1.1.2.1
- Enabled actions on the repo, so I can build on github and release directly.
- Updated to match upstream release 1.1.2
- Still no auto-updating.
- No tumbleweed release as of release. COPR is broken atm.

### Sat Mar 29 2025 Rankyn Bass <rankyn@proton.me>
1.1.1.7
No auto-updating of compatibility tools in this version, sorry.
- Updated wine, valvebe, and Proton versions to work with FFXIV 7.2
- Partially fixed segfault on closing
- Got rid of update checks
- Changed the internal dalamud error message to offer possible solutions
- Updated dxvk and dxvk-nvapi versions
- `XL_NVNGX_TO_PREFIX` now defaults to true. There's not really a reason *not* to do this.

### Sat Feb 08 2025 Rankyn Bass <rankyn@proton.me>
1.1.1.6
- Add a hack in the troubleshooting tab for an internal Dalamud error related to experimental proton-wine that results in this error message at the terminal:
```
Cannot get symbol u_charsToUChars from libicuuc
Error: 127
```
- Updated wine, valvebe wine, proton, dxvk, and dxvk-nvapi
- Added an environment variable `XL_NVNGX_TO_PREFIX`, which when set to "1" will copy nvngx files to the prefix as well as the game folder. This should allow OptiScaler to include DLSS when using nvidia GPUs.

### Sat Dec 07 2024 Rankyn Bass <rankyn@proton.me>
1.1.1.5
- The `WINEPREFIX` environment variable will now be ignored for proton. Use `PROTONPREFIX` for a proton prefix. *DO NOT* use the same folder for both prefix types; it will almost certainly break, and you'll get an "Internal Dalamud Error" of some sort when trying to launch.
- Updated valve wine to latest bleeding edge.
- Added feature to disable all plugins but still load Dalamud on launch. See [PR#204](https://github.com/goatcorp/XIVLauncher.Core/pull/204) from the main repo.

### Thu Nov 28 2024 Rankyn Bass <rankyn@proton.me>
1.1.1.4
- Reverted OTP page to upstream version, and added scaling tweaks. This should fix the OTP not working properly on Steam Deck.
- Updated Wine versions. xiv-9.22.1 and valvebe-9-13 should no longer show the prefix updating or try to install mono. This should greatly reduce crashes and delays on prefix creation and update. (Updates happen whenever changing wine versions.) The launcher should switch to the ffxiv process more quickly, although Dalamud may still take a while to load.

### Sat Nov 23 2024 Rankyn Bass <rankyn@proton.me>
1.1.1.3
- Fixed an issue where the custom wine path was being saved as the managed wine path. This would cause a crash when switching back to managed wine.
- Updated Wine, Proton, Dxvk, and Dxvk-gplasync

### Tue Nov 12 2024 Rankyn Bass <rankyn@proton.me>
1.1.1.2
- Fixed a typo in the library paths for flatpaks. DLSS should now work.
- Updated DXVK to 2.5

### Sun Nov 10 2024 Rankyn Bass <rankyn@proton.me>
1.1.1.1
- Moved XL.Common.Unix to XL.Core repo to keep up with latest upstream
- Fixed a bug where aria2 would not pause and shutdown when the launcher was closed during download.
- Fixed XLM download url, so the compatibility tool installer should work again.
- Update Wine versions.
- No longer use the linux find command to find nvngx. Now it's done with dotnet Directory.GetFiles(). I didn't use it previously because I didn't know how to make it avoid infinite loops caused by symlinks.
- Added `XL_SCALE` environment variable to force a specific launcher scale. This will override the desktop detection. Only applies to XWayland/X11 for now.
- Wine and Dxvk tabs now will indicate a failed download attempt when using the Download buttons.

### Sun Oct 27 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.18
- Fixed a bug with downloading dxvk-nvapi. The dxvk-nvapi tarballs do not have a top-level folder, and I forgot to account for that when I did the last rework in 1.1.0.15.

### Sat Oct 26 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.17
- Don't show Steam Deck prompt if using with XLM
- Don't hide launcher until after ffxiv is launched. This prevents the several-seconds wait from launcher disappearance to ffxiv window appearance.
- Improve steam initialization. It will now use SteamAppId if you attach it to a non-ffxiv steam game. This should help steam input work properly.
- Fix the label on xiv-Proton_9.0-3c to be "XIV-patched" instead of "UMU-Proton".

### Thu Oct 24 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.16
- Fix default paths for Steam installs. Previous versions of the installer wanted the `~/.local/share` folder and would add Steam internally. Current version uses `~/.local/share/Steam`.
- Fixed XLM install scripts to use `.local/share/Steam` instead of `.steam/root`, in case the script is run before steam is run for the first time.
- Updated wine, valvebe wine, and proton
- When used with XLM, it will automatically use the selected game's SteamAppId if no XL_APPID is set.

### Sat Oct 12 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.15
- Added snap support to the compatibility tool.
- Updated wine, proton.
- More rework on the XL.Common.Unix backend.
- Launcher will now use `find` instead of `/bin/find`, and hid stderr messages. DLSS files will not be found if find is not installed (`XL_NVNGXPATH` can still be used)
- zstd added to the requirements for COPR (arch will have it installed since it's used for pacman).

### Sat Sep 28 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.14
- Replaced the compatibility tool with one based around [XLM](https://github.com/Blooym/xlm)
- Updated wine, proton, and dxvk versions
- Fixed the UI so that using the download buttons for wine, dxvk, etc, will properly show them as downloaded. Also fixed up the Clear wine/dxvk in the troubleshooting tab so it also shows.

### Sat Sep 14 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.13
- Don't delete nvngx.dll and _nvngx.dll from game directory if using proton. This allows FSR2 mod to work.

### Sat Sep 14 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.12
- Updated nvngx.dll detection to disable nvapi if not found. This should prevent the launcher from deleting the nvngx.dll provided with the FSR2 mod.
- The `--version and -V options will now correctly report this as RB-Patched instead of Official.

### Mon Sep 02 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.11
- Updated search paths for nvngx.dll. Now checks /lib64 as well.

### Sun Sep 01 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.10
- Updated wine and proton builds
- Added DLSS support to wine via dxvk-nvapi. The launcher will search /lib and ~/.xlcore/compatibilitytool/nvidia for nvngx.dll. If it can't find it, you'll need to set the environment variable explained below.
- Merged some fixes from the official repos
- Environment vars added:
  - `XL_APPID=12345` can be set to the AppId of any steam game you own. This will allow you to hijack that entry for Steam Input. Useful if your  region doesn't have FFXIV or FFXIV Free trial.
  - `XL_FORCE_DLSS=1` prevents the search for nvidia wine dlls and assumes they are already in the FFXIV/game folder. Yes, this means you can enable nvapi on an AMD card. No, it does not magically give you DLSS.
  - `XL_NVNGXPATH=/nix/some-weird-path/nvidia/wine` allows you to set a path to your nvidia wine dll folder, so that the files can actually be found. Only use this if Nvapi version doesn't appear in your DXVK tab.

### Sat Aug 03 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.9
- Revert Proton9-11 to Proton9-9. There's a nasty bug in prefix updates that causes all sorts of issues.
- Added warning about Proton9-11.
- Added button to re-install/update steam runtimes.
- Fixed default SteamPath to be correct (was ~/.local/share, is now ~/.local/share/Steam)

### Fri Aug 02 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.8
- Fixed broken download link for XIV-Proton9-11

### Fri Aug 02 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.7
- Re-added the wayland options to the wine tab
- Updated wine versions
- Added automatic scaling in x11/xwayland mode. This should cause sharp upscaling of the xlcore client matching your desktop upscaling. Thanks to Maia Everett (https://github.com/Maia-Everett)

### Wed Jul 10 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.6
- Permanently fixed a crash that would occur if the user did not have a particular proton version installed with a fresh .ini file.
- Updated DXVK to 2.4
- Updated valvebe versions

### Sun Jul 07 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.5
- Added WINEDLLOVERRIDES in Wine tab. This is the correct way to set extra overrides.
- Updated wine and proton versions

### Wed Jul 03 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.4
- Restored XL_PATH environment variable handling. It accidentally got deleted when I did the new proton layer merge.

### Sun Jun 30 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.3
- Fix a bug where the launcher would crash with an ini file missing ProtonVersion entery and with no GE-Proton8-9 installed. This was leftover from a previous testing build. I re-added some GE-Proton builds, including 8-9, to fix this.

### Sun Jun 30 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.2
- Updated to a completely new Proton layer, inspired by umu-launcher. Code is much cleaner and robust, and doesn't break every time I look at it funny.
- Proton versions and runtimes can now be downloaded just like wine versions.
- Removed the scaling patch for now. I'll pull in the PR on the main repo in a bit so that it'll be easier to maintain in the future, but that's several hours of work I don't want to do right now.
- Removed automatic wayland settings for the same reason. I'll add it back later. Doing it manually still works, of course.

### Fri Jun 28 2024 Rankyn Bass <rankyn@proton.me>
1.1.0.1
- Pulled in a few more patches to bring in line with 1.1.0

### Thu Jun 27 2024 Rankyn Bass <rankyn@proton.me>
1.0.9.1
- Merged in 1.0.9 changes from official project
- Added in the game repair fix
- Updated to dotnet8. Lots of warnings, but it builds and runs.
- Got rid of the openssl fix. It's no longer needed.

### Tue Mar 19 2024 Rankyn Bass <rankyn@proton.me>
1.0.8.1
- Added in upstream changes so we can log in
- Removed async update patch for now... it's broken with upstream changes.
- DirectX 9 is now no longer available
- Updated for patch 6.58
- Moved into its own directory: /opt/xivlauncher-rb
- Got rid of the overly-complicated launcher script. Now it just has the SSL fix.

### Sat Jan 13 2024 Rankyn Bass <rankyn@proton.me>
1.0.6.10
- Updated Wine versions. Now includes a version of Wine 9.0-rc5, which has the new wayland driver.
- Added options in the wine tab to enable wayland. Don't enable wayland on a non-wayland wine build; FFXIV won't launch.
- Added a patch to work around bad timezone settings. If TZ is set to a non-windows value, it will result in incorrect display of server and local time in game. This patch simply unsets TZ, so the game will check the system clock instead.

### Wed Dec 27 2023 Rankyn Bass <rankyn@proton.me>
1.0.6.9:
- Added Locale hack for systems with non-utf8 locales
- Fixed an issue with Auto-launch apps not running if there was a space in the path
- Added arguments field for Auto-launch apps
- Code changes:
  - Added ListSettingsEntry
  - Fixed DictionarySettingsEntry so that it won't crash with an invalid index lookup
  - Locale checking now produces a list of utf8 values instead of being a toggle for C.utf8

### Wed Nov 29 2023 Rankyn Bass <rankyn@proton.me>
- Fixed up some problems with detecting XDG_DATA_HOME inside a flatpak.
- Revised Steam Tool tab to match [PR #99](https://github.com/goatcorp/XIVLauncher.Core/pull/99).

### Sun Nov 26 2023 Rankyn Bass <rankyn@proton.me>
Reworked the internal STEAM_COMPAT_MOUNTS handling to include a few extra paths. Not sure why this works, but now steam runtimes work again with proton.
- Re-enabled steam runtime support
- Updated tkg-wine version
- Added a separate Clear Dalamud button that does just that. It leaves plugins installed. Clear Plugins will now only delete installed Plugins.

### Sat Nov 25 2023 Rankyn Bass <rankyn@proton.me>
Proton+steam runtime has been broken since 1.0.5.0. I'm not sure why, as I didn't change anything in the code between those patches. Disabled for now.

### Fri Nov 24 2023 Rankyn Bass <rankyn@proton.me>
Big feature added: you can now install this as a steam compatibility tool. Just go into the settings, and you will see a new tab, "Steam Tool." There, you'll find an option to install to Steam as a compatibility tool. This does not work with flatpak Steam; for that, you'll need to grab the .flatpak file from my [XIVLauncher.Core fork](https://github.com/rankynbass/XIVLauncher.Core/releases).

Once installed, you can open steam, right-click on FFXIV, Properties -> Compatibility, and select XIVLauncher.Core as Compatibility Tool.

Other updates:
- Updated with patches from current main branch (xlcore) and master (submodule)
- Added async update checks
- Added version checking against my own git repo instead of official repo

### Fri Nov 10 2023 Rankyn Bass <rankyn@proton.me>
1.0.6.3:
- Re-add Dxvk GPLAsync 2.2-4 to fix crashing with ReShade Effects Toggler addon
- Sort custom wine and dxvk folders by name

1.0.6.2:
- Updated Wine-GE version to 8-21.
- Updated Wine-TKG version to 8.18 staging.

### Thu Nov 05 2023 Rankyn Bass <rankyn@proton.me>
1.0.6.1:
- Fixed a bug where an invalid entry for ProtonVersion or SteamRuntime in the launcher.ini file would cause a crash when going into the Settings -> Wine tab and selecting Proton.

### Wed Nov 04 2023 Rankyn Bass <rankyn@proton.me>
1.0.6.0:
- Purely cosmetic bump to match the official release. The 1.0.6 patches were applied in 1.0.5.1.

### Tue Nov 03 2023 Rankyn Bass <rankyn@proton.me>
1.0.5.0, 1.0.5.1:
- Update to support patch 6.50. The .1 release fixed the repair game function.

### Thu Sep 21 2023 Rankyn Bass <rankyn@proton.me>
1.0.4.7-1:
- Changed the versioning scheme. Updated scripts and added epoch to .spec file.

### Thu Sep 21 2023 Rankyn Bass <rankyn@proton.me>
1.0.4-7:
- Fixed a minor issue with console spamming gamemodeauto: if gamemode was enabled.

### Thu Sep 21 2023 Rankyn Bass <rankyn@proton.me>
1.0.4-6: Rebased on compatibility-rework-2 branch, which will hopefully be merged in 1.0.5.
- Pruned Wine and Dxvk lists
- Added separate download buttons for Wine and Dxvk
- Reworked scaling. If you were scaling based on Font size, reset font size to 22, and change the scaling factor instead. In the UI, it's shown as a int percentage, but it's stored as a float multiplier (e.g 1.5)
- You can now extract wine folders into `~/.xlcore/compatibilitytool/wine` and they will show up in the list of Wine versions.
- You can do the same with Dxvk folders into `~/.xlcore/compatibilitytool/dxvk`

### Sat Aug 12 2023 Rankyn Bass <rankyn@proton.me>
Added a few new wine versions
- Wine-GE based 8-13
- Wine-staging-tkg 8.13
- proton-wine 8.0.3c (tkg)
- proton-wine-experimental bleeding edge 8.0.52623 (tkg)

Added the ability to use a custom Dxvk folder

### Sat Jul 22 2023 Rankyn Bass <rankyn@proton.me>
Added a Reshade toggle button at the bottom of the Wine Tab. Also added Unofficial-wine-xiv-proton 8-12, with FSR, and a new valve proton-wine with xiv patches and fsr.

### Sat Jul 15 2023 Rankyn Bass <rankyn@proton.me>
Fixed the "Disabled" option for DXVK. Also prevented DXVK Async from showing when Disabled is selected.

### Fri Jul 14 2023 Rankyn Bass <rankyn@proton.me>
Re-added the Auto-launch feature. Up to 3 Windows exes can be launched with the game. Tested as working with winediscordipcbridge.exe.

Confirmed that launching when using Proton, I can launch with ReShade, Dalamud, and MangoHud enabled and not crash, even after several hours of playtime.

### Thu Jul 13 2023 Rankyn Bass <rankyn@proton.me>
Updated to 1.0.4 with Proton compatibility patched in.
- Includes patched wine versions 8.10-12, 8.8, and 7.22
- Includes patched wine-ge versions
- Includes dxvk 2.1 and 2.2 with gplasync patches
- Includes XL_PATH environment variable

### Tue Nov 29 2022 Rankyn Bass <rankyn@proton.me>
Updated the xivlauncher script. It has some management features built in
- It will create .desktop files for your custom scripts in `~/.local/share/applications` (or the $XDG_DATA_HOME directory if that is set).
- You can list scripts with -l, delete with -d <scriptname>, and refresh (to default) with -r <scriptname>.
- There is some basic sanity parsing, but not too much. You can break this if you try. I'm not responsible if you do.

The settings tab has a minor makeover: dropdown menus now show the extended description (previously this was unused in xlcore).

Added support for MangoHud. It needs to be installed to work.

### Sun Nov 27 2022 Rankyn Bass <rankyn@proton.me>
Moved a bunch of code back since the XIVLauncher.Common.Unix pull request got rejected.

Fixed a few minor scripting errors
- getsources.sh will no longer fail if the CoreTag and LauncherTag are the same
- xivlauncher.sh had a minor typo that probably would never have been encountered, but it's fixed anyway.
- xivlauncher.sh now installs a .desktop file to `~/.local/share/applications` when you use a custom arguement.

Bump release to 4.

### Sat Nov 26 2022 Rankyn Bass <rankyn@proton.me>
Added the `XL_FORCE_WINED3D` environment variable. Forces use of WineD3D instead of DXVK

Moved a bunch of code around for ease of management. The XIVLauncher.Common.Unix folder was moved from the FFXIVQuickLauncher repo to the XIVLauncher.Core repo.

The titlebar now has RB-Unofficial at the end to remind you this is not a default build.

Bump release to 3.

### Thu Nov 24 2022 Rankyn Bass <rankyn@proton.me>
XIVLauncher-RB now has a DXVK version switcher!
- It's in the Wine tab.
- Default version is 1.10.1. But you can chose .2, .3, or 2.0.

My fork of the XIVLauncher.Core repo now points to my fork of the FFXIVQuickLauncher repo, at least for this set of branches.

Bump release to 2.

### Sat Nov 12 2022 Rankyn Bass <rankyn@proton.me>
New build! XIVLauncher-RB 1.0.2-1 with custom patches:
- XL_PATH patch - You can now use the environment variable XL_PATH to set a path for the xlcore directory. For example, `XL_PATH=$HOME/.local/share/xlcore` to conform with XDG directory structure.
- DXVK v2 - upgraded to the latest version of DXVK
- The titlebar now says "XIVLauncher-RB" to indicate that you're not running the default launcher.

You must uninstall XIVLauncher, and install XIVLauncher-RB. The two versions cannot coexist.

### Wed Nov 09 2022 Rankyn Bass <rankyn@proton.me>
Release bumped to 6

Changed back to tar.gz files for faster testing. Size saving isn't worth it.
- Changed FFXIVQuickLauncher commit to 261464a. This is only one commit off the offical xlcore 1.0.2 commit, but has the XIVLauncher.Core stuff moved out of the repo. This prevents a bunch of duplicates in the tar.gz / SRPM.
- XIVLauncher.Core is now on commit ad6b701 (it actually has been for a few releases). This commit skips version checking for non-flatpak releases. The version check function can sometimes take up to 5 seconds to execute (at which point it times out and gives up), and the window contents won't load until it's finished.

1.0.8-2
- Get rid of overly complicated launcher script.
- Make it no longer conflict with -RB package.
- Move to /opt/xivlauncher instead of /opt/XIVLauncher
- Renamed primary launcher script to /usr/bin/xivlauncher-core to match other distros.

### Sun Jun 18 2023 Rankyn Bass <rankyn@proton.me>
Updated to 1.0.4-3. Fixed a bug that would prevent launching if wine had not already been downloaded.

### Sat Jun 17 2023 Rankyn Bass <rankyn@proton.me>
Updated to 1.0.4-2. There's a few minor fixes. Dalamud plugins might work better, now.

### Sat Jun 17 2023 Rankyn Bass <rankyn@proton.me>
Updated to version 1.0.4

### Sat Jan 14 2023 Rankyn Bass <rankyn@proton.me>
Updated to version 1.0.3

### Mon Nov 28 2022 Rankyn Bass <rankyn@proton.me>
Release bumped to 6

Updated the xivlauncher script. It has some management features built in
- It will create .desktop files for your custom scripts in `~/.local/share/applications` (or the $XDG_DATA_HOME directory if that is set).
- You can list scripts with -l, delete with -d <script>, and refresh (to default) with -r <script>.
- There is some basic sanity parsing, but not too much. You can break this if you try. I'm not responsible if you do.

Updated the submodule to the xlcore removed commit. This cuts the source size in half, which means I went back to using tar.gz instead of tar.xz. This doesn't change functionality at all.

### Wed Nov 02 2022 Rankyn Bass <rankyn@proton.me>
Release bumped to 5

Redid the xivlauncher script
- Running `/usr/bin/xivlauncher` with no arguements just launches XIVLauncher.
- Running `/usr/bin/xivlauncher custom` will check for `$HOME/.local/bin/xivlauncher-custom.sh`, and launch it if it's found and has valid bash syntax.
    - If the script is found, but is broken, it'll back it up and create a new one.
    - If it doesn't find the custom script, it will created it.
    - If the path `$HOME/.local/bin` doesn't exist, it will be created first. This fixes an error that was reported. This path is *not* part of the XDG basedir specs, but it *is* part of the systemd file heirarchy, and Fedora, openSUSE, Enterprise Linux, and most distros with systemd use it. 

### Sun Oct 30 2022 Rankyn Bass <rankyn@proton.me>
Release bumped to 4

Fixed an error in the xivlauncher.sh script
- The xivlauncher-custom.sh script being created was malformed, resulting in a crash.
- The xivlauncher script now checks it for syntax errors, and backs it up and creates a new one if there are problems. This should fix it for people who got a poorly formed script file.

### Sat Oct 29 2022 Rankyn Bass <rankyn@proton.me>
Release bumped to 3

Minor update to launcher scripts
- The `/usr/bin/xivlauncher` script now checks `~/.local/bin/xivlauncher-custom.sh` for an openssl config line
- If it doesn't find one, it adds it to the top of the file (just under `#!/bin/bash`)
- If it's creating the file for the first time, the line is included.
- The `/usr/bin/xivlauncher` script no longer has the openssl config line. Users can just directly execute the xivlauncher-custom script with no problems now. Calling the main xivlauncher script will still work as well.

### Thu Oct 27 2022 Rankyn Bass <rankyn@proton.me>
Minor packaging update to 1.0.2-2

The last number has been dropped from the versioning. As a result, I had to add an Epoch entry to the .spec file.

The `/usr/bin/xivlauncher` script has been modified. It now calls a custom script from `~/.local/bin/xivlauncher-custom.sh`, and creates it first if it doesn't already exist. This will allow the user to maintain their own edits to the launch script that won't be overwritten with each update. the README.md and install script output have been updated accordingly.

Steam users should now be able to just safely reference the `xivlauncher` script or the .desktop file.

The `cleanupprofile.sh` script now just deletes folders instead of backing them up. There wasn't really a point to that.

### Sun Oct 23 2022 Rankyn Bass <rankyn@proton.me>
New version! 1.0.2.0-1

Now uses a new repo. _version file, scripts, and spec file adjusted to work with it.
- pull tarballs from XIVLauncher.Core repo and FFXIVQuickLauncher repo
- using build hash from XIVLauncher.Core repo for BuildHash

You can now generate a tspack for debugging. It's in the Settings > About tab.

You can now press enter while in the user or password fields and log in.

Update without starting should now actually not start the game.

### Sun Oct 2 2022 Rankyn Bass <rankyn@proton.me>
Bump version-release to 1.0.1.0-6

Modified getsources.sh and version file to allow alternate forks of FFXIVQuickLauncher

Added local.sh to make it easier to test new builds without doing commits or git pushes.

Modified xivlauncher.sh to indicate how to add env variables.

Modified spec file so titlebar will show "1.0.1.0 (rpm-hashnum)"

Modified .desktop file to include (native) in the title, so it's different from flatpak install.

### Sat Sep 10 2022 Rankyn Bass <rankyn@proton.me>
Bump version-release to 1.0.1.0-5

Added cleanupprofile.sh
- Moves compatibilitytool, dalamud, dalamudAssets, devPlugins folders to _old_compat
- Must be run by user. Can't run as part of install, since that is run as root, not as user.

Modified spec file
- Added %post and %postun macros.
- Added reference to cleanupprofile.sh

Modified getsources.sh to include cleanupprofile.sh

Modified README.md to include cleanupprofile.sh

### Sun Sep 04 2022 Rankyn Bass <rankyn@proton.me>
Bump version-release to 1.0.1.0-4, because 3a is not > 3

Added _version file. This contains UpstreamTag, Version, and Release.

Modify getsources.sh
- Build XIVLauncher4rpm tarball from local git clone.
- Put in if statements for local builds.
- Now gets UpstreamTag, DownstreamTag from _version file.

Modify spec file
- Now gets UpstreamTag, xlversion, and xlrelease from _version file.
- Moved source2 up above definitions, because I need it declared before using it in %define tags.

### Sun Sep 04 2022 Rankyn Bass <rankyn@proton.me>
Bump version-release to 1.0.1.0-3a

Modify Makefile, getsources.sh
- Remove wget, replace with curl

Modify spec file
- Add -p:BuildHash=UpstreamTag to prevent git describe.
- Drop unneeded git build dependency
- Drop git init section
- Add xivlogo.png to install directory (from misc/header.png)

### Fri Sep 02 2022 Rankyn Bass <rankyn@proton.me>
Bump version-release to 1.0.1.0-3

Modify Makefile, add getsources script
- No longer requires git. Now just needs wget.
- Makefile now calls getsources.sh, which uses wget to download sources
- getsources.sh MUST have matching UpstreamTag and DownstreamTag in spec file.
- No longer call rpmbuild -bp, which should fix problems with building srpm.

Modify spec file
- Now works with downloaded sources instead of downloading with git during prep stage.
- Reorganized importand definitions (%define) to the top of the script
- Worked out a method to deal with ugly long hash name in upstream tarball
- %setup macro was unpacking source0 tarball multiple times. This has been fixed.
- More inline documentation of macros and shell commands.
- Fixed warnings about macros expanding in comments.

Modify README.md
- Updated build instructions.
- Included install instructions for openSUSE.

### Mon Aug 29 2022 Rankyn Bass <rankyn@proton.me>
First changelog entry for setting up for COPR.
