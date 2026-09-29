using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Creates the Milestone 1 scene and the BoardTuning asset the first time the project is opened
    /// (and on demand via the Downhill menu), so nothing has to be hand-authored in YAML.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/M1_TestScene.unity";
        public const string TuningPath = "Assets/Settings/BoardTuning.asset";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(ScenePath)) CreateTestScene(openAfterwards: string.IsNullOrEmpty(SceneManager.GetActiveScene().path));
            };
        }

        [MenuItem("Downhill/Rebuild M1 Test Scene")]
        public static void RebuildTestScene() => CreateTestScene(openAfterwards: true);

        [MenuItem("Downhill/Select Board Tuning")]
        public static void SelectTuning() => Selection.activeObject = GetOrCreateTuning();

        public static BoardTuning GetOrCreateTuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<BoardTuning>(TuningPath);
            if (tuning != null) return tuning;
            EnsureFolder("Assets/Settings");
            tuning = ScriptableObject.CreateInstance<BoardTuning>();
            AssetDatabase.CreateAsset(tuning, TuningPath);
            AssetDatabase.SaveAssets();
            return tuning;
        }

        static void CreateTestScene(bool openAfterwards)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BoardTuning tuning = GetOrCreateTuning();
            EnsureFolder("Assets/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            var so = new SerializedObject(gm);
            so.FindProperty("tuning").objectReferenceValue = tuning;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.Refresh();
            if (openAfterwards) EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("Downhill: created " + ScenePath + " (press Play).");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
