** 1.4.0.11 **
- Fix: Crash on exit bug with some versions of proton 11. The launcher was deleting lsteamclient to fix another bug, and relying on proton to regenerate the file. It was not doing so. Created a fix which checks for lsteamclient in the prefix, and creates/fixes symlink from proton/wine to the prefix, or deletes it if not present in wine release.
- Fix: NumericSettingsEntry was showing double input boxes due launcher changes made in the Discord patch,
