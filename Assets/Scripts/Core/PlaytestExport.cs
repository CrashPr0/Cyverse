using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Cyverse.Core
{
    /// <summary>
    /// Settings → Export Data: hands the playtest telemetry kept in this
    /// browser to the player as a CSV or JSON file, for study sessions.
    /// Everything stays on the device: on WebGL the file is a browser
    /// download built from a Blob, nothing is uploaded. In the editor and
    /// desktop builds it is written under <c>persistentDataPath/exports</c>.
    /// </summary>
    public static class PlaytestExport
    {
        public enum Format { Csv, Json }

        public static readonly string[] CsvColumns =
            { "sessionId", "playthrough", "utc", "seconds", "scene", "eventName", "detail" };

        [Serializable]
        private sealed class ExportEnvelope
        {
            public string exportedUtc;
            public string build;
            public string currentSessionId;
            public int eventCount;
            public PlaytestMetrics.MetricEvent[] events;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int Cyverse_DownloadText(string fileName, string mime, string text);
#endif

        /// <summary>Last file written outside the browser (editor, desktop, tests).</summary>
        public static string LastLocalPath { get; private set; }

        /// <summary>Builds the file from everything stored and offers it to the
        /// player. Returns false if it could not be saved.</summary>
        public static bool Export(Format format)
        {
            PlaytestMetrics metrics = PlaytestMetrics.Ensure();
            List<PlaytestMetrics.MetricEvent> events = metrics.AllStoredEvents();
            string text = format == Format.Csv ? BuildCsv(events) : BuildJson(events, metrics.SessionId);
            string name = FileName(format, DateTime.UtcNow);
            bool ok = Save(name, format == Format.Csv ? "text/csv" : "application/json", text);
            PlaytestMetrics.Record("data_exported",
                "format=" + (format == Format.Csv ? "csv" : "json") + ";events=" + events.Count + ";ok=" + ok);
            return ok;
        }

        public static string FileName(Format format, DateTime utc) =>
            "cyverse-playtest-" + utc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) +
            (format == Format.Csv ? ".csv" : ".json");

        public static string BuildCsv(IList<PlaytestMetrics.MetricEvent> events)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", CsvColumns)).Append("\r\n");
            foreach (PlaytestMetrics.MetricEvent e in events)
            {
                sb.Append(Cell(e.sessionId)).Append(',')
                  .Append(e.playthrough.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Cell(e.utc)).Append(',')
                  .Append(e.seconds.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                  .Append(Cell(e.scene)).Append(',')
                  .Append(Cell(e.eventName)).Append(',')
                  .Append(Cell(e.detail)).Append("\r\n");
            }
            return sb.ToString();
        }

        public static string BuildJson(IList<PlaytestMetrics.MetricEvent> events, string currentSessionId)
        {
            var array = new PlaytestMetrics.MetricEvent[events.Count];
            events.CopyTo(array, 0);
            return JsonUtility.ToJson(new ExportEnvelope
            {
                exportedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                build = Application.version,
                currentSessionId = currentSessionId,
                eventCount = array.Length,
                events = array,
            }, true);
        }

        /// <summary>RFC 4180 cell: quoted when it holds a comma, quote or line
        /// break. A leading = + - @ gets a ' so spreadsheets don't run it as a
        /// formula.</summary>
        public static string Cell(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if ("=+-@".IndexOf(value[0]) >= 0) value = "'" + value;
            bool quote = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!quote) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static bool Save(string fileName, string mime, string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Cyverse_DownloadText(fileName, mime, text) == 1;
#else
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "exports");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, fileName);
                File.WriteAllText(path, text, new UTF8Encoding(false));
                LastLocalPath = path;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
#endif
        }
    }
}
