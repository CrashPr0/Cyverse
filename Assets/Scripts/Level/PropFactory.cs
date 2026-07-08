using System.Collections.Generic;
using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Furnishes the CyberVerse lobby so it reads as a lived-in, high-tech
    /// workplace rather than an empty box: desk pods with monitors and chairs,
    /// a lounge (couches, table, rug, plants), a reception counter, a server
    /// wall, lockers, crates, and hovering drones. Everything is composed from
    /// primitives with a small set of cached, shared materials (good for
    /// batching) and parented under one "Furnishings" root so artists can
    /// move, edit, or delete props freely in a saved scene.
    /// </summary>
    public static class PropFactory
    {
        // ---- Shared material palette (cached; survives repeat builds) -------

        private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        private static Material Mat(string key, System.Func<Material> make)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = make();
            cache[key] = m;
            return m;
        }

        private static Material BodyDark => Mat("body", () => BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.15f), 0.55f, 0.35f));
        private static Material TopLight => Mat("top", () => BuildKit.MakeStandard(new Color(0.17f, 0.19f, 0.25f), 0.7f, 0.3f));
        private static Material Wood => Mat("wood", () => BuildKit.MakeStandard(new Color(0.44f, 0.32f, 0.21f), 0.45f, 0.05f));
        private static Material ChairDark => Mat("chair", () => BuildKit.MakeStandard(new Color(0.07f, 0.08f, 0.11f), 0.4f, 0.2f));
        private static Material Couch => Mat("couch", () => BuildKit.MakeStandard(new Color(0.14f, 0.17f, 0.24f), 0.15f, 0f));
        private static Material Cushion => Mat("cushion", () => BuildKit.MakeStandard(new Color(0.19f, 0.23f, 0.32f), 0.15f, 0f));
        private static Material PlantGreen => Mat("plant", () => BuildKit.MakeStandard(new Color(0.14f, 0.36f, 0.19f), 0.2f, 0f));
        private static Material PotDark => Mat("pot", () => BuildKit.MakeStandard(new Color(0.09f, 0.09f, 0.11f), 0.35f, 0.1f));
        private static Material RackDark => Mat("rack", () => BuildKit.MakeStandard(new Color(0.06f, 0.07f, 0.10f), 0.5f, 0.4f));
        private static Material Rug => Mat("rug", () => BuildKit.MakeStandard(new Color(0.09f, 0.11f, 0.16f), 0.05f, 0f));
        private static Material Screen => Mat("screen", () => BuildKit.MakeEmissive(new Color(0.35f, 0.70f, 1f), 0.9f));
        private static Material LedGreen => Mat("ledg", () => BuildKit.MakeEmissive(new Color(0.3f, 1f, 0.5f), 2.2f));
        private static Material LedCyan => Mat("ledc", () => BuildKit.MakeEmissive(new Color(0.3f, 0.85f, 1f), 2.2f));
        private static Material TrimGlow => Mat("trim", () => BuildKit.MakeEmissive(new Color(0.25f, 0.80f, 1f), 1.4f));

        // ---- Layout ----------------------------------------------------------
        // Room is 40x40 (±20). Kept clear: spawn (0,-8), the station arc
        // (-5,3)/(0,7)/(5,3), the core (0,13), and the scanner (8,15).

        public static GameObject BuildFurnishings()
        {
            var root = new GameObject("Furnishings");

            // Work pods, east and west.
            BuildWorkPod(root.transform, new Vector3(-12f, 0f, -3.5f));
            BuildWorkPod(root.transform, new Vector3(12f, 0f, -3.5f));

            // Lounge, south-west.
            BuildRug(root.transform, new Vector3(-12f, 0f, -14f), new Vector3(4.8f, 0.02f, 3.6f));
            BuildCouch(root.transform, new Vector3(-12f, 0f, -15.3f), 0f);
            BuildCouch(root.transform, new Vector3(-14.4f, 0f, -13.4f), 90f);
            BuildCoffeeTable(root.transform, new Vector3(-11.6f, 0f, -13.5f));
            BuildPlant(root.transform, new Vector3(-9.5f, 0f, -16.5f));

            // Reception, near the spawn approach.
            BuildReception(root.transform, new Vector3(5f, 0f, -12f), -30f);

            // Server wall, flanking the core on the north side.
            for (int i = 0; i < 4; i++)
                BuildServerRack(root.transform, new Vector3(-6.5f + i * 1.2f, 0f, 18.5f), 180f);
            BuildServerRack(root.transform, new Vector3(13.6f, 0f, 18.5f), 180f);
            BuildServerRack(root.transform, new Vector3(14.8f, 0f, 18.5f), 180f);

            // Lockers along the south wall; crates in the south-east corner.
            BuildLockerBank(root.transform, new Vector3(-6f, 0f, -19.15f), 0f);
            BuildLockerBank(root.transform, new Vector3(-3.7f, 0f, -19.15f), 0f);
            BuildCrates(root.transform, new Vector3(14.3f, 0f, -16.9f));

            // Greenery to soften the tech.
            BuildPlant(root.transform, new Vector3(-8f, 0f, 8f));
            BuildPlant(root.transform, new Vector3(8f, 0f, 8f));
            BuildPlant(root.transform, new Vector3(17.3f, 0f, 17.3f));
            BuildPlant(root.transform, new Vector3(-17.3f, 0f, -6f));
            BuildPlant(root.transform, new Vector3(17.3f, 0f, -8f));

            // Ambient drones.
            BuildDrone(root.transform, new Vector3(-6f, 3.4f, 9f));
            BuildDrone(root.transform, new Vector3(11f, 3.8f, -4f));

            return root;
        }

        // ---- Compound props --------------------------------------------------

        /// <summary>Four desks back-to-back around a partition, with chairs.</summary>
        public static void BuildWorkPod(Transform parent, Vector3 center)
        {
            var pod = Group(parent, "WorkPod", center, 0f);

            // Partition down the middle (along Z), with a glowing top edge.
            Child(pod, PrimitiveType.Cube, "Partition", new Vector3(0, 0.75f, 0), Vector3.zero,
                new Vector3(0.06f, 1.5f, 4.6f), BodyDark, collider: true);
            Child(pod, PrimitiveType.Cube, "PartitionGlow", new Vector3(0, 1.52f, 0), Vector3.zero,
                new Vector3(0.09f, 0.04f, 4.6f), TrimGlow, collider: false);

            // Two desks per side, facing the partition; chairs on the outside.
            // Rotation signs were previously swapped between desk and chair,
            // which put the chair beyond the monitor (facing the wrong way,
            // looking at the back of the screen) instead of at the keyboard
            // facing the monitor. Desk and chair rotations must be opposite in
            // sign from what you'd naively guess — see BuildDesk/BuildChair docs.
            foreach (float z in new[] { -1.15f, 1.15f })
            {
                BuildDesk(pod, new Vector3(-0.75f, 0, z), -90f);
                BuildChair(pod, new Vector3(-1.55f, 0, z), 90f);
                BuildDesk(pod, new Vector3(0.75f, 0, z), 90f);
                BuildChair(pod, new Vector3(1.55f, 0, z), -90f);
            }
        }

        /// <summary>Desk with monitor, keyboard, and mug. Screen faces local +Z (the sitter).</summary>
        public static void BuildDesk(Transform parent, Vector3 localPos, float rotY)
        {
            var desk = Group(parent, "Desk", localPos, rotY);

            Child(desk, PrimitiveType.Cube, "Top", new Vector3(0, 0.73f, 0), Vector3.zero,
                new Vector3(1.5f, 0.06f, 0.75f), TopLight, collider: true);
            Child(desk, PrimitiveType.Cube, "LegL", new Vector3(-0.7f, 0.35f, 0), Vector3.zero,
                new Vector3(0.06f, 0.7f, 0.65f), BodyDark, collider: true);
            Child(desk, PrimitiveType.Cube, "LegR", new Vector3(0.7f, 0.35f, 0), Vector3.zero,
                new Vector3(0.06f, 0.7f, 0.65f), BodyDark, collider: true);
            Child(desk, PrimitiveType.Cube, "Modesty", new Vector3(0, 0.45f, -0.3f), Vector3.zero,
                new Vector3(1.38f, 0.5f, 0.04f), BodyDark, collider: false);

            Child(desk, PrimitiveType.Cube, "MonBase", new Vector3(0, 0.77f, -0.16f), Vector3.zero,
                new Vector3(0.22f, 0.02f, 0.14f), ChairDark, collider: false);
            Child(desk, PrimitiveType.Cube, "MonStand", new Vector3(0, 0.86f, -0.16f), Vector3.zero,
                new Vector3(0.05f, 0.16f, 0.05f), ChairDark, collider: false);
            Child(desk, PrimitiveType.Cube, "MonBody", new Vector3(0, 1.08f, -0.16f), Vector3.zero,
                new Vector3(0.60f, 0.36f, 0.04f), RackDark, collider: false);
            Child(desk, PrimitiveType.Cube, "MonScreen", new Vector3(0, 1.08f, -0.135f), Vector3.zero,
                new Vector3(0.55f, 0.31f, 0.012f), Screen, collider: false);

            Child(desk, PrimitiveType.Cube, "Keyboard", new Vector3(0, 0.775f, 0.14f), Vector3.zero,
                new Vector3(0.4f, 0.025f, 0.13f), ChairDark, collider: false);
            Child(desk, PrimitiveType.Cylinder, "Mug", new Vector3(0.48f, 0.815f, 0.1f), Vector3.zero,
                new Vector3(0.055f, 0.05f, 0.055f), Wood, collider: false);
        }

        /// <summary>Office chair. Sitter faces local +Z.</summary>
        public static void BuildChair(Transform parent, Vector3 localPos, float rotY)
        {
            var chair = Group(parent, "Chair", localPos, rotY);

            Child(chair, PrimitiveType.Cube, "Seat", new Vector3(0, 0.46f, 0), Vector3.zero,
                new Vector3(0.46f, 0.07f, 0.46f), ChairDark, collider: true);
            Child(chair, PrimitiveType.Cube, "Back", new Vector3(0, 0.82f, -0.22f), Vector3.zero,
                new Vector3(0.46f, 0.55f, 0.07f), ChairDark, collider: false);
            Child(chair, PrimitiveType.Cylinder, "Post", new Vector3(0, 0.28f, 0), Vector3.zero,
                new Vector3(0.04f, 0.18f, 0.04f), BodyDark, collider: false);
            Child(chair, PrimitiveType.Cube, "BaseA", new Vector3(0, 0.05f, 0), Vector3.zero,
                new Vector3(0.52f, 0.04f, 0.09f), BodyDark, collider: false);
            Child(chair, PrimitiveType.Cube, "BaseB", new Vector3(0, 0.05f, 0), Vector3.zero,
                new Vector3(0.09f, 0.04f, 0.52f), BodyDark, collider: false);
        }

        public static void BuildCouch(Transform parent, Vector3 localPos, float rotY)
        {
            var couch = Group(parent, "Couch", localPos, rotY);

            Child(couch, PrimitiveType.Cube, "Base", new Vector3(0, 0.26f, 0), Vector3.zero,
                new Vector3(2.1f, 0.32f, 0.85f), Couch, collider: true);
            Child(couch, PrimitiveType.Cube, "Back", new Vector3(0, 0.72f, -0.33f), Vector3.zero,
                new Vector3(2.1f, 0.5f, 0.22f), Couch, collider: true);
            Child(couch, PrimitiveType.Cube, "ArmL", new Vector3(-0.93f, 0.5f, 0), Vector3.zero,
                new Vector3(0.24f, 0.52f, 0.85f), Couch, collider: false);
            Child(couch, PrimitiveType.Cube, "ArmR", new Vector3(0.93f, 0.5f, 0), Vector3.zero,
                new Vector3(0.24f, 0.52f, 0.85f), Couch, collider: false);
            foreach (float x in new[] { -0.62f, 0f, 0.62f })
                Child(couch, PrimitiveType.Cube, "Cushion", new Vector3(x, 0.49f, 0.05f), Vector3.zero,
                    new Vector3(0.58f, 0.14f, 0.7f), Cushion, collider: false);
        }

        public static void BuildCoffeeTable(Transform parent, Vector3 localPos)
        {
            var table = Group(parent, "CoffeeTable", localPos, 0f);

            Child(table, PrimitiveType.Cube, "Top", new Vector3(0, 0.42f, 0), Vector3.zero,
                new Vector3(1.15f, 0.05f, 0.62f), Wood, collider: true);
            foreach (var p in new[] { new Vector2(-0.5f, -0.24f), new Vector2(0.5f, -0.24f),
                                      new Vector2(-0.5f, 0.24f), new Vector2(0.5f, 0.24f) })
                Child(table, PrimitiveType.Cube, "Leg", new Vector3(p.x, 0.2f, p.y), Vector3.zero,
                    new Vector3(0.05f, 0.4f, 0.05f), BodyDark, collider: false);
            Child(table, PrimitiveType.Cube, "HoloPad", new Vector3(0.2f, 0.455f, 0.05f), Vector3.zero,
                new Vector3(0.26f, 0.012f, 0.18f), Screen, collider: false);
        }

        public static void BuildRug(Transform parent, Vector3 localPos, Vector3 size)
        {
            var rug = Group(parent, "Rug", localPos, 0f);
            Child(rug, PrimitiveType.Cube, "RugTop", new Vector3(0, 0.011f, 0), Vector3.zero, size, Rug, collider: false);
        }

        public static void BuildPlant(Transform parent, Vector3 localPos)
        {
            var plant = Group(parent, "Plant", localPos, Random.Range(0f, 360f));

            Child(plant, PrimitiveType.Cylinder, "Pot", new Vector3(0, 0.18f, 0), Vector3.zero,
                new Vector3(0.32f, 0.18f, 0.32f), PotDark, collider: true);
            Child(plant, PrimitiveType.Cylinder, "Trunk", new Vector3(0, 0.55f, 0), Vector3.zero,
                new Vector3(0.05f, 0.26f, 0.05f), Wood, collider: false);
            Child(plant, PrimitiveType.Sphere, "FoliageA", new Vector3(0, 1.02f, 0), Vector3.zero,
                new Vector3(0.56f, 0.5f, 0.56f), PlantGreen, collider: false);
            Child(plant, PrimitiveType.Sphere, "FoliageB", new Vector3(0.2f, 0.86f, 0.12f), Vector3.zero,
                new Vector3(0.4f, 0.36f, 0.4f), PlantGreen, collider: false);
            Child(plant, PrimitiveType.Sphere, "FoliageC", new Vector3(-0.18f, 0.9f, -0.1f), Vector3.zero,
                new Vector3(0.38f, 0.34f, 0.38f), PlantGreen, collider: false);
        }

        public static void BuildServerRack(Transform parent, Vector3 localPos, float rotY)
        {
            var rack = Group(parent, "ServerRack", localPos, rotY);

            Child(rack, PrimitiveType.Cube, "Body", new Vector3(0, 1.1f, 0), Vector3.zero,
                new Vector3(0.85f, 2.2f, 0.7f), RackDark, collider: true);
            Child(rack, PrimitiveType.Cube, "Door", new Vector3(0, 1.1f, 0.365f), Vector3.zero,
                new Vector3(0.75f, 2.05f, 0.03f), BodyDark, collider: false);
            for (int i = 0; i < 6; i++)
                Child(rack, PrimitiveType.Cube, "Led" + i,
                    new Vector3(0, 0.45f + i * 0.29f, 0.385f), Vector3.zero,
                    new Vector3(0.42f, 0.025f, 0.015f), i % 2 == 0 ? LedGreen : LedCyan, collider: false);
        }

        public static void BuildLockerBank(Transform parent, Vector3 localPos, float rotY)
        {
            var bank = Group(parent, "Lockers", localPos, rotY);

            Child(bank, PrimitiveType.Cube, "Body", new Vector3(0, 0.95f, 0), Vector3.zero,
                new Vector3(2.0f, 1.9f, 0.5f), BodyDark, collider: true);
            foreach (float x in new[] { -0.5f, 0f, 0.5f })
                Child(bank, PrimitiveType.Cube, "Seam", new Vector3(x, 0.95f, 0.255f), Vector3.zero,
                    new Vector3(0.02f, 1.84f, 0.015f), RackDark, collider: false);
            foreach (float x in new[] { -0.78f, -0.28f, 0.22f, 0.72f })
                Child(bank, PrimitiveType.Cube, "Handle", new Vector3(x, 1.05f, 0.26f), Vector3.zero,
                    new Vector3(0.05f, 0.14f, 0.03f), TopLight, collider: false);
        }

        public static void BuildCrates(Transform parent, Vector3 center)
        {
            var crates = Group(parent, "Crates", center, 0f);

            Crate(crates, new Vector3(0.3f, 0.31f, -0.3f), 8f);
            Crate(crates, new Vector3(-0.5f, 0.31f, 0.5f), -14f);
            Crate(crates, new Vector3(0f, 0.93f, 0f), 22f);
        }

        private static void Crate(Transform parent, Vector3 localPos, float rotY)
        {
            var crate = Group(parent, "Crate", localPos, rotY);
            Child(crate, PrimitiveType.Cube, "Box", Vector3.zero, Vector3.zero,
                new Vector3(0.62f, 0.62f, 0.62f), BodyDark, collider: true);
            Child(crate, PrimitiveType.Cube, "Label", new Vector3(0, 0.05f, 0.315f), Vector3.zero,
                new Vector3(0.2f, 0.12f, 0.012f), LedCyan, collider: false);
        }

        public static void BuildReception(Transform parent, Vector3 localPos, float rotY)
        {
            var desk = Group(parent, "Reception", localPos, rotY);

            Child(desk, PrimitiveType.Cube, "Front", new Vector3(0, 0.525f, 0), Vector3.zero,
                new Vector3(2.6f, 1.05f, 0.5f), BodyDark, collider: true);
            Child(desk, PrimitiveType.Cube, "Side", new Vector3(1.05f, 0.525f, -0.95f), Vector3.zero,
                new Vector3(0.5f, 1.05f, 1.4f), BodyDark, collider: true);
            Child(desk, PrimitiveType.Cube, "FrontTop", new Vector3(0, 1.08f, 0), Vector3.zero,
                new Vector3(2.8f, 0.06f, 0.62f), Wood, collider: false);
            Child(desk, PrimitiveType.Cube, "SideTop", new Vector3(1.05f, 1.08f, -0.95f), Vector3.zero,
                new Vector3(0.62f, 0.06f, 1.5f), Wood, collider: false);
            Child(desk, PrimitiveType.Cube, "AccentGlow", new Vector3(0, 0.85f, 0.26f), Vector3.zero,
                new Vector3(2.55f, 0.05f, 0.02f), TrimGlow, collider: false);

            Child(desk, PrimitiveType.Cube, "MonBody", new Vector3(1.05f, 1.35f, -0.7f), new Vector3(0, -35f, 0),
                new Vector3(0.5f, 0.32f, 0.04f), RackDark, collider: false);

            BuildKit.MakeSign(desk, new Vector3(0, 1.9f, 0), "RECEPTION",
                new Color(0.7f, 0.9f, 1f), 0.028f);
        }

        public static void BuildDrone(Transform parent, Vector3 worldPos)
        {
            var drone = Group(parent, "Drone", worldPos, 0f);

            Child(drone, PrimitiveType.Sphere, "Body", Vector3.zero, Vector3.zero,
                new Vector3(0.34f, 0.22f, 0.34f), BodyDark, collider: false);
            Child(drone, PrimitiveType.Sphere, "Eye", new Vector3(0, -0.08f, 0.13f), Vector3.zero,
                new Vector3(0.09f, 0.09f, 0.09f), LedCyan, collider: false);
            Child(drone, PrimitiveType.Cube, "ArmA", new Vector3(0, 0.06f, 0), Vector3.zero,
                new Vector3(0.95f, 0.03f, 0.05f), RackDark, collider: false);
            Child(drone, PrimitiveType.Cube, "ArmB", new Vector3(0, 0.06f, 0), new Vector3(0, 90f, 0),
                new Vector3(0.95f, 0.03f, 0.05f), RackDark, collider: false);

            var rotors = new List<Transform>();
            foreach (var p in new[] { new Vector3(0.45f, 0.11f, 0), new Vector3(-0.45f, 0.11f, 0),
                                      new Vector3(0, 0.11f, 0.45f), new Vector3(0, 0.11f, -0.45f) })
            {
                var rotor = Child(drone, PrimitiveType.Cylinder, "Rotor", p, Vector3.zero,
                    new Vector3(0.17f, 0.008f, 0.17f), ChairDark, collider: false);
                rotors.Add(rotor.transform);
            }

            var hover = drone.gameObject.AddComponent<Hoverer>();
            hover.rotors = rotors.ToArray();

            // Drones move — they must not be statically batched.
            foreach (var t in drone.GetComponentsInChildren<Transform>(true))
                t.gameObject.isStatic = false;
        }

        // ---- Construction helpers --------------------------------------------

        /// <summary>Prop root: positioned in parent-local space, rotated, static-flagged.</summary>
        private static Transform Group(Transform parent, string name, Vector3 localPos, float rotY)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            return go.transform;
        }

        private static GameObject Child(Transform parent, PrimitiveType type, string name,
            Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null)
                {
                    if (Application.isPlaying) Object.Destroy(c);
                    else Object.DestroyImmediate(c);
                }
            }
            go.isStatic = true;
            return go;
        }
    }
}
