using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Game.Simulation
{
    /// <summary>One finished run in the local top-10 table.</summary>
    public struct HighScoreEntry
    {
        public float Score, Distance;
        public int Crew;
        /// <summary>yyyy-MM-dd (local date of the run).</summary>
        public string Date;
    }

    public struct HighScoreRecord
    {
        public float BestScore;
        public float BestDistance;
        /// <summary>Best runs by score, highest first (at most <see cref="HighScoreManager.TableSize"/>; null = none yet).</summary>
        public List<HighScoreEntry> Top;
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
                    if (key == "entry")
                    {
                        // entry=score;distance;crew;date
                        string[] f = line.Substring(eq + 1).Split(';');
                        if (f.Length >= 4 && float.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float sc) && SimMath.IsFinite(sc) && sc >= 0f
                            && float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float di) && SimMath.IsFinite(di) && di >= 0f
                            && int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int crew))
                        {
                            if (record.Top == null) record.Top = new List<HighScoreEntry>();
                            record.Top.Add(new HighScoreEntry { Score = sc, Distance = di, Crew = Math.Max(1, Math.Min(6, crew)), Date = f[3].Trim() });
                        }
                        continue;
                    }
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
            var text = new System.Text.StringBuilder();
            text.Append(string.Format(CultureInfo.InvariantCulture, "bestScore={0}\nbestDistance={1}\n", record.BestScore, record.BestDistance));
            if (record.Top != null)
                foreach (HighScoreEntry e in record.Top)
                    text.Append(string.Format(CultureInfo.InvariantCulture, "entry={0};{1};{2};{3}\n", e.Score, e.Distance, e.Crew, (e.Date ?? "").Replace(";", "")));
            File.WriteAllText(tmp, text.ToString());
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
        }
    }

    /// <summary>Keeps best score, best distance and the local top-10 table across runs and app launches.</summary>
    public sealed class HighScoreManager
    {
        public const int TableSize = 10;

        readonly IHighScoreStore _store;
        HighScoreRecord _record;

        public float BestScore => _record.BestScore;
        public float BestDistance => _record.BestDistance;
        /// <summary>Top runs by score, highest first.</summary>
        public IReadOnlyList<HighScoreEntry> Top => _record.Top;
        /// <summary>Supplies the date stored with a run (tests pin it).</summary>
        public Func<string> Today = () => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public HighScoreManager(IHighScoreStore store)
        {
            _store = store;
            _record = store != null ? store.Load() : new HighScoreRecord();
            if (_record.Top == null) _record.Top = new List<HighScoreEntry>(TableSize + 1);
            _record.Top.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (_record.Top.Count > TableSize) _record.Top.RemoveRange(TableSize, _record.Top.Count - TableSize);
            // a file from before the table existed: seed it with the old best so the table is not empty
            if (_record.Top.Count == 0 && _record.BestScore > 0f)
                _record.Top.Add(new HighScoreEntry { Score = _record.BestScore, Distance = _record.BestDistance, Crew = 1, Date = "" });
        }

        /// <summary>Submits a finished run. Returns true if either record was beaten (and saved).</summary>
        public bool Submit(float score, float distance, out bool newBestScore, out bool newBestDistance) =>
            Submit(score, distance, 1, out newBestScore, out newBestDistance, out _);

        /// <summary>Submits a finished run; <paramref name="rank"/> is its 1-based place in the table (0 = not in the top 10).</summary>
        public bool Submit(float score, float distance, int crew, out bool newBestScore, out bool newBestDistance, out int rank)
        {
            newBestScore = score > _record.BestScore;
            newBestDistance = distance > _record.BestDistance;
            if (newBestScore) _record.BestScore = score;
            if (newBestDistance) _record.BestDistance = distance;

            rank = 0;
            List<HighScoreEntry> top = _record.Top;
            if (score > 0f && (top.Count < TableSize || score > top[top.Count - 1].Score))
            {
                int i = 0;
                while (i < top.Count && top[i].Score >= score) i++;
                top.Insert(i, new HighScoreEntry { Score = score, Distance = distance, Crew = Math.Max(1, Math.Min(6, crew)), Date = Today() });
                if (top.Count > TableSize) top.RemoveAt(top.Count - 1);
                rank = i + 1;
            }
            if (!newBestScore && !newBestDistance && rank == 0) return false;
            _store?.Save(_record);
            return newBestScore || newBestDistance;
        }
    }
}
