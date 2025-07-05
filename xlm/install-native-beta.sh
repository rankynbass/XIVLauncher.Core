#!/bin/bash

echo "-- XLM Native Auto-Installer --"
echo ""

echo "[Step: 1] Downloading XLM"
curl -L https://github.com/Blooym/xlm/releases/latest/download/xlm-x86_64-unknown-linux-gnu > /tmp/xlm

echo "[Step: 2] Configuring XLM as a Steam Tool using XIVLauncher-RB"
chmod +x /tmp/xlm
/tmp/xlm install-steam-tool --extra-launch-args="--xlcore-web-release-url=\"https://github.com/rankynbass/XIVLauncher.Core/releases/download/rb-v1.2.1.1-beta/\"" --steam-compat-path ~/.steam/root/compatibilitytools.d/

echo "[Step: 3] Cleanup XLM binary"
rm /tmp/xlm

echo ""
echo "-- Auto Installer Complete: Restart Steam and follow the README to continue! --"