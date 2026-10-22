using UnityEngine;
using Cyverse.Core;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// The Hub's west wing: an opening in the west wall into a side room
    /// holding the Hard copies of the levels. Built at load like the other Hub
    /// passes, so the saved Hub scene is never edited.
    ///
    /// The opening sits between the wall columns at z = -8 and z = 0. Every
    /// long, axis-aligned piece of the west wall (the wall itself, its
    /// skirting, floor trim and light strip) is split around it, and a lintel
    /// closes the top. The wing beyond runs x -31..-20.5, z -17..9, with one
    /// Hard door per story level on its far wall. A Hard door opens once that
    /// level is complete on Normal; levels without a Hard version yet show as
    /// in development.
    /// </summary>
    public static class HardWing
    {
        public static readonly Color HardRed = new Color(1f, 0.28f, 0.32f);

        // The opening: clear of the columns at z = -8 and 0 (0.5 m wide each).
        private const float GapMinZ = -6.4f, GapMaxZ = -1.6f;
        private const float LintelBottom = 4.2f;

        // The wing's inner floor area.
        private const float WestX = -30f, EastX = -20f;
        private const float SouthZ = -16f, NorthZ = 8f;

        private struct HardDoor
        {
            public readonly int level;
            public readonly string name, scene;
            public readonly float z;
            public HardDoor(int level, string name, string scene, float z)
            { this.level = level; this.name = name; this.scene = scene; this.z = z; }
        }

        /// <summary>Hard copies built so far carry a scene; the rest are in
        /// development until their Hard rules exist.</summary>
        private static HardDoor[] Doors() => new[]
        {
            new HardDoor(1, "Level 1 — Hard", "Level1_IAM", 5f),
            new HardDoor(2, "Level 2 — Hard", "", -1f),
            new HardDoor(3, "Level 3 — Hard", "", -7f),
            new HardDoor(4, "Level 4 — Hard", "", -13f),
        };

        public static void Build()
        {
            if (GameObject.Find("HardWing") != null) return;
            GameObject wall = GameObject.Find("Wall_West");
            if (wall == null) return; // not the saved Hub

            var root = new GameObject("HardWing").transform;

            Material wallMat = SharedMaterial(wall) ?? BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.12f), 0.4f, 0.2f);
            Material floorMat = SharedMaterial(GameObject.Find("Floor")) ?? wallMat;
            Material ceilingMat = SharedMaterial(GameObject.Find("CeilingSlab")) ?? wallMat;

            OpenWestWall(wallMat, root);
            BuildRoom(root, wallMat, floorMat, ceilingMat);
            BuildDoors(root);
            BuildEntranceSign(root);
        }

        // ---- The opening ----------------------------------------------------------

        private static void OpenWestWall(Material wallMat, Transform root)
        {
            foreach (string name in new[] { "Wall_West", "Base_W", "Trim_W", "Strip_W" })
                Carve(GameObject.Find(name));

            // Close the top of the opening at door height.
            float h = 5f - LintelBottom;
            BuildKit.Spawn(PrimitiveType.Cube, "HardWing_Lintel", root,
                new Vector3(-20f, LintelBottom + h * 0.5f, (GapMinZ + GapMaxZ) * 0.5f),
                new Vector3(1f, h, GapMaxZ - GapMinZ), wallMat, true);
        }

        /// <summary>Splits a root-level, unrotated box that runs along z into
        /// the parts south and north of the opening. Anything else is left
        /// alone.</summary>
        private static void Carve(GameObject go)
        {
            if (go == null) return;
            Transform t = go.transform;
            if (t.parent != null || Quaternion.Angle(t.rotation, Quaternion.identity) > 0.5f) return;

            Vector3 p = t.position, s = t.localScale;
            float z0 = p.z - s.z * 0.5f, z1 = p.z + s.z * 0.5f;
            if (GapMaxZ <= z0 || GapMinZ >= z1) return;

            if (z1 > GapMaxZ)
            {
                var north = Object.Instantiate(go);
                north.name = go.name + "_North";
                north.transform.position = new Vector3(p.x, p.y, (GapMaxZ + z1) * 0.5f);
                north.transform.localScale = new Vector3(s.x, s.y, z1 - GapMaxZ);
            }
            if (z0 < GapMinZ)
            {
                t.position = new Vector3(p.x, p.y, (z0 + GapMinZ) * 0.5f);
                t.localScale = new Vector3(s.x, s.y, GapMinZ - z0);
            }
            else
            {
                go.SetActive(false);
            }
        }

        // ---- The room ---------------------------------------------------------------

        private static void BuildRoom(Transform root, Material wallMat, Material floorMat, Material ceilingMat)
        {
            float cx = (WestX + EastX) * 0.5f, cz = (SouthZ + NorthZ) * 0.5f;
            float w = EastX - WestX, d = NorthZ - SouthZ;

            // The floor starts at the Hub floor's edge (x = -20) so the two meet
            // under the opening; its grid is in world space and lines up.
            BuildKit.Spawn(PrimitiveType.Cube, "HardWing_Floor", root,
                new Vector3(cx, -0.05f, cz), new Vector3(w, 0.1f, d + 2f), floorMat, true).isStatic = true;
            BuildKit.Spawn(PrimitiveType.Cube, "HardWing_Ceiling", root,
                new Vector3(cx, 5.15f, cz), new Vector3(w + 2f, 0.3f, d + 2f), ceilingMat, true).isStatic = true;

            BuildKit.Spawn(PrimitiveType.Cube, "HardWing_WallWest", root,
                new Vector3(WestX - 0.5f, 2.5f, cz), new Vector3(1f, 5f, d + 2f), wallMat, true).isStatic = true;
            BuildKit.Spawn(PrimitiveType.Cube, "HardWing_WallNorth", root,
                new Vector3(cx - 0.5f, 2.5f, NorthZ + 0.5f), new Vector3(w + 1f, 5f, 1f), wallMat, true).isStatic = true;
            BuildKit.Spawn(PrimitiveType.Cube, "HardWing_WallSouth", root,
                new Vector3(cx - 0.5f, 2.5f, SouthZ - 0.5f), new Vector3(w + 1f, 5f, 1f), wallMat, true).isStatic = true;

            // A red strip along the far wall reads as "different rules" from the Hub.
            Material strip = BuildKit.MakeEmissive(HardRed, 1.2f);
            BuildKit.Spawn(PrimitiveType.Cube, "HardWing_Strip", root,
                new Vector3(WestX + 0.02f, 3.4f, cz), new Vector3(0.06f, 0.08f, d - 0.4f), strip, false);

            foreach (float z in new[] { NorthZ - 6f, SouthZ + 6f })
            {
                var lamp = new GameObject("HardWing_Light");
                lamp.transform.SetParent(root, false);
                lamp.transform.position = new Vector3(cx, 4.3f, z);
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.78f, 0.78f);
                light.range = 13f;
                light.intensity = 1.3f;
            }
        }

        private static void BuildDoors(Transform root)
        {
            foreach (HardDoor d in Doors())
            {
                // rotY -90 faces the door east, back into the wing (as the
                // Orientation door on the Hub's own west wall).
                HubDoor door = HubDoor.Build(new Vector3(WestX + 0.3f, 0f, d.z), -90f,
                    d.name, d.scene, -1, HardRed);
                door.hardLevel = d.level;
                door.transform.SetParent(root, true);
            }
        }

        private static void BuildEntranceSign(Transform root)
        {
            Vector3 centre = new Vector3(-19.35f, 0f, (GapMinZ + GapMaxZ) * 0.5f);
            BuildKit.MakeSign(root, centre + Vector3.up * 4.62f, "HARD MODE", HardRed, 0.03f);
            BuildKit.MakeLabel(root, root.InverseTransformPoint(centre + Vector3.up * 3.75f),
                LevelProgress.IsCompleted(1) ? "Replay a cleared level with tougher rules"
                                             : "Clear a level to unlock its Hard copy",
                new Color(1f, 0.82f, 0.82f), 0.02f, billboard: true);
        }

        private static Material SharedMaterial(GameObject go)
        {
            if (go == null) return null;
            var r = go.GetComponent<Renderer>();
            return r != null ? r.sharedMaterial : null;
        }
    }
}
