![xlcore_sized](https://user-images.githubusercontent.com/16760685/197423373-b6082cdb-dc1f-46db-8768-3f507f182ba8.png)

# XIVLauncher-RB: XIVLauncher.Core with additional patches [![Discord Shield](https://discordapp.com/api/guilds/581875019861328007/widget.png?style=shield)](https://discord.gg/3NMcUV5)
Cross-platform version of XIVLauncher for Linux and Steam Deck. Comes with several versions of [WINE tuned for FFXIV](https://github.com/rankynbass/unofficial-wine-xiv-git), as well as Proton and Steam Runtime support.

## Using on Steam Deck
If you want to use XIVLauncher on your Steam Deck, it's not quite as easy as using the official version, but still not too difficult.

1) You'll want to switch to desktop mode and download the latest flatpak file. From the terminal (Konsole) install with `flatpak install --user xivlauncher-rb-v1.1.0.2.flatpak` (or whatever the latest flatpak file is).
2) Run `XL_USE_STEAM=0 flatpak run dev.rankyn.xivlauncher --deck-install`
3) Restart Steam. This is necessary to get the compatibility tool to register.
3) In Steam, do the initial install of FFXIV or FFXIV free trial. You do not have to run the official launcher, you just need to have it installed in your steam library.
4) Go into the FFXIV properties, and go to Compatibility. Check "Force the use of a specific Steam Play compatibility tool", and select "XIVLauncher.Core as Compatibility Tool".
5) You can now launch the game from desktop mode *or* game mode. Both should work.

If you're having trouble, you can [join the XIVLauncher Discord server](https://discord.gg/3NMcUV5) and join the $linux-and-deck channel. I'm online most days and can usually help out, and there are a number of other people who may also be willing. Please don't use the GitHub issues for troubleshooting unless you're sure that your problem is an actual issue with XIVLauncher-RB.

## Building & Contributing
1. Clone this repository with submodules
2. Make sure you have a recent(.NET 6.0.400+) version of the .NET SDK installed
2. Run `dotnet build` or `dotnet publish`

Common components that are shared with the Windows version of XIVLauncher are linked as a submodule in the "lib" folder. XIVLauncher Core can run on Windows, but is by far not as polished as the [original Windows version](https://github.com/goatcorp/FFXIVQuickLauncher). Windows users should not use this application unless for troubleshooting purposes or development work.

## Distribution
XIVLauncher-RB is not officially supported by the XIVLauncher community, but many people have used it successfully on different linux distros. You can download flatpak files from the releases, but it is also in several user-submitted repos:

| Repo        | Status      |
| ----------- | ----------- |
| [AUR](https://aur.archlinux.org/packages/xivlauncher-rb) | ![AUR version](https://img.shields.io/aur/version/xivlauncher-rb) |
| [Copr (Fedora+openSuse+EL9)](https://copr.fedorainfracloud.org/coprs/rankyn/xivlauncher/) | ![COPR version](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2Frankynbass%2FXIVLauncher4rpm%2FRB-patched%2Fbadge.json)|
| [MPR (Debian+Ubuntu)](https://mpr.makedeb.org/packages/xivlauncher-rb)  | ![MPR package](https://repology.org/badge/version-for-repo/mpr/xivlauncher-rb.svg?header=MPR) |

If there are any others, please let me know and I'll add them.