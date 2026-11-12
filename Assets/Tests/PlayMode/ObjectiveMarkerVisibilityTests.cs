using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Cyverse.Tests
{
    /// <summary>The floating objective marker has to stay visible when level
    /// geometry (a header sign, a wall, a prop) stands between it and the
    /// player.</summary>
    public sealed class ObjectiveMarkerVisibilityTests
    {
        // Far from any level geometry the loaded scene may still hold.
        private static readonly Vector3 Origin = new Vector3(0f, 600f, 0f);

        private GameObject cameraObject, wall, target;
        private RenderTexture renderTexture;
        private Texture2D pixels;

        [TearDown]
        public void Cleanup()
        {
            RenderTexture.active = null;
            if (cameraObject != null) Object.Destroy(cameraObject);
            if (wall != null) Object.Destroy(wall);
            if (target != null) Object.Destroy(target);
            if (renderTexture != null) Object.Destroy(renderTexture);
            if (pixels != null) Object.Destroy(pixels);
            Type beaconType = FindType("Cyverse.Level.ObjectiveBeacon");
            var beacon = beaconType.GetProperty("Instance").GetValue(null) as Component;
            if (beacon != null) beaconType.GetMethod("Hide").Invoke(beacon, null);
        }

        [UnityTest]
        public IEnumerator Marker_HasSeeThroughPassInItsColour()
        {
            Component beacon = PointBeaconAtTarget(new Color(0.94f, 0.33f, 0.15f));
            yield return null;

            Type beaconType = beacon.GetType();
            var ghost = beaconType.GetProperty("GhostRenderer").GetValue(beacon) as Renderer;
            Assert.That(ghost, Is.Not.Null,
                "The marker needs its see-through copy (is Cyverse/ObjectiveMarkerGhost in Resources/Shaders?).");
            Assert.That(ghost.sharedMaterial.shader.name, Is.EqualTo("Cyverse/ObjectiveMarkerGhost"));
            Assert.That(ghost.gameObject.activeInHierarchy, Is.True,
                "The see-through copy shows whenever the marker does.");
            Assert.That(ghost.sharedMaterial.GetColor("_Color").g, Is.EqualTo(0.33f).Within(0.01f),
                "The see-through copy takes the level's guide colour.");

            beaconType.GetMethod("Hide").Invoke(beacon, null);
            Assert.That(ghost.gameObject.activeInHierarchy, Is.False,
                "No marker, no see-through copy.");
        }

        [UnityTest]
        public IEnumerator Marker_ShowsThroughAWallInFrontOfIt()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Needs a graphics device to render.");

            Component beacon = PointBeaconAtTarget(new Color(0.90f, 0.66f, 0.14f));
            var ghost = beacon.GetType().GetProperty("GhostRenderer").GetValue(beacon) as Renderer;
            Assert.That(ghost, Is.Not.Null);

            // The camera looks straight at the marker, 10 m away...
            Vector3 marker = (Vector3)beacon.GetType().GetProperty("MarkerPosition").GetValue(beacon);
            cameraObject = new GameObject("MarkerTestCamera");
            cameraObject.transform.position = new Vector3(marker.x, marker.y, marker.z - 10f);
            cameraObject.transform.rotation = Quaternion.identity;
            var cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 20f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.allowHDR = false;
            cam.enabled = false;
            renderTexture = new RenderTexture(256, 256, 24);
            cam.targetTexture = renderTexture;
            pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);

            Assert.That(CountMarkerPixels(cam), Is.GreaterThan(20),
                "Sanity: with nothing in the way the marker is on screen.");

            // ...and an opaque wall stands halfway.
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "MarkerTestWall";
            wall.transform.position = new Vector3(marker.x, marker.y, marker.z - 5f);
            wall.transform.localScale = new Vector3(8f, 8f, 0.2f);
            var wallMaterial = new Material(Shader.Find("Unlit/Color"));
            wallMaterial.color = new Color(0.05f, 0.07f, 0.16f);
            wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            yield return null;

            ghost.enabled = false;
            Assert.That(CountMarkerPixels(cam), Is.EqualTo(0),
                "Sanity: the wall really covers the ordinary marker.");

            ghost.enabled = true;
            Assert.That(CountMarkerPixels(cam), Is.GreaterThan(20),
                "The marker must show through geometry in front of it.");
            Object.Destroy(wallMaterial);
        }

        private Component PointBeaconAtTarget(Color colour)
        {
            FindType("Cyverse.Core.HardMode").GetMethod("End").Invoke(null, null);
            target = new GameObject("MarkerTestTarget");
            target.transform.position = Origin;

            Type beaconType = FindType("Cyverse.Level.ObjectiveBeacon");
            var beacon = (Component)beaconType.GetMethod("Ensure").Invoke(null, null);
            beaconType.GetMethod("PointAt").Invoke(beacon,
                new object[] { target.transform, "TEST", colour, 2.9f });
            return beacon;
        }

        /// <summary>Renders the camera and counts warm yellow/orange pixels.</summary>
        private int CountMarkerPixels(Camera cam)
        {
            cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            pixels.ReadPixels(new Rect(0f, 0f, 256f, 256f), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;

            int count = 0;
            foreach (Color32 p in pixels.GetPixels32())
                if (p.r > 110 && p.g > 60 && p.r > p.b + 70) count++;
            return count;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            throw new InvalidOperationException("Type not found: " + fullName);
        }
    }
}
