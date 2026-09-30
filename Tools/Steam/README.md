# SteamPipe upload

1. Replace `YOUR_APP_ID` and the depot ids in `app_build.vdf`, `depot_windows.vdf`, `depot_linux.vdf`
   (Steamworks partner site → your app → SteamPipe → Depots).
2. Build in Unity: **Downhill → Build → Windows x64 (Steam release)** (and **Linux x64 (Steam Deck)** if you ship Linux).
   Output: `Builds/Steam/Windows`, `Builds/Steam/Linux`.
3. Upload with steamcmd (from the Steamworks SDK `tools/ContentBuilder/builder`):
   ```
   steamcmd +login <builder_account> +run_app_build ../../../path/to/Tools/Steam/app_build.vdf +quit
   ```
4. Partner site → SteamPipe → Builds: set the uploaded build live on a branch (`beta` first, then `default`).

Development builds (**Windows x64 (development, steam_appid.txt)**) contain `steam_appid.txt` so Steam features work
when launched directly; the depot scripts exclude that file from uploads.
