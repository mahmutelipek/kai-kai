#!/usr/bin/env bash
# Renders every M4 preview scene into out/ (needs: dotnet build, npm install in this folder).
set -euo pipefail
cd "$(dirname "$0")"
dotnet build -nologo -v q -clp:NoSummary
scenes="chars_front chars_back chars_colors board_gameplay game_start game_ramp game_construction game_bridge game_tunnel game_curve game_traffic"
dotnet bin/Debug/net8.0/ArtPreview.dll out $scenes chars_silhouette
for s in $scenes; do node shot.mjs out/$s.json out/$s.png --width 1600 --height 900 > /dev/null & done; wait
node shot.mjs out/chars_silhouette.json out/chars_silhouette.png --width 1920 --height 1080 --analyze out/chars_silhouette.stats.json --masks P > /dev/null
node shot.mjs out/chars_colors.json out/chars_colors.png --width 1920 --height 1080 --analyze out/chars_colors.stats.json > /dev/null
python3 silhouettes.py
