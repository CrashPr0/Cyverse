using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cyverse.Core
{
    /// <summary>
    /// Small, serverless playtest flight recorder. It deliberately stores only
    /// gameplay telemetry (not the player's callsign or form answers): scene
    /// transitions, objectives, interaction attempts, score changes, results,
    /// and runtime errors. Every event is emitted as a JSON log line so a
    /// browser/Unity console capture can be analysed without a backend.
    /// </summary>
    public sealed class PlaytestMetrics : MonoBehaviour
    {
        public const string PrefsKey = "cv_playtest_metrics_json";
        public const string FileName = "cyverse-playtest-metrics.jsonl";
        private const int MaxEvents = 256;

        [Serializable]
        public sealed class MetricEvent
        {
            public string sessionId;
            public string eventName;
            public string scene;
            public float seconds;
            public string detail;
        }

        [Serializable]
        private sealed class MetricEnvelope
        {
            public string sessionId;
            public string build;
            public MetricEvent[] events;
        }

        public static PlaytestMetrics Instance { get; private set; }
        public string SessionId { get; private set; }
        public int EventCount => events.Count;
        public string StoragePath => Path.Combine(Application.persistentDataPath, FileName);

        private readonly List<MetricEvent> events = new List<MetricEvent>(MaxEvents);
        private float sessionStart;
        private bool shuttingDown;
        private bool persistenceWarningShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Ensure();
        }

        public static PlaytestMetrics Ensure()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("PlaytestMetrics");
            Instance = go.AddComponent<PlaytestMetrics>();
            DontDestroyOnLoad(go);
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            sessionStart = Time.realtimeSinceStartup;
            SessionId = BuildSessionId();

            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.logMessageReceived += OnLogMessage;
            Application.quitting += OnQuitting;

            RecordInternal("session_start", "build=" + Application.version);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.logMessageReceived -= OnLogMessage;
            Application.quitting -= OnQuitting;
            FlushPrefs();
            Instance = null;
        }

        private void OnQuitting()
        {
            if (shuttingDown) return;
            shuttingDown = true;
            RecordInternal("session_end", "events=" + events.Count);
            FlushPrefs();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RecordInternal("scene_loaded", scene.name + "|mode=" + mode);
        }

        private void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (shuttingDown || message.StartsWith("[PLAYTEST_METRIC]", StringComparison.Ordinal))
                return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            string detail = type + ": " + Compact(message, 360);
            if (!string.IsNullOrEmpty(stackTrace))
                detail += " | " + Compact(stackTrace, 360);
            RecordInternal("runtime_error", detail);
        }

        public static void Record(string eventName, string detail = null)
        {
            Ensure().RecordInternal(eventName, detail);
        }

        public static void RecordInteraction(string targetType, string prompt)
        {
            Record("interaction", "target=" + Compact(targetType, 80) +
                ";prompt=" + Compact(prompt, 180));
        }

        public static void RecordObjective(string objective)
        {
            Record("objective", Compact(objective, 240));
        }

        public static void RecordScore(int delta, int total)
        {
            Record("score", "delta=" + delta + ";total=" + total);
        }

        public static void RecordResult(string header, int score, int quizCorrect,
            int quizTotal, float seconds)
        {
            Record("level_result", "header=" + Compact(header, 100) +
                ";score=" + score + ";quiz=" + quizCorrect + "/" + quizTotal +
                ";duration=" + seconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        }

        public string ExportJson()
        {
            return JsonUtility.ToJson(new MetricEnvelope
            {
                sessionId = SessionId,
                build = Application.version,
                events = events.ToArray()
            });
        }

        public static string ReadStoredJson()
        {
            return PlayerPrefs.GetString(PrefsKey, "");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearStored()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();

            if (Instance != null)
                Instance.events.Clear();
        }
#endif

        private void RecordInternal(string eventName, string detail)
        {
            if (shuttingDown && eventName != "session_end") return;
            if (string.IsNullOrEmpty(eventName)) return;

            var metric = new MetricEvent
            {
                sessionId = SessionId,
                eventName = eventName,
                scene = SceneManager.GetActiveScene().name,
                seconds = Mathf.Max(0f, Time.realtimeSinceStartup - sessionStart),
                detail = Compact(detail, 520)
            };

            if (events.Count >= MaxEvents) events.RemoveAt(0);
            events.Add(metric);
            Persist(metric);

            // This prefix makes browser-console filtering and CI log scraping
            // trivial while keeping the payload machine-readable.
            Debug.Log("[PLAYTEST_METRIC] " + JsonUtility.ToJson(metric));
        }

        private void Persist(MetricEvent metric)
        {
            string line = JsonUtility.ToJson(metric) + "\n";
            try
            {
                File.AppendAllText(StoragePath, line);
            }
            catch (Exception ex)
            {
                if (!persistenceWarningShown)
                {
                    persistenceWarningShown = true;
                    Debug.LogWarning("[PLAYTEST_METRIC] File persistence unavailable: " + ex.GetType().Name);
                }
            }

            try
            {
                PlayerPrefs.SetString(PrefsKey, ExportJson());
                // WebGL has no reliable process-shutdown callback when a tab
                // is closed or the browser crashes. Save each bounded update
                // so the latest useful trail is still available after reload.
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                if (!persistenceWarningShown)
                {
                    persistenceWarningShown = true;
                    Debug.LogWarning("[PLAYTEST_METRIC] PlayerPrefs persistence unavailable: " + ex.GetType().Name);
                }
            }
        }

        private void FlushPrefs()
        {
            try
            {
                PlayerPrefs.SetString(PrefsKey, ExportJson());
                PlayerPrefs.Save();
            }
            catch (Exception)
            {
                // Shutdown should never turn a successful playtest into an
                // application error just because storage is unavailable.
            }
        }

        private static string BuildSessionId()
        {
            return DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" +
                Mathf.Abs(Environment.TickCount).ToString("X");
        }

        private static string Compact(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string compact = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return compact.Length <= maxLength ? compact : compact.Substring(0, maxLength);
        }
    }
}
