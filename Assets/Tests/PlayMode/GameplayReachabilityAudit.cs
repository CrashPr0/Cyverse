using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Cyverse.Tests
{
    /// <summary>
    /// Opt-in walk-and-aim audit of every playable scene. For each scene it
    /// flood-fills where the player's capsule can actually stand (once with
    /// the briefing door shut, once with it open), then checks every
    /// interactable: is there a spot the player can stand on from which the
    /// interact ray (eye height, 3.5 m, first collider hit wins, triggers
    /// included) lands on it? Writes a text report, a walkability map and a
    /// top-down render per scene. Enable with CYVERSE_GAMEPLAY_AUDIT=1; it is
    /// a no-op otherwise, so the normal suite does not pay for it.
    /// </summary>
    public sealed class GameplayReachabilityAudit
    {
        private static readonly string[] Scenes =
        {
            "Hub", "Level0", "Level1_IAM", "Level2_CyberDefense", "Level3_Forensics", "Level4_CyberAttack",
        };

        // Player capsule and interact ray, as in BuildKit.BuildPlayer / PlayerInteractor.
        private const float Radius = 0.4f, Height = 2f, StepUp = 0.3f, Eye = 1.7f, Range = 3.5f;
        private const float Cell = 0.25f, Extent = 33f;
        private const int N = (int)(2f * Extent / Cell);

        private Type interactableType;

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator WalkAndAimAtEveryInteractable()
        {
            if (Environment.GetEnvironmentVariable("CYVERSE_GAMEPLAY_AUDIT") != "1")
                yield break;

            string output = Environment.GetEnvironmentVariable("CYVERSE_GAMEPLAY_AUDIT_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "GameplayAudit");
            Directory.CreateDirectory(output);

            interactableType = FindType("Cyverse.Interaction.IInteractable");
            Assert.That(interactableType, Is.Not.Null, "IInteractable not found.");

            var report = new StringBuilder();
            LogAssert.ignoreFailingMessages = true;

            foreach (string sceneName in Scenes)
            {
                if (!Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    report.AppendLine($"## {sceneName}: NOT IN BUILD SETTINGS");
                    continue;
                }
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                for (int i = 0; i < 45; i++) yield return null; // bootstraps, polish passes, gravity

                try
                {
                    AuditScene(sceneName, output, report);
                }
                catch (Exception e)
                {
                    report.AppendLine($"## {sceneName}: AUDIT FAILED {e}");
                }
                File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
                yield return null;
            }

            File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
            Debug.Log("[GAMEPLAY AUDIT] wrote " + output);
        }

        private void AuditScene(string sceneName, string output, StringBuilder report)
        {
            report.AppendLine($"## {sceneName}");

            CharacterController cc = UnityEngine.Object.FindObjectOfType<CharacterController>();
            if (cc == null)
            {
                report.AppendLine("NO PLAYER (CharacterController) IN SCENE");
                return;
            }
            Vector3 spawn = cc.transform.position;
            report.AppendLine($"player at {V(spawn)} radius {cc.radius} height {cc.height} step {cc.stepOffset} grounded {cc.isGrounded}");
            Camera cam = Camera.main;
            if (cam != null)
                report.AppendLine($"camera local y {cam.transform.localPosition.y:0.00}");
            cc.enabled = false; // its capsule must not block its own audit

            // Pass A: as loaded. Pass B: briefing doors open.
            float[,] groundA = Flood(spawn);
            var doorPanels = new List<Collider>();
            foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb.GetType().Name != "LockedDoor") continue;
                Transform panel = mb.transform.Find("Panel");
                Collider c = panel != null ? panel.GetComponent<Collider>() : null;
                if (c != null && c.enabled) { c.enabled = false; doorPanels.Add(c); }
                report.AppendLine($"locked door at {V(mb.transform.position)} panel collider {(c != null ? "found" : "MISSING")}");
            }
            Physics.SyncTransforms();
            float[,] groundB = Flood(spawn);

            int cellsA = Count(groundA), cellsB = Count(groundB);
            report.AppendLine($"walkable area: start room {cellsA * Cell * Cell:0.0} m2, with doors open {cellsB * Cell * Cell:0.0} m2");
            report.AppendLine($"walkable bounds (doors open): {BoundsOf(groundB)}");

            // Interactables.
            var rows = new List<string>();
            var marks = new List<KeyValuePair<Vector3, Color>>();
            foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (!interactableType.IsAssignableFrom(mb.GetType()) || !mb.isActiveAndEnabled) continue;
                string prompt = SafeProp(mb, "Prompt");
                string can = SafeProp(mb, "CanInteract");

                var own = new List<Collider>();
                foreach (Collider c in mb.GetComponentsInChildren<Collider>())
                    if (c.enabled && c.gameObject.activeInHierarchy && Owner(c) == mb) own.Add(c);

                string line = $"{mb.GetType().Name} | {mb.name} | {V(mb.transform.position)} | prompt \"{prompt}\" | canInteract {can}";
                if (own.Count == 0)
                {
                    rows.Add("!! " + line + " | NO COLLIDER OF ITS OWN: cannot be aimed at");
                    marks.Add(new KeyValuePair<Vector3, Color>(mb.transform.position, Color.red));
                    continue;
                }
                own.Sort((a, b) => b.bounds.size.sqrMagnitude.CompareTo(a.bounds.size.sqrMagnitude));
                if (own.Count > 10) own.RemoveRange(10, own.Count - 10);
                Bounds all = own[0].bounds;
                foreach (Collider c in own) all.Encapsulate(c.bounds);

                var blockers = new Dictionary<string, int>();
                float nearestStand;
                int aimA = AimCells(mb, own, all, groundA, null, out _);
                float bestAim;
                int aimB = AimCells(mb, own, all, groundB, blockers, out bestAim);
                nearestStand = NearestStand(all, groundB);

                string flag = aimB == 0 ? "!! " : aimB < 16 ? "!  " : "   ";
                line += $" | aim spots start-room {aimA} / doors-open {aimB} ({aimB * Cell * Cell:0.0} m2)" +
                        $" | closest aim {bestAim:0.00} m | nearest standable {nearestStand:0.00} m" +
                        $" | colliders {own.Count} (triggers {CountTriggers(own)}) | size {V(all.size)}";
                if (aimB < 16) line += " | blocked by: " + Top(blockers, 4);
                rows.Add(flag + line);
                marks.Add(new KeyValuePair<Vector3, Color>(all.center,
                    aimB == 0 ? Color.red : aimB < 16 ? Color.yellow : Color.white));
            }
            rows.Sort(StringComparer.Ordinal);
            foreach (string row in rows) report.AppendLine(row);

            foreach (Collider c in doorPanels) c.enabled = true;

            WriteMap(Path.Combine(output, sceneName + "__walkable.png"), groundA, groundB, marks, spawn);
            try
            {
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    WriteTopDown(Path.Combine(output, sceneName + "__topdown.png"));
            }
            catch (Exception e)
            {
                report.AppendLine("top-down render failed: " + e.Message);
            }
            report.AppendLine();
        }

        // ---- Walkability ---------------------------------------------------------

        private static float[,] Flood(Vector3 spawn)
        {
            var ground = new float[N, N];
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                    ground[x, z] = float.NaN;

            float startY = spawn.y;
            if (Physics.Raycast(spawn + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor, 6f, ~0,
                    QueryTriggerInteraction.Ignore))
                startY = floor.point.y;

            int sx = Mathf.Clamp(Mathf.RoundToInt((spawn.x + Extent) / Cell), 0, N - 1);
            int sz = Mathf.Clamp(Mathf.RoundToInt((spawn.z + Extent) / Cell), 0, N - 1);
            var queue = new Queue<int>();
            // The spawn cell itself may be a little tight; look around it.
            for (int r = 0; r <= 4 && queue.Count == 0; r++)
                for (int dx = -r; dx <= r && queue.Count == 0; dx++)
                    for (int dz = -r; dz <= r && queue.Count == 0; dz++)
                    {
                        int x = sx + dx, z = sz + dz;
                        if (x < 0 || z < 0 || x >= N || z >= N) continue;
                        if (!CanStand(World(x, z, startY))) continue;
                        ground[x, z] = startY;
                        queue.Enqueue(x * N + z);
                    }

            int[] ox = { 1, -1, 0, 0 }, oz = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                int id = queue.Dequeue();
                int x = id / N, z = id % N;
                float y = ground[x, z];
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + ox[k], nz = z + oz[k];
                    if (nx < 0 || nz < 0 || nx >= N || nz >= N || !float.IsNaN(ground[nx, nz])) continue;
                    Vector3 probe = World(nx, nz, y + 0.9f);
                    if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 1.4f, ~0,
                            QueryTriggerInteraction.Ignore)) continue;
                    float ny = hit.point.y;
                    if (ny > y + StepUp + 0.02f || ny < y - 0.5f) continue;
                    if (!CanStand(World(nx, nz, ny))) continue;
                    ground[nx, nz] = ny;
                    queue.Enqueue(nx * N + nz);
                }
            }
            return ground;
        }

        private static bool CanStand(Vector3 feet)
        {
            Vector3 bottom = feet + Vector3.up * (Radius + StepUp + 0.02f);
            Vector3 top = feet + Vector3.up * (Height - Radius);
            return !Physics.CheckCapsule(bottom, top, Radius - 0.03f, ~0, QueryTriggerInteraction.Ignore);
        }

        private static Vector3 World(int x, int z, float y) =>
            new Vector3(x * Cell - Extent, y, z * Cell - Extent);

        private static int Count(float[,] ground)
        {
            int n = 0;
            foreach (float y in ground) if (!float.IsNaN(y)) n++;
            return n;
        }

        private static string BoundsOf(float[,] ground)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                {
                    if (float.IsNaN(ground[x, z])) continue;
                    Vector3 p = World(x, z, 0f);
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                }
            return $"x {minX:0.0}..{maxX:0.0}, z {minZ:0.0}..{maxZ:0.0}";
        }

        // ---- Aiming ----------------------------------------------------------------

        private Component Owner(Collider c) => c.GetComponentInParent(interactableType);

        private int AimCells(MonoBehaviour target, List<Collider> own, Bounds all, float[,] ground,
            Dictionary<string, int> blockers, out float bestDistance)
        {
            bestDistance = float.PositiveInfinity;
            int cells = 0;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((all.min.x - Range + Extent) / Cell));
            int x1 = Mathf.Min(N - 1, Mathf.CeilToInt((all.max.x + Range + Extent) / Cell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((all.min.z - Range + Extent) / Cell));
            int z1 = Mathf.Min(N - 1, Mathf.CeilToInt((all.max.z + Range + Extent) / Cell));
            float[] f = { 0f, -0.8f, 0.8f, -0.4f, 0.4f };

            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    float y = ground[x, z];
                    if (float.IsNaN(y)) continue;
                    Vector3 eye = World(x, z, y + Eye);
                    if ((all.ClosestPoint(eye) - eye).magnitude > Range) continue;

                    bool hitIt = false;
                    string firstBlocker = null;
                    foreach (Collider c in own)
                    {
                        Bounds b = c.bounds;
                        for (int i = 0; i < f.Length && !hitIt; i++)
                            for (int j = 0; j < f.Length && !hitIt; j++)
                                for (int k = 0; k < 3 && !hitIt; k++)
                                {
                                    Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3(f[i], f[j], f[k]));
                                    Vector3 dir = p - eye;
                                    float d = dir.magnitude;
                                    if (d < 0.05f || d > Range) continue;
                                    if (Mathf.Abs(Mathf.Asin(dir.y / d)) * Mathf.Rad2Deg > 84f) continue;
                                    if (!Physics.Raycast(eye, dir / d, out RaycastHit hit, Range)) continue;
                                    if (Owner(hit.collider) == target)
                                    {
                                        hitIt = true;
                                        if (hit.distance < bestDistance) bestDistance = hit.distance;
                                    }
                                    else if (firstBlocker == null)
                                    {
                                        firstBlocker = PathOf(hit.collider.transform) +
                                            (hit.collider.isTrigger ? " [trigger]" : "");
                                    }
                                }
                        if (hitIt) break;
                    }
                    if (hitIt) cells++;
                    else if (blockers != null && firstBlocker != null)
                    {
                        blockers.TryGetValue(firstBlocker, out int n);
                        blockers[firstBlocker] = n + 1;
                    }
                }
            return cells;
        }

        private static float NearestStand(Bounds all, float[,] ground)
        {
            float best = float.PositiveInfinity;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((all.min.x - 8f + Extent) / Cell));
            int x1 = Mathf.Min(N - 1, Mathf.CeilToInt((all.max.x + 8f + Extent) / Cell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((all.min.z - 8f + Extent) / Cell));
            int z1 = Mathf.Min(N - 1, Mathf.CeilToInt((all.max.z + 8f + Extent) / Cell));
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    float y = ground[x, z];
                    if (float.IsNaN(y)) continue;
                    Vector3 eye = World(x, z, y + Eye);
                    float d = (all.ClosestPoint(eye) - eye).magnitude;
                    if (d < best) best = d;
                }
            return best;
        }

        private static int CountTriggers(List<Collider> colliders)
        {
            int n = 0;
            foreach (Collider c in colliders) if (c.isTrigger) n++;
            return n;
        }

        private static string Top(Dictionary<string, int> counts, int take)
        {
            var list = new List<KeyValuePair<string, int>>(counts);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count && i < take; i++)
                sb.Append(i == 0 ? "" : "; ").Append(list[i].Key).Append(" x").Append(list[i].Value);
            return sb.Length == 0 ? "(nothing in range)" : sb.ToString();
        }

        // ---- Output ----------------------------------------------------------------

        private static void WriteMap(string path, float[,] groundA, float[,] groundB,
            List<KeyValuePair<Vector3, Color>> marks, Vector3 spawn)
        {
            const int scale = 4;
            var tex = new Texture2D(N * scale, N * scale, TextureFormat.RGB24, false);
            var pixels = new Color32[N * scale * N * scale];
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                {
                    Color32 c = !float.IsNaN(groundA[x, z]) ? new Color32(70, 150, 90, 255)
                        : !float.IsNaN(groundB[x, z]) ? new Color32(60, 100, 170, 255)
                        : new Color32(18, 18, 22, 255);
                    for (int px = 0; px < scale; px++)
                        for (int pz = 0; pz < scale; pz++)
                            pixels[(z * scale + pz) * N * scale + x * scale + px] = c;
                }
            foreach (var mark in marks) Dot(pixels, N * scale, mark.Key, mark.Value, scale, 5);
            Dot(pixels, N * scale, spawn, new Color(1f, 0f, 1f), scale, 6);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        private static void Dot(Color32[] pixels, int size, Vector3 world, Color color, int scale, int half)
        {
            int cx = Mathf.RoundToInt((world.x + Extent) / Cell * scale);
            int cz = Mathf.RoundToInt((world.z + Extent) / Cell * scale);
            for (int x = cx - half; x <= cx + half; x++)
                for (int z = cz - half; z <= cz + half; z++)
                {
                    if (x < 0 || z < 0 || x >= size || z >= size) continue;
                    pixels[z * size + x] = color;
                }
        }

        private static void WriteTopDown(string path)
        {
            const int size = 1056;
            var go = new GameObject("AuditTopDownCamera");
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Extent;
            camera.aspect = 1f;
            camera.transform.position = new Vector3(0f, 4.4f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 12f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var rt = new RenderTexture(size, size, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(go);
        }

        // ---- Helpers -----------------------------------------------------------------

        private static string SafeProp(object target, string name)
        {
            try
            {
                var prop = target.GetType().GetProperty(name);
                object value = prop != null ? prop.GetValue(target) : null;
                return value != null ? value.ToString().Replace("\n", " ") : "";
            }
            catch (Exception e)
            {
                return "(threw " + e.GetType().Name + ")";
            }
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            int depth = 0;
            while (t.parent != null && depth < 2)
            {
                t = t.parent;
                path = t.name + "/" + path;
                depth++;
            }
            return path;
        }

        private static string V(Vector3 v) => $"({v.x:0.0}, {v.y:0.0}, {v.z:0.0})";

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }
    }
}
