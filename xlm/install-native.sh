#!/bin/bash

echo "-- XLM Native Auto-Installer --"
echo ""

echo "[Step: 1] Downloading XLM"
curl -L https://github.com/Blooym/xlm/releases/latest/download/xlm-x86_64-unknown-linux-gnu > /tmp/xlm

echo "[Step: 2] Configuring XLM as a Steam Tool using XIVLauncher-RB"
chmod +x /tmp/xlm
/tmp/xlm install-steam-tool --extra-launch-args="--xlcore-repo-owner rankynbass" --steam-compat-path ~/.local/share/Steam/compatibilitytools.d/

echo "[Step: 3] Cleanup XLM binary"
rm /tmp/xlm

echo ""
echo "-- Auto Installer Complete: Restart Steam and follow the README to continue! --"