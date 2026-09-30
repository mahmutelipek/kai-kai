#!/usr/bin/env bash
# M4 acceptance 3: every obstacle / pickup alone at 50 m and 30 m ahead, gameplay camera, 1920x1080.
# Measures on-screen size and per-pixel contrast against the lit road colour (70,74,84).
set -euo pipefail
cd "$(dirname "$0")"
names="reaction50"; for k in $(seq 0 11); do names="$names react50_$k react30_$k"; done
dotnet bin/Debug/net8.0/ArtPreview.dll out $names > /dev/null
node shot.mjs out/reaction50.json out/reaction50.png --width 1920 --height 1080 > /dev/null
for k in $(seq 0 11); do
  for d in 50 30; do node shot.mjs out/react${d}_$k.json out/react${d}_$k.png --width 1920 --height 1080 --analyze out/react${d}_$k.stats.json --ref 70,74,84 > /dev/null & done
  wait
done
python3 measure.py
