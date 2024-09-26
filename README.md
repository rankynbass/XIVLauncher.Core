![xlcore_sized](https://user-images.githubusercontent.com/16760685/197423373-b6082cdb-dc1f-46db-8768-3f507f182ba8.png)

# XIVLauncher-RB: XIVLauncher.Core with additional patches [![Discord Shield](https://discordapp.com/api/guilds/581875019861328007/widget.png?style=shield)](https://discord.gg/3NMcUV5)
Cross-platform version of XIVLauncher for Linux and Steam Deck. Comes with several versions of [WINE tuned for FFXIV](https://github.com/rankynbass/unofficial-wine-xiv-git), as well as Proton and Steam Runtime support.

## Changes from XIVLauncher.Core
1) Proton support. At the moment there are no plans to add proton to XIVLauncher.Core, so if you want to use proton, this is it. To enable proton, go to settings, Wine tab, and change the Installation Type to Steam Runtime with Proton
2) Preview of various PRs for XIVLauncher.Core: I try to add various feature and bugfix PRs before they get merged.
3) Wine and DXVK switchers. This feature will hopefully be merged upstream soon. For now, though, you can test it out here.
    - Allows switching between various pre-selected Wine and DXVK versions. DXVK is moved to its own tab.
    - Allows you to easily add new wine and dxvk versions by dropping them in `~/.xlcore/compatibilitytool/wine` and `~/.xlcore/compatibilitytool/dxvk`, respectively.
4) Automatic DLSS. You can either use proton, or choose an nvapi version in the DXVK tab. You need to use Wine 9, ValveBE wine, or wine-ge 8-x wine, along with DXVK 2.0+.
5) Auto-Start other windows programs from the Auto-Start tab. They'll be launched within the same prefix (and container, in the case of proton) just before FFXIV is launched. Only works with windows programs at the moment.
6) Managed wine includes several versions taken from my github repos at [Unofficial Wine-XIV](https://github.com/rankynbass/unofficial-wine-xiv-git) and [Wine-GE-XIV](https://github.com/rankynbass/https://github.com/rankynbass/wine-ge-xiv) in addition to the official versions.

## Using as a Steam compatibility tool using XLM (recommended for Steam Deck)
[XLM](https://github.com/Blooym/xlm) is now the recommended way to install XIVLauncher-RB as a compatibility tool. Instructions are copied from the XLM readme.

Run one of the following commands to install XIVLauncher-RB as a Steam compatibility tool using XLM. What command you need to run depends on how you have Steam installed.

**Steamdeck**:

```sh
sh -c "$(curl -fsSL https://raw.githubusercontent.com/rankynbass/XIVLauncher.Core/refs/heads/RB-patched/xlm/install-steamdeck.sh)"
```

**Steam (Native)**
```sh
sh -c "$(curl -fsSL https://raw.githubusercontent.com/rankynbass/XIVLauncher.Core/refs/heads/RB-patched/xlm/install-native.sh)"
```

**Steam (Flatpak)**
```sh
sh -c "$(curl -fsSL https://raw.githubusercontent.com/rankynbass/XIVLauncher.Core/refs/heads/RB-patched/xlm/install-flatpak.sh)"
```

After the auto-installer has finished running, follow these steps to use it in Steam:
1) Switch back to gaming mode (Steam Deck) or restart Steam.
2) Navigate to your library and select "FINAL FANTASY XIV Online" or "FINAL FANTASY XIV Online Free Trial" (trial and non-steam users).**&midast;**
3) Open the game properties menu and switch to the "compatibility" tab.
4) Enable the "Force the use of a specific Steam Play compatibility tool" checkbox.
5) From the dropdown that appears select "XLCore [XLM]" (if this does not show, please make sure you restarted Steam first).
6) You can now launch the game. XIVLauncher-RB will be automatically installed to the compatibilitytools.d directory and start as usual. When you close the game, Steam will recognise this.

**&midast;** If FFXIV or the FFXIV free trial are not available on Steam in your region, you can technically use *any* game. Download a free game from steam and set up the controls how you like for FFXIV, and then set up the compatibility tool as above from step 3.

## Using on Steam Deck (depricated, <= 1.1.0.13)
If you want to use XIVLauncher on your Steam Deck, it's not quite as easy as using the official version, but still not too difficult.

1) You'll want to switch to desktop mode and download the latest flatpak file. From the terminal (Konsole) install with `flatpak install --user xivlauncher-rb-v1.1.0.2.flatpak` (or whatever the latest flatpak file is).
2) Run `XL_USE_STEAM=0 flatpak run dev.rankyn.xivlauncher --deck-install`
3) Restart Steam. This is necessary to get the compatibility tool to register.
3) In Steam, do the initial install of FFXIV or FFXIV free trial. You do not have to run the official launcher, you just need to have it installed in your steam library.
4) Go into the FFXIV properties, and go to Compatibility. Check "Force the use of a specific Steam Play compatibility tool", and select "XIVLauncher.Core as Compatibility Tool".
5) You can now launch the game from desktop mode *or* game mode. Both should work.

## Using the AppImage
I've started creating AppImages as well. These have worked on the Steam Deck in my testing, although there are a couple of minor issues.
1) Download the AppImage tarball and extract somewhere.
2) Run the included `install.sh` script. This will move the AppImage to ~/Applications and create a .desktop entry in your menus.
3) The desktop shortcut can now be added to steam the same way you'd do with the official Flatpak, without the need for the extra parameters. You will still need the `XL_SECRET_PROVIDER=file` if you want to save passwords in Game Mode.
4) Do *not* hilight the AppImage file in your file browser and launch with the enter key. It launches multiple copies, for some unknown reason. Double-clicking on it, or launching from the terminal or desktop file all work fine, however.

If you're having trouble, you can [join the XIVLauncher Discord server](https://discord.gg/3NMcUV5), grab the Steam Deck & Linux and join the #xlcore-questions channel. I'm online most days and can usually help out, and there are a number of other people who may also be willing. Please don't use the GitHub issues for troubleshooting unless you're sure that your problem is an actual issue with XIVLauncher-RB.

## Environment Variables for troubleshooting
| Variable      | Description    |
| ------------- | -------------- |
| `XL_SECRET_PROVIDER` | Set to `file` if using the Steam Deck or other desktop session that doesn't have a secret provider. |
| `XL_DECK` | Force XIVLauncher-RB to pretend it's Steam Deck. Does not enable the Steam keyboard. |
| `XL_GAMEMODE` | Forces XIVLauncher-RB to pretend it's in Steam Deck Game Mode. Also does not enable the Steam keyboard. |
| `XL_FIRSTRUN` | Set to 0 or 1 to force the launcher to skip or activate the Steam Deck First Run screen. |
| `XL_USE_STEAM` | Set to 0 or 1 to enable or disable steam API checks. |
| `XL_APPID` | Set to a steam AppID number to hook that application instead of FFXIV or the free trial. |
| `XL_FORCE_DLSS` | Skip DLSS checks and assume that the nvngx dlls are in the game folder. |
| `XL_NVNGXPATH` | Set a custom path for the folder containing nvngx.dll and _nvngx.dll. Most useful for NixOS, which has unusual paths. |

## Building & Contributing
1. Clone this repository with submodules
2. Make sure you have a recent(.NET 8.0) version of the .NET SDK installed
2. Run `dotnet build` or `dotnet publish`

Common components that are shared with the Windows version of XIVLauncher are linked as a submodule in the "lib" folder. XIVLauncher Core can run on Windows, but is by far not as polished as the [original Windows version](https://github.com/goatcorp/FFXIVQuickLauncher). Windows users should not use this application unless for troubleshooting purposes or development work.

## Distribution
XIVLauncher-RB is not officially supported by the XIVLauncher community, but many people have used it successfully on different linux distros. You can download flatpak files and AppImages from the releases, but it is also in several user-submitted repos:

| Repo        | Status      |
| ----------- | ----------- |
| [AUR](https://aur.archlinux.org/packages/xivlauncher-rb) | ![AUR version](https://img.shields.io/aur/version/xivlauncher-rb) |
| [Copr (Fedora+openSuse+EL9)](https://copr.fedorainfracloud.org/coprs/rankyn/xivlauncher/) | ![COPR version](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2Frankynbass%2FXIVLauncher4rpm%2FRB-patched%2Fbadge.json)|
| [MPR (Debian+Ubuntu)](https://mpr.makedeb.org/packages/xivlauncher-rb)&#42; | ![MPR package](https://repology.org/badge/version-for-repo/mpr/xivlauncher-rb.svg?header=MPR) |
| [Nix Flake](https://github.com/drakon64/nixos-xivlauncher-rb) | [![Update](https://github.com/drakon64/nixos-xivlauncher-rb/actions/workflows/xivlauncher-rb.yml/badge.svg?event=schedule)](https://github.com/drakon64/nixos-xivlauncher-rb/actions/workflows/xivlauncher-rb.yml) |

&#42; The MPR just pulls the latest git master, so it should always be up to date. If you want a specific tag, change the url in the PKGBUILD source section from `branch=RB-patched` to `tag={tag}`. Tags will be in the format `rb-v1.1.0.11`.

If there are any others, please let me know and I'll add them.
