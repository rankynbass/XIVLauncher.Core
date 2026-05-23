**1.4.0.4**
- Fixed mangohud detection. It should now work with nix. Switched to using PATH, since mangohud is being called directly.
- Fixed Apps tab. Toggles for slots 2 and 3 were broken. Thanks to Genevieve Mendoza.
- Merged in some fixes from goatcorp repo
- Storage will now be `$XDG_DATA_HOME/dev.goats.xivlauncher`. The old locations will be moved here if they are on the same volume.
- By default, a symlink will be created at `~/.xlcore` to the new `$XDG_DATA_HOME/dev.goats.xivlauncher` location. You can disable this behaviour by passing the environment variable `XL_MAKE_SYMLINK=0`, or by creating a file at `$XDG_CONFIG_HOME/dev.goats.xivlauncher/nosymlink`. The file just has to exist; it can be empty. It will *not* delete an existing symlink.
