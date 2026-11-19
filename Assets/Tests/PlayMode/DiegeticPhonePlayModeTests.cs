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
    /// The Level 1 MFA vault's in-world authenticator phone (DiegeticPhone):
    /// it should stand upright beside the passcode terminal, be phone-sized,
    /// face the player, and show the code on its screen.
    ///
    /// The geometry test always runs. The capture test is opt-in: set
    /// CYVERSE_PHONE_CAPTURE=1 (and optionally CYVERSE_PHONE_CAPTURE_OUTPUT)
    /// to write PNGs of the phone, lit with a code, from the player's views.
    /// </summary>
    public class DiegeticPhonePlayModeTests
    {
        private const string SampleCode = "4829";

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator Phone_StandsUprightAndPhoneSizedBesideTheTerminal()
        {
            Component phone = null;
            yield return LoadLevel1Phone(c => phone = c);
            Assert.That(phone, Is.Not.Null, "Level 1 should build the MFA authenticator phone.");

            Transform body = phone.transform.Find("Body");
            Assert.That(body, Is.Not.Null);
            Assert.That(Vector3.Dot(body.up, Vector3.up), Is.GreaterThan(0.995f), "The phone should stand upright.");

            Renderer screen = body.Find("Screen").GetComponent<Renderer>();
            Vector3 size = screen.bounds.size;
            Assert.That(size.x, Is.InRange(0.12f, 0.24f), "Screen width should be phone-sized.");
            Assert.That(size.y, Is.InRange(0.25f, 0.45f), "Screen height should be phone-sized.");
            Assert.That(screen.bounds.center.y, Is.InRange(0.9f, 1.6f), "Screen should sit near eye-to-hand height.");

            // A Quad draws on its local -Z face, so the screen faces the player
            // who walks up from -Z when its forward points +Z.
            Assert.That(Vector3.Dot(screen.transform.forward, Vector3.forward), Is.GreaterThan(0.99f),
                "The screen should face the player at the terminal.");
            Material material = screen.sharedMaterial;
            Assert.That(material, Is.Not.Null);
            Assert.That(material.mainTexture, Is.InstanceOf<RenderTexture>(), "The screen should show the phone UI render texture.");

            Type phoneType = phone.GetType();
            phoneType.GetMethod("ShowCode").Invoke(phone, new object[] { SampleCode });
            Assert.That(screen.enabled, Is.True, "Showing a code should light the screen.");
            object codeText = phoneType
                .GetField("codeText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(phone);
            Assert.That(codeText.GetType().GetProperty("text").GetValue(codeText), Is.EqualTo(SampleCode));
            phoneType.GetMethod("Hide").Invoke(phone, null);
            Assert.That(screen.enabled, Is.False, "Hide should darken the screen.");
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator Nameplate_FitsTheDockFront_OnADarkPlate()
        {
            Component phone = null;
            yield return LoadLevel1Phone(c => phone = c);
            Assert.That(phone, Is.Not.Null);

            Transform label = phone.transform.Find("Label_MY_PHONE");
            Transform plate = phone.transform.Find("NameplateBack");
            Assert.That(label, Is.Not.Null, "The dock should carry a MY PHONE nameplate.");
            Assert.That(plate, Is.Not.Null, "The nameplate should sit on a dark backing plate.");

            Vector3 right = phone.transform.right, forward = phone.transform.forward;
            Bounds text = label.GetComponent<Renderer>().bounds;
            Bounds back = plate.GetComponent<Renderer>().bounds;
            float textWidth = 2f * Mathf.Abs(Vector3.Dot(text.extents, right));
            float plateWidth = 2f * Mathf.Abs(Vector3.Dot(back.extents, right));
            Assert.That(textWidth, Is.GreaterThan(0.05f), "The nameplate text should have readable width.");
            // The dock's flat front between its bevels is about 23 cm wide.
            Assert.That(plateWidth, Is.LessThanOrEqualTo(0.23f), "The plate should fit the dock's flat front.");
            Assert.That(textWidth, Is.LessThanOrEqualTo(plateWidth),
                $"MY PHONE ({textWidth:0.000} m) should fit on its plate ({plateWidth:0.000} m), not overhang the dock.");

            // The player stands on the phone's -Z side: the text must be in front of the plate.
            float textFront = Vector3.Dot(text.center - phone.transform.position, forward);
            float plateFront = Vector3.Dot(back.center - phone.transform.position, forward) -
                               Mathf.Abs(Vector3.Dot(back.extents, forward));
            Assert.That(textFront, Is.LessThan(plateFront + 0.001f), "The text should sit in front of its plate.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator CapturePhoneViews()
        {
            if (Environment.GetEnvironmentVariable("CYVERSE_PHONE_CAPTURE") != "1") yield break;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;

            string output = Environment.GetEnvironmentVariable("CYVERSE_PHONE_CAPTURE_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Captures", "diegetic-phone");
            Directory.CreateDirectory(output);

            Component phone = null;
            yield return LoadLevel1Phone(c => phone = c);
            Assert.That(phone, Is.Not.Null);
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                string name = behaviour.GetType().Name;
                if (name == "FirstPersonController" || name == "PlayerInteractor") behaviour.enabled = false;
            }
            foreach (Renderer r in camera.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.03f;

            Renderer screen = phone.transform.Find("Body/Screen").GetComponent<Renderer>();
            Vector3 s = screen.bounds.center;
            Vector3 terminal = phone.transform.position + new Vector3(0.9f, 0f, 0.2f);

            Shoot(camera, output, "0-dark-from-terminal", terminal + new Vector3(-0.3f, 1.6f, -1.2f), s);

            phone.GetType().GetMethod("ShowCode").Invoke(phone, new object[] { SampleCode });
            phone.GetType().GetMethod("SetStatus").Invoke(phone, new object[] { "<color=#8FB8CC>STARTER PASSCODE  ·  SPARTAN</color>" });
            yield return null;

            // Where the player stands to use the passcode terminal, looking at it.
            Shoot(camera, output, "1-player-at-terminal", terminal + new Vector3(0f, 1.6f, -1.3f), terminal + new Vector3(-0.3f, 1.1f, 0f));
            // Turned toward the phone from the same spot.
            Shoot(camera, output, "2-player-turns-to-phone", terminal + new Vector3(0f, 1.6f, -1.3f), s);
            // Walked up to the phone.
            Shoot(camera, output, "3-at-phone", s + new Vector3(0f, 0.4f, -0.9f), s);
            Shoot(camera, output, "4-close", s + new Vector3(0f, 0.05f, -0.45f), s);
            Shoot(camera, output, "5-side", s + new Vector3(-0.8f, 0.2f, -0.6f), s);
            Shoot(camera, output, "6-wide", s + new Vector3(1.2f, 1.0f, -3.2f), s);

            // The render texture itself, as the phone UI drew it.
            var rt = (RenderTexture)screen.sharedMaterial.mainTexture;
            WriteRt(rt, Path.Combine(output, "7-screen-texture.png"));
            Debug.Log("[PHONE CAPTURE] wrote views to " + output);
        }

        private static IEnumerator LoadLevel1Phone(Action<Component> found)
        {
            SceneManager.LoadScene("Level1_IAM", LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
            Type phoneType = FindType("Cyverse.Interaction.DiegeticPhone");
            Assert.That(phoneType, Is.Not.Null);
            found((Component)UnityEngine.Object.FindObjectOfType(phoneType));
        }

        private static void Shoot(Camera camera, string dir, string name, Vector3 from, Vector3 at)
        {
            camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
            WriteRt(target, Path.Combine(dir, name + ".png"));
            target.Release();
            UnityEngine.Object.Destroy(target);
        }

        private static void WriteRt(RenderTexture rt, string path)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var pixels = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            UnityEngine.Object.Destroy(pixels);
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
