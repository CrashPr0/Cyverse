using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Cyverse.Tests
{
    /// <summary>
    /// Playtest events print to the console only in the editor and
    /// development builds; release builds keep them in local storage only.
    /// </summary>
    public sealed class PlaytestConsolePlayModeTests
    {
        [Test]
        public void ConsoleEcho_FollowsTheDebugBuildFlag()
        {
            Type metrics = MetricsType();
            PropertyInfo echo = metrics.GetProperty("EchoToConsole", BindingFlags.Public | BindingFlags.Static);
            Assert.That(echo, Is.Not.Null, "PlaytestMetrics.EchoToConsole should exist.");
            Assert.That((bool)echo.GetValue(null), Is.EqualTo(Debug.isDebugBuild),
                "Events should print only in the editor and development builds.");
        }

        [Test]
        public void Record_StoresTheEvent_AndPrintsItInTheEditor()
        {
            Type metrics = MetricsType();
            object instance = metrics.GetMethod("Ensure").Invoke(null, null);

            string marker = "console_test_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            if (Debug.isDebugBuild)
                LogAssert.Expect(LogType.Log, new Regex(@"^\[PLAYTEST_METRIC\] .*" + marker));
            metrics.GetMethod("Record", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { marker, "detail" });

            StringAssert.Contains(marker, (string)metrics.GetMethod("ExportJson").Invoke(instance, null),
                "The event is stored whether or not it is printed.");
        }

        private static Type MetricsType()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("Cyverse.Core.PlaytestMetrics", false);
                if (type != null) return type;
            }
            Assert.Fail("PlaytestMetrics should exist.");
            return null;
        }
    }
}
