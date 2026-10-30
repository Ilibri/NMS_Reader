using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;

namespace NMS_Reader.Services
{
    public class DialogueDatabaseService : IDisposable
    {
        private readonly string _dbPath;
        private SqliteConnection? _connection;
        private readonly object _lock = new();

        public bool IsAvailable { get; private set; }

        public DialogueDatabaseService()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _dbPath = Path.Combine(baseDir, "Data", "nms_dialogues.db");

            // Also check project folder in dev environment
            if (!File.Exists(_dbPath))
            {
                string devPath = Path.Combine(baseDir, "..", "..", "..", "Data", "nms_dialogues.db");
                if (File.Exists(devPath))
                {
                    _dbPath = Path.GetFullPath(devPath);
                }
            }

            Initialize();
        }

        private void Initialize()
        {
            try
            {
                if (File.Exists(_dbPath))
                {
                    _connection = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly;Cache=Shared");
                    _connection.Open();
                    IsAvailable = true;
                    Debug.WriteLine($"[DialogueDatabaseService] Initialized with DB: {_dbPath}");
                }
                else
                {
                    Debug.WriteLine($"[DialogueDatabaseService] DB not found at {_dbPath}");
                    IsAvailable = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DialogueDatabaseService Error]: {ex.Message}");
                IsAvailable = false;
            }
        }

        public string MatchAndCorrect(string text, double minRatio = 0.85)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 15 || !IsAvailable || _connection == null)
            {
                return text;
            }

            string clean = text.Trim();
            int len = clean.Length;
            int minLen = (int)(len * 0.80);
            int maxLen = (int)(len * 1.20);

            var sw = Stopwatch.StartNew();

            lock (_lock)
            {
                try
                {
                    var candidates = new List<string>();

                    // 1. Try prefix search (first 15 chars)
                    string prefix = clean.Length > 15 ? clean.Substring(0, 15) : clean;
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT text FROM dialogues WHERE length BETWEEN @minLen AND @maxLen AND text LIKE @prefix LIMIT 15";
                        cmd.Parameters.AddWithValue("@minLen", minLen);
                        cmd.Parameters.AddWithValue("@maxLen", maxLen);
                        cmd.Parameters.AddWithValue("@prefix", prefix + "%");

                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            candidates.Add(reader.GetString(0));
                        }
                    }

                    // 2. If no candidate with prefix, try length-only search limited to 40
                    if (candidates.Count == 0 && len >= 25)
                    {
                        using var cmd = _connection.CreateCommand();
                        {
                            cmd.CommandText = "SELECT text FROM dialogues WHERE length BETWEEN @minLen AND @maxLen LIMIT 40";
                            cmd.Parameters.AddWithValue("@minLen", minLen);
                            cmd.Parameters.AddWithValue("@maxLen", maxLen);

                            using var reader = cmd.ExecuteReader();
                            while (reader.Read())
                            {
                                candidates.Add(reader.GetString(0));
                            }
                        }
                    }

                    if (candidates.Count == 0) return text;

                    string? bestMatch = null;
                    double bestScore = 0.0;

                    foreach (var cand in candidates)
                    {
                        double score = CalculateSimilarity(clean, cand);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestMatch = cand;
                        }
                    }

                    sw.Stop();

                    if (bestMatch != null && bestScore >= minRatio)
                    {
                        Debug.WriteLine($"[FuzzyMatch] Replaced ({bestScore:P1} match in {sw.ElapsedMilliseconds} ms):\n  FROM: '{clean}'\n  TO:   '{bestMatch}'");
                        return bestMatch;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FuzzyMatch Query Error]: {ex.Message}");
                }
            }

            return text;
        }

        private static double CalculateSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;
            if (s1 == s2) return 1.0;

            int dist = LevenshteinDistance(s1, s2);
            int maxLen = Math.Max(s1.Length, s2.Length);
            return 1.0 - ((double)dist / maxLen);
        }

        private static int LevenshteinDistance(string s1, string s2)
        {
            int n = s1.Length;
            int m = s2.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _connection?.Close();
                _connection?.Dispose();
                _connection = null;
                IsAvailable = false;
            }
        }
    }
}
