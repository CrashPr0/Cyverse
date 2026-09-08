using System.Collections;
using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Cyverse.Tests
{
    /// <summary>Smoke coverage for the isolated Level 4 visual layer.
    /// This test does not require a Level 4 scene asset, so it protects the
    /// primitive/TMP foundation while the mechanics scene is being authored.</summary>
    public class Level4VisualFoundationTests
    {
        [UnityTest]
        [Timeout(30000)]
        public IEnumerator BuildsFourBoundedStationZonesAndMeter()
        {
            GameObject host = new GameObject("Level4VisualFoundationTestHost");
            Type visualsType = FindType("Cyverse.Level.Level4CyberAttackVisuals");
            Component visuals = host.AddComponent(visualsType);
            visualsType.GetMethod("Apply", BindingFlags.Public | BindingFlags.Instance)
                .Invoke(visuals, null);
            yield return null;
            yield return null;

            Assert.That(GameObject.Find("L4_ZonePad_bypass-mfa"), Is.Not.Null);
            Assert.That(GameObject.Find("L4_ZonePad_escalate-privileges"), Is.Not.Null);
            Assert.That(GameObject.Find("L4_ZonePad_extract-data"), Is.Not.Null);
            Assert.That(GameObject.Find("L4_ZonePad_cover-tracks"), Is.Not.Null);

            Type meterType = FindType("Cyverse.Level.Level4ExfiltrationMeter");
            Component meter = UnityEngine.Object.FindObjectOfType(meterType) as Component;
            Assert.That(meter, Is.Not.Null);
            meterType.GetMethod("SetProgress", BindingFlags.Public | BindingFlags.Instance)
                .Invoke(meter, new object[] { 0.42f, 0.18f });
            float data = (float)meterType.GetProperty("DataProgress").GetValue(meter);
            float detection = (float)meterType.GetProperty("DetectionProgress").GetValue(meter);
            Assert.That(data, Is.EqualTo(0.42f).Within(0.001f));
            Assert.That(detection, Is.EqualTo(0.18f).Within(0.001f));

            foreach (TextMeshPro text in UnityEngine.Object.FindObjectsOfType<TextMeshPro>(true))
            {
                if (!text.name.StartsWith("L4_")) continue;
                Assert.That(text.rectTransform.sizeDelta.x, Is.GreaterThan(0f),
                    "Level 4 world TMP must have a bounded horizontal layout box.");
                Assert.That(text.enableWordWrapping, Is.False,
                    "Station labels should not wrap through their backing panels.");
                Assert.That(text.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
            }

            UnityEngine.Object.Destroy(host);
            UnityEngine.Object.Destroy(GameObject.Find("LEVEL4_ATTACK_VISUALS"));
            GameObject meterObject = meter != null ? meter.gameObject : null;
            if (meterObject != null) UnityEngine.Object.Destroy(meterObject);
            yield return null;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            Assert.Fail("Type not found: " + fullName);
            return null;
        }
    }
}
