using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Steam builds: Windows x64 (main), Linux x64 (Steam Deck / Linux) and macOS. Sets the player settings a
    /// Steam PC game wants, builds into Builds/Steam/&lt;platform&gt;/, and for development builds drops
    /// steam_appid.txt next to the executable so Steam features work without launching through Steam.
    /// Command line: Unity -batchmode -quit -projectPath . -executeMethod Game.EditorTools.BuildScript.BuildWindows
    /// </summary>
    public static class BuildScript
    {
        public const string ProductName = "Downhill Party Board";
        public const string Version = "0.5.0";
        const string ExeName = "DownhillPartyBoard";
        const string Root = "Builds/Steam";

        [MenuItem("Downhill/Build/Windows x64 (Steam release)")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Windows", ExeName + ".exe", development: false);

        [MenuItem("Downhill/Build/Windows x64 (development, steam_appid.txt)")]
        public static void BuildWindowsDev() => Build(BuildTarget.StandaloneWindows64, "Windows-dev", ExeName + ".exe", development: true);

        [MenuItem("Downhill/Build/Linux x64 (Steam Deck)")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Linux", ExeName + ".x86_64", development: false);

        [MenuItem("Downhill/Build/macOS (Steam)")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "macOS", ExeName + ".app", development: false);

        public static void ApplyPlayerSettings()
        {
            if (string.IsNullOrEmpty(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany")
                PlayerSettings.companyName = "Downhill Party Team"; // TODO: your studio name
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; // borderless: fast alt-tab, Steam overlay friendly
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;            // keeps Steam callbacks and Remote Play streams alive
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.forceSingleInstance = true;
            PlayerSettings.usePlayerLog = true;
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
#else
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
#endif
            try { PlayerSettings.SplashScreen.showUnityLogo = false; } catch (Exception) { /* not allowed on this license */ }
        }

        static void Build(BuildTarget target, string folder, string exe, bool development)
        {
            ProjectSetup.EnsureAll();
            ApplyPlayerSettings();
            string dir = Path.Combine(Root, folder);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = Path.Combine(dir, exe),
                target = target,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"Downhill: {folder} build failed ({report.summary.totalErrors} errors).");

            if (development) File.WriteAllText(Path.Combine(dir, "steam_appid.txt"), Game.SteamSettings.AppId.ToString());
            File.Copy("Docs/THIRD_PARTY_NOTICES.md", Path.Combine(dir, "THIRD_PARTY_NOTICES.txt"), true);
            Debug.Log($"Downhill: {folder} build OK -> {dir} ({report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime.TotalSeconds:0} s)");
        }
    }
}
