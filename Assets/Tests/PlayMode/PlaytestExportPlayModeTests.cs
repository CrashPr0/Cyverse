using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Cyverse.Tests
{
    /// <summary>
    /// Settings → Export Data saves the playtest log kept on this device as a
    /// CSV or JSON file. Outside WebGL the file lands under
    /// persistentDataPath/exports, which is what these tests read back.
    /// </summary>
    public sealed class PlaytestExportPlayModeTests
    {
        private const string Tricky = "prompt=Press E, then \"Enter\"\nnext line";

        [Test]
        public void Cell_QuotesCommasQuotesAndLineBreaks_AndDefusesFormulas()
        {
            MethodInfo cell = ExportType().GetMethod("Cell", BindingFlags.Public | BindingFlags.Static);
            Assert.That(cell, Is.Not.Null);
            Func<string, string> Cell = v => (string)cell.Invoke(null, new object[] { v });

            Assert.That(Cell("plain"), Is.EqualTo("plain"));
            Assert.That(Cell(null), Is.EqualTo(""));
            Assert.That(Cell("a,b"), Is.EqualTo("\"a,b\""));
            Assert.That(Cell("say \"hi\""), Is.EqualTo("\"say \"\"hi\"\"\""));
            Assert.That(Cell("two\nlines"), Is.EqualTo("\"two\nlines\""));
            Assert.That(Cell("=SUM(A1)"), Is.EqualTo("'=SUM(A1)"));
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator ExportData_FromSettings_WritesCsvAndJsonWithEveryStoredEvent()
        {
            SceneManager.LoadScene("Level1_IAM", LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            Type metricsType = FindType("Cyverse.Core.PlaytestMetrics");
            Type settingsType = FindType("Cyverse.Settings.AccessibilitySettings");
            Assert.That(metricsType, Is.Not.Null);
            Assert.That(settingsType, Is.Not.Null);
            string marker = "export_test_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            metricsType.GetMethod("Record", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { marker, Tricky });

            object settings = settingsType.GetProperty("Instance").GetValue(null);
            Assert.That(settings, Is.Not.Null, "Level 1 should have a settings menu.");
            MethodInfo export = settingsType.GetMethod("ExportData", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(export, Is.Not.Null, "Settings should expose the Export Data action.");

            // Default format is CSV.
            Assert.That((bool)export.Invoke(settings, null), Is.True, "CSV export should save.");
            string csvPath = LastPath();
            Assert.That(csvPath, Does.EndWith(".csv"));
            string csv = File.ReadAllText(csvPath);
            Assert.That(csv, Does.StartWith("sessionId,playthrough,utc,seconds,scene,eventName,detail\r\n"));
            Assert.That(csv, Does.Contain(marker));
            Assert.That(csv, Does.Contain("\"prompt=Press E, then \"\"Enter\"\" next line\""),
                "Details are flattened to one line and quoted.");

            // Left/Right switches the row to JSON.
            FieldInfo format = settingsType.GetField("exportFormat", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(format, Is.Not.Null);
            format.SetValue(settings, Enum.Parse(format.FieldType, "Json"));
            Assert.That((bool)export.Invoke(settings, null), Is.True, "JSON export should save.");
            string jsonPath = LastPath();
            Assert.That(jsonPath, Does.EndWith(".json"));
            string json = File.ReadAllText(jsonPath);
            Assert.That(json, Does.Contain(marker));
            Assert.That(json, Does.Contain("\"events\""));
            Assert.That(json, Does.Contain("\"playthrough\""));
            Assert.That(json, Does.Contain("\"utc\""));
            // The first export recorded itself, so the second file shows it.
            Assert.That(json, Does.Contain("data_exported"));

            File.Delete(csvPath);
            File.Delete(jsonPath);
        }

        [Test]
        public void StartOver_MovesEventsToTheNextPlaythrough()
        {
            Type metricsType = FindType("Cyverse.Core.PlaytestMetrics");
            object metrics = metricsType.GetMethod("Ensure").Invoke(null, null);
            PropertyInfo playthrough = metricsType.GetProperty("Playthrough");
            Assert.That(playthrough, Is.Not.Null);
            int before = (int)playthrough.GetValue(metrics);
            metricsType.GetMethod("Record", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { "playthrough_reset", "source=test" });
            Assert.That((int)playthrough.GetValue(metrics), Is.EqualTo(before + 1));
        }

        private static string LastPath()
        {
            string path = (string)ExportType().GetProperty("LastLocalPath").GetValue(null);
            Assert.That(path, Is.Not.Null.And.Not.Empty);
            Assert.That(File.Exists(path), Is.True, path + " should exist.");
            return path;
        }

        private static Type ExportType()
        {
            Type t = FindType("Cyverse.Core.PlaytestExport");
            Assert.That(t, Is.Not.Null, "PlaytestExport should exist.");
            return t;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
