using System.Collections.Generic;
using UnityEngine;
using Cyverse.Settings;

namespace Cyverse.Level
{
    /// <summary>
    /// Breadcrumbs: glowing motes that flow along the floor from the player
    /// toward the current objective, around walls and furniture, while the
    /// guide drone is out pointing (and briefly whenever the objective
    /// changes). The route is a coarse grid search over the room's colliders,
    /// recomputed as the player moves. Purely visual.
    /// </summary>
    public class GuideTrail : MonoBehaviour
    {
        // The shared 40 x 40 m level footprint, in half-metre cells.
        private const float Cell = 0.5f, Extent = 19.75f;
        private static readonly int Size = Mathf.CeilToInt(Extent * 2f / Cell);
        private const float ShowAfterNewObjective = 4f;
        // The trail is the first hint: it comes up after the objective has
        // been out of sight this long; the drone follows much later.
        private const float ShowAfterHidden = 12f;
        private const float TrailLength = 9f;
        private static readonly Color Guide = new Color(0.90f, 0.66f, 0.14f);

        public static GuideTrail Instance { get; private set; }

        private ParticleSystem motes;
        private readonly List<Vector3> path = new List<Vector3>();
        private float pathLength;
        private sbyte[] blocked;          // 0 unknown, 1 free, 2 blocked
        private float blockedAge;
        private Transform lastTarget;
        private Vector3 lastFrom;
        private float lastSearch, targetChangedAt = -99f, strength, emitCarry;

        public static GuideTrail Ensure()
        {
            var existing = FindObjectOfType<GuideTrail>();
            if (existing != null) return existing;
            var trail = new GameObject("GuideTrail").AddComponent<GuideTrail>();
            trail.BuildParticles();
            return trail;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>A point a few metres along the walking route to the
        /// objective, at chest height — where the drone points when the
        /// objective itself is behind a wall. False with no route.</summary>
        public bool TryGetHeading(Vector3 from, out Vector3 point)
        {
            point = default;
            if (path.Count < 2 || lastTarget == null) return false;
            // Measure from the point on the route nearest the player.
            float best = float.MaxValue, at = 0f, walked = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 a = path[i - 1], b = path[i];
                float seg = Vector3.Distance(a, b);
                Vector3 flat = new Vector3(from.x, 0f, from.z);
                float t = seg > 0f ? Mathf.Clamp01(Vector3.Dot(flat - a, b - a) / (seg * seg)) : 0f;
                float d = (Vector3.Lerp(a, b, t) - flat).sqrMagnitude;
                if (d < best) { best = d; at = walked + t * seg; }
                walked += seg;
            }
            point = Along(Mathf.Min(at + 3.5f, pathLength), out _) + Vector3.up * 1.4f;
            return true;
        }

        void LateUpdate()
        {
            if (motes == null) return;
            Camera cam = Camera.main;
            ObjectiveBeacon beacon = ObjectiveBeacon.Instance;
            Transform target = beacon != null && beacon.MarkerShown ? beacon.Target : null;

            if (target != lastTarget)
            {
                lastTarget = target;
                targetChangedAt = Time.time;
                path.Clear();
                blocked = null; // doors may have opened since the last objective
            }

            bool menuUp = Core.GameState.Busy && !Core.GameState.DialogueActive;
            GuideDrone drone = GuideDrone.Instance;
            bool want = target != null && cam != null && !menuUp &&
                        ((drone != null && drone.MarkerHiddenFor > ShowAfterHidden) ||
                         Time.time - targetChangedAt < ShowAfterNewObjective);
            strength = Mathf.MoveTowards(strength, want ? 1f : 0f, Time.deltaTime * 2.5f);
            if (strength <= 0f || target == null || cam == null) return;

            Vector3 feet = cam.transform.root.position;
            bool moved = (feet - lastFrom).sqrMagnitude > 0.5f;
            if (Time.time - lastSearch > (path.Count == 0 ? 0.6f : 0.35f) && (path.Count == 0 || moved))
                Search(feet, target);
            if (path.Count < 2) return;
            Emit();
        }

        // ---- Route ------------------------------------------------------------

        private void Search(Vector3 from, Transform target)
        {
            lastSearch = Time.time;
            lastFrom = from;
            if (blocked == null || Time.time - blockedAge > 6f)
            {
                blocked = new sbyte[Size * Size];
                blockedAge = Time.time;
            }

            int start = CellOf(from), goalX, goalZ;
            Vector3 goalPoint = target.position;
            Coords(CellOf(goalPoint), out goalX, out goalZ);
            if (start < 0) { path.Clear(); return; }

            // A* over 8-connected cells; the goal is any free cell near the
            // objective (the objective itself fills its own cells).
            var open = new List<int> { start };
            var came = new Dictionary<int, int>();
            var cost = new Dictionary<int, float> { [start] = 0f };
            var closed = new HashSet<int>();
            int found = -1, guard = 0;
            while (open.Count > 0 && guard++ < 7000)
            {
                int best = 0;
                float bestF = float.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    Coords(open[i], out int ox, out int oz);
                    float f = cost[open[i]] + Octile(ox, oz, goalX, goalZ);
                    if (f < bestF) { bestF = f; best = i; }
                }
                int current = open[best];
                open.RemoveAt(best);
                if (!closed.Add(current)) continue;

                Coords(current, out int cx, out int cz);
                if (Octile(cx, cz, goalX, goalZ) * Cell < 1.4f) { found = current; break; }

                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = cx + dx, nz = cz + dz;
                    if (nx < 0 || nz < 0 || nx >= Size || nz >= Size) continue;
                    int next = nx * Size + nz;
                    if (closed.Contains(next) || IsBlocked(nx, nz, target)) continue;
                    // No squeezing diagonally past a corner.
                    if (dx != 0 && dz != 0 && (IsBlocked(cx + dx, cz, target) || IsBlocked(cx, cz + dz, target)))
                        continue;
                    float step = (dx != 0 && dz != 0) ? 1.414f : 1f;
                    float g = cost[current] + step;
                    if (cost.TryGetValue(next, out float old) && old <= g) continue;
                    cost[next] = g;
                    came[next] = current;
                    open.Add(next);
                }
            }

            path.Clear();
            if (found < 0) return;
            var cells = new List<int>();
            for (int c = found; ; c = came[c])
            {
                cells.Add(c);
                if (!came.ContainsKey(c)) break;
            }
            cells.Reverse();
            path.Add(new Vector3(from.x, 0f, from.z));
            foreach (int c in cells) path.Add(Centre(c));
            path.Add(new Vector3(goalPoint.x, 0f, goalPoint.z));
            Smooth();
            pathLength = 0f;
            for (int i = 1; i < path.Count; i++) pathLength += Vector3.Distance(path[i - 1], path[i]);
        }

        /// <summary>Drops points the route can skip in a straight, clear line.</summary>
        private void Smooth()
        {
            var kept = new List<Vector3> { path[0] };
            int anchor = 0;
            for (int i = 2; i < path.Count; i++)
                if (!ClearLine(path[anchor], path[i])) { kept.Add(path[i - 1]); anchor = i - 1; }
            kept.Add(path[path.Count - 1]);
            path.Clear();
            path.AddRange(kept);
        }

        private bool ClearLine(Vector3 a, Vector3 b)
        {
            float d = Vector3.Distance(a, b);
            for (float t = 0f; t <= d; t += Cell * 0.5f)
            {
                Vector3 p = Vector3.Lerp(a, b, d > 0f ? t / d : 0f);
                int c = CellOf(p);
                if (c < 0) return false;
                Coords(c, out int x, out int z);
                if (IsBlocked(x, z, lastTarget)) return false;
            }
            return true;
        }

        private bool IsBlocked(int x, int z, Transform target)
        {
            if (x < 0 || z < 0 || x >= Size || z >= Size) return true;
            int i = x * Size + z;
            if (blocked[i] == 0)
            {
                Vector3 c = Centre(i) + Vector3.up * 1.0f;
                bool hit = false;
                foreach (Collider col in Physics.OverlapBox(c, new Vector3(0.22f, 0.75f, 0.22f),
                             Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (col is CharacterController) continue;
                    if (target != null && col.transform.IsChildOf(target)) continue;
                    hit = true;
                    break;
                }
                blocked[i] = (sbyte)(hit ? 2 : 1);
            }
            return blocked[i] == 2;
        }

        private static int CellOf(Vector3 p)
        {
            int x = Mathf.FloorToInt((p.x + Extent) / Cell), z = Mathf.FloorToInt((p.z + Extent) / Cell);
            return x < 0 || z < 0 || x >= Size || z >= Size ? -1 : x * Size + z;
        }

        private static void Coords(int i, out int x, out int z)
        {
            x = i / Size;
            z = i % Size;
        }

        private static Vector3 Centre(int i)
        {
            Coords(i, out int x, out int z);
            return new Vector3(-Extent + (x + 0.5f) * Cell, 0f, -Extent + (z + 0.5f) * Cell);
        }

        private static float Octile(int ax, int az, int bx, int bz)
        {
            int dx = Mathf.Abs(ax - bx), dz = Mathf.Abs(az - bz);
            return Mathf.Max(dx, dz) + 0.414f * Mathf.Min(dx, dz);
        }

        // ---- Motes ------------------------------------------------------------

        private void Emit()
        {
            bool still = AccessibilitySettings.ReduceMotion;
            float length = Mathf.Min(pathLength, TrailLength);
            float rate = (still ? 24f : 70f) * strength * Mathf.Clamp01(length / 3f);
            emitCarry += rate * Time.deltaTime;
            var p = new ParticleSystem.EmitParams();
            while (emitCarry >= 1f)
            {
                emitCarry -= 1f;
                float s = Random.Range(0.6f, length);
                Vector3 at = Along(s, out Vector3 dir);
                // Fade toward the far end, so the trail reads as heading off.
                float fade = 1f - Mathf.Clamp01((s - length * 0.6f) / (length * 0.4f));
                Color c = Guide;
                c.a = fade * strength;
                p.position = at + new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(0.06f, 0.16f), Random.Range(-0.08f, 0.08f));
                p.velocity = still ? Vector3.up * 0.05f : dir * 1.6f + Vector3.up * Random.Range(0f, 0.12f);
                p.startColor = c;
                p.startSize = Random.Range(0.08f, 0.15f);
                p.startLifetime = Random.Range(0.55f, 0.85f);
                motes.Emit(p, 1);
            }
        }

        private Vector3 Along(float s, out Vector3 dir)
        {
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Vector3.Distance(path[i - 1], path[i]);
                if (s <= seg || i == path.Count - 1)
                {
                    dir = seg > 0f ? (path[i] - path[i - 1]) / seg : Vector3.forward;
                    return Vector3.Lerp(path[i - 1], path[i], seg > 0f ? Mathf.Clamp01(s / seg) : 0f);
                }
                s -= seg;
            }
            dir = Vector3.forward;
            return path[path.Count - 1];
        }

        private void BuildParticles()
        {
            motes = gameObject.AddComponent<ParticleSystem>();
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = motes.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.startSpeed = 0f;
            var emission = motes.emission;
            emission.enabled = false; // emitted by hand along the route
            var shape = motes.shape;
            shape.enabled = false;
            var fadeOut = motes.colorOverLifetime;
            fadeOut.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            fadeOut.color = g;
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.material = GlowMaterial();
            motes.Play();
        }

        /// <summary>The additive glow sprite material BurstFX uses.</summary>
        public static Material GlowMaterial()
        {
            Shader glow = Shader.Find("Cyverse/GlowSprite");
            var mat = new Material(glow != null ? glow : Shader.Find("Sprites/Default"));
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Intensity")) mat.SetFloat("_Intensity", 1.4f);
            return mat;
        }
    }
}
