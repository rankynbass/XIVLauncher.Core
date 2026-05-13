**1.4.0.3**
- XIVLauncher-RB will now search for the storage in the following order: $XL_USERDIR env variable, $XDG_DATA_HOME/xlcore, ~/.xlcore
- The storage directory is now passed to compatibility tools instead of the tools folder; this is mostly an internal housekeeping chore.
- Umu will now keep track of the downloaded version, and if a different version is provided via the .json file, it will be updated.
