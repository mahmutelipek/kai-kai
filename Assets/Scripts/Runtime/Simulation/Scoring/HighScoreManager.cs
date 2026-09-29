using System;
using System.Globalization;
using System.IO;

namespace Game.Simulation
{
    public struct HighScoreRecord
    {
        public float BestScore;
        public float BestDistance;
    }

    /// <summary>Where high scores live. Unity uses a file in Application.persistentDataPath; tests use a temp file.</summary>
    public interface IHighScoreStore
    {
        HighScoreRecord Load();
        void Save(HighScoreRecord record);
    }

    /// <summary>Tiny text file store ("bestScore=...", "bestDistance=..."), culture-invariant, atomic replace.</summary>
    public sealed class FileHighScoreStore : IHighScoreStore
    {
        readonly string _path;

        public FileHighScoreStore(string path) { _path = path; }

        public HighScoreRecord Load()
        {
            var record = new HighScoreRecord();
            try
            {
                if (!File.Exists(_path)) return record;
                foreach (string line in File.ReadAllLines(_path))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    if (!float.TryParse(line.Substring(eq + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) continue;
                    if (!SimMath.IsFinite(value) || value < 0f) continue;
                    if (key == "bestScore") record.BestScore = value;
                    else if (key == "bestDistance") record.BestDistance = value;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return record;
        }

        public void Save(HighScoreRecord record)
        {
            string dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, string.Format(CultureInfo.InvariantCulture, "bestScore={0}\nbestDistance={1}\n", record.BestScore, record.BestDistance));
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
        }
    }

    /// <summary>Keeps best score and best distance across runs and app launches.</summary>
    public sealed class HighScoreManager
    {
        readonly IHighScoreStore _store;
        HighScoreRecord _record;

        public float BestScore => _record.BestScore;
        public float BestDistance => _record.BestDistance;

        public HighScoreManager(IHighScoreStore store)
        {
            _store = store;
            _record = store != null ? store.Load() : new HighScoreRecord();
        }

        /// <summary>Submits a finished run. Returns true if either record was beaten (and saved).</summary>
        public bool Submit(float score, float distance, out bool newBestScore, out bool newBestDistance)
        {
            newBestScore = score > _record.BestScore;
            newBestDistance = distance > _record.BestDistance;
            if (!newBestScore && !newBestDistance) return false;
            if (newBestScore) _record.BestScore = score;
            if (newBestDistance) _record.BestDistance = distance;
            _store?.Save(_record);
            return true;
        }
    }
}
