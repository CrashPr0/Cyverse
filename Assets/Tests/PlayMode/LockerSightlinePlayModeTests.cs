using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Cyverse.Tests
{
    public sealed class LockerSightlinePlayModeTests
    {
        private static readonly string[] RosterKeys =
        {
            "cv_scenario_roster_index", "cv_scenario_roster_finished"
        };
        private const string EvidenceKey = "cv_soc_evidence_json";
        private readonly bool[] hadRosterKey = new bool[RosterKeys.Length];
        private readonly int[] savedRoster = new int[RosterKeys.Length];
        private bool hadEvidence, savedInitialized;
        private string savedEvidence;
        private int savedSessionIndex;

        [SetUp]
        public void PreserveProgress()
        {
            for (int i = 0; i < RosterKeys.Length; i++)
            {
                hadRosterKey[i] = PlayerPrefs.HasKey(RosterKeys[i]);
                savedRoster[i] = PlayerPrefs.GetInt(RosterKeys[i]);
            }
            hadEvidence = PlayerPrefs.HasKey(EvidenceKey);
            savedEvidence = PlayerPrefs.GetString(EvidenceKey, "");
            Type roster = FindType("Cyverse.Level.ScenarioRoster");
            savedInitialized = (bool)roster.GetField("sessionInitialized",
                BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            savedSessionIndex = (int)roster.GetField("sessionIndex",
                BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        }

        [TearDown]
        public void RestoreProgress()
        {
            for (int i = 0; i < RosterKeys.Length; i++)
            {
                if (hadRosterKey[i]) PlayerPrefs.SetInt(RosterKeys[i], savedRoster[i]);
                else PlayerPrefs.DeleteKey(RosterKeys[i]);
            }
            if (hadEvidence) PlayerPrefs.SetString(EvidenceKey, savedEvidence);
            else PlayerPrefs.DeleteKey(EvidenceKey);
            Type roster = FindType("Cyverse.Level.ScenarioRoster");
            roster.GetField("sessionInitialized", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, savedInitialized);
            roster.GetField("sessionIndex", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, savedSessionIndex);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator SavedLocker_StartRestoresSightlineAndKeepsWallCollision()
        {
            foreach (string scene in new[] { "Level2_CyberDefense", "Level3_Forensics" })
            {
                yield return Load(scene);
                Type lockerType = FindType("Cyverse.Interaction.EvidenceLocker");
                Type sightlineType = FindType("Cyverse.Level.LockerSightline");
                Component original = (Component)UnityEngine.Object.FindObjectOfType(lockerType);
                original.gameObject.SetActive(false);
                GameObject savedLocker = UnityEngine.Object.Instantiate(original.gameObject);
                savedLocker.name = original.name;
                Material legacyGlass = new Material(Shader.Find("Cyverse/Hologram"));
                savedLocker.transform.Find("Glass").GetComponent<Renderer>().sharedMaterial = legacyGlass;

                // A serialized locker keeps its model and Side, but an editor
                // build does not create the runtime-only window or replica.
                UnityEngine.Object.DestroyImmediate(savedLocker.GetComponent(sightlineType));
                for (int i = savedLocker.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = savedLocker.transform.GetChild(i);
                    if (child.name.StartsWith("Sleeve_", StringComparison.Ordinal) ||
                        child.name.StartsWith("FarFrame_", StringComparison.Ordinal) ||
                        child.name == "SightlineStandIn")
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                UnityEngine.Object.DestroyImmediate(original.gameObject);
                yield return null;

                string wallName = scene == "Level2_CyberDefense" ? "Wall_North" : "Wall_South";
                GameObject wall = GameObject.Find(wallName);
                wall.GetComponent<Renderer>().enabled = true;
                UnityEngine.Object.DestroyImmediate(GameObject.Find(wallName + "_Cut"));
                Collider wallCollider = wall.GetComponent<Collider>();
                savedLocker.SetActive(true);
                yield return null;
                yield return null;

                Component sightline = savedLocker.GetComponent(sightlineType);
                Assert.That(sightline, Is.Not.Null,
                    scene + " must recreate its window when the locker was saved by an editor builder.");
                GameObject replica = Replica(sightline);
                Assert.That(replica, Is.Not.Null);
                Assert.That(replica.GetComponentsInChildren<Collider>(true), Is.Empty,
                    "The adjacent room must remain scenery, with no physics or interaction targets.");
                foreach (Light fill in replica.GetComponentsInChildren<Light>(true))
                    Assert.That(Mathf.Abs(fill.transform.position.z) - fill.range, Is.GreaterThan(20f),
                        "Replica lighting must not spill through the wall into the playable room.");
                Assert.That(wall.GetComponent<Collider>(), Is.SameAs(wallCollider));
                Assert.That(wallCollider.enabled, Is.True, "Cutting the window must preserve the real wall barrier.");
                Assert.That(wall.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(savedLocker.transform.Find("Glass").GetComponent<Renderer>()
                    .sharedMaterial.shader.name, Is.EqualTo("Cyverse/GlassPane"),
                    "Saved lockers must upgrade their old hologram material to the clear pane.");
                UnityEngine.Object.DestroyImmediate(legacyGlass);

                // Initialization must also tolerate repeated scene realization.
                MethodInfo build = sightlineType.GetMethod("Build");
                object side = lockerType.GetProperty("side").GetValue(savedLocker.GetComponent(lockerType));
                build.Invoke(null, new[] { (object)savedLocker.transform, side });
                Assert.That(savedLocker.GetComponents(sightlineType).Length, Is.EqualTo(1));
                Assert.That(Replica(savedLocker.GetComponent(sightlineType)), Is.SameAs(replica));
            }
        }

        [UnityTest]
        public IEnumerator DistantWindowFallback_CoversObliqueRaysOnBothSides()
        {
            foreach (string scene in new[] { "Level2_CyberDefense", "Level3_Forensics" })
            {
                yield return Load(scene);
                Component locker = (Component)UnityEngine.Object.FindObjectOfType(
                    FindType("Cyverse.Interaction.EvidenceLocker"));
                Transform card = locker.transform.Find("SightlineStandIn");
                Assert.That(card, Is.Not.Null);

                // This ray clears the near pane, the sleeve and the far frame,
                // but misses the old card placed two metres behind the opening.
                Vector3 entry = locker.transform.TransformPoint(new Vector3(-0.4709f, 1.46f, -0.585f));
                Vector3 exit = locker.transform.TransformPoint(new Vector3(0.46f, 1.46f, 1.02f));
                Vector3 direction = (exit - entry).normalized;
                Ray ray = new Ray(entry - direction * 15f, direction);
                Camera camera = Camera.main;
                foreach (MonoBehaviour component in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
                    if (component.GetType().FullName == "Cyverse.Player.FirstPersonController") component.enabled = false;
                camera.transform.SetParent(null, true);
                camera.transform.SetPositionAndRotation(ray.origin, Quaternion.LookRotation(direction));
                yield return new WaitForSeconds(0.25f);

                Assert.That(card.gameObject.activeInHierarchy, Is.True,
                    "A distant viewer should see the fallback while the replica is culled.");
                var plane = new Plane(card.forward, card.position);
                Assert.That(plane.Raycast(ray, out float distance), Is.True);
                Vector3 hit = card.InverseTransformPoint(ray.GetPoint(distance));
                Assert.That(Mathf.Abs(hit.x), Is.LessThanOrEqualTo(0.5f),
                    scene + " fallback leaves a void at an oblique horizontal viewing angle.");
                Assert.That(Mathf.Abs(hit.y), Is.LessThanOrEqualTo(0.5f));
            }
        }

        [UnityTest]
        public IEnumerator LockerGlass_DoesNotProjectHologramGraphicsOverTheRoom()
        {
            yield return Load("Level2_CyberDefense");
            Component locker = (Component)UnityEngine.Object.FindObjectOfType(
                FindType("Cyverse.Interaction.EvidenceLocker"));
            Material glass = locker.transform.Find("Glass").GetComponent<Renderer>().sharedMaterial;
            Assert.That(glass.shader.name, Is.EqualTo("Cyverse/GlassPane"));
            Assert.That(glass.shader.isSupported, Is.True);
            Assert.That(glass.GetFloat("_Alpha"), Is.InRange(0.05f, 0.12f),
                "The pane should be visible without hiding the adjoining room.");
            Assert.That(glass.GetFloat("_ReflectionStrength"), Is.InRange(0.1f, 0.3f));
            foreach (string property in new[] { "_BarStrength", "_GridStrength", "_ScanStrength" })
            {
                Assert.That(glass.HasProperty(property), Is.False,
                    "Glass must not use animated display graphics: " + property);
            }
        }

        private static IEnumerator Load(string scene)
        {
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static GameObject Replica(Component sightline) =>
            (GameObject)sightline.GetType().GetField("replica", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(sightline);

        private static Type FindType(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(name, false);
                if (type != null) return type;
            }
            throw new InvalidOperationException("Missing runtime type " + name);
        }
    }
}
