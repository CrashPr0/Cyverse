using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// A blocky service robot: chamfered, flat-shaded boxes (see
    /// <see cref="CharacterMesh.BevelBlock"/>) in matte white over dark hinge
    /// joints, with glowing accents and a flat visor face whose square eyes,
    /// brows and equalizer-bar mouth are lights — so it can blink, glance and
    /// "talk" with any voice. Everything hangs off a small joint hierarchy
    /// (hips, chest, neck, head, shoulders, elbows, wrists) for a behaviour
    /// script to animate. About 1.70 m tall with the feet at the root, facing +Z.
    /// </summary>
    public static class ProceduralRobot
    {
        /// <summary>Joints and animated features.</summary>
        public sealed class Rig
        {
            public Transform root, hips, chest, neck, head, handL, handR;
            public Transform shoulderL, elbowL, wristL, shoulderR, elbowR, wristR;
            public Transform eyeL, eyeR, browL, browR, antennaTip;
            public Vector3 eyeLRest, eyeRRest, browLRest, browRRest, eyeScale;
            public Transform[] mouthBars;
            /// <summary>Emissive materials the behaviour drives: eyes/mouth, chest core, antenna tip.</summary>
            public Material face, core, antenna;
        }

        private static readonly Color ShellColor = new Color(0.84f, 0.87f, 0.91f);
        private static readonly Color JointColor = new Color(0.13f, 0.14f, 0.17f);
        private static readonly Color SeamColor = new Color(0.30f, 0.33f, 0.39f);
        private static readonly Color VisorColor = new Color(0.025f, 0.04f, 0.075f);
        private static readonly Color BezelColor = new Color(0.20f, 0.22f, 0.27f);
        private static readonly Color FaceLight = new Color(0.30f, 0.78f, 1f);

        private const float HipsY = 0.92f;
        private const float ChestY = 0.96f;
        private const float HeadJointY = 1.49f;
        private const float ShoulderY = 1.345f;
        private const float ShoulderX = 0.200f;
        private const float UpperArmLength = 0.272f;
        private const float ForearmLength = 0.245f;

        // Chest: a block tapering from the waist to broad shoulders.
        private const float ChestBottom = 1.00f, ChestTop = 1.42f;
        private static readonly Vector2 ChestLow = new Vector2(0.130f, 0.090f);
        private static readonly Vector2 ChestHigh = new Vector2(0.185f, 0.105f);
        private const float ChestZ = 0.005f;

        // Head block, in head-joint space; its front face is the visor plane.
        private const float HeadBottom = -0.015f, HeadTop = 0.205f;
        private static readonly Vector2 HeadHalf = new Vector2(0.105f, 0.098f);

        public static Rig Build(Transform parent, Color accent)
        {
            var rig = new Rig();
            Material shell = Mat(ShellColor, 0.45f, 0.15f);
            Material joint = Mat(JointColor, 0.55f, 0.6f);
            Material seam = Mat(SeamColor, 0.4f, 0.3f);
            Material glow = BuildKit.MakeEmissive(accent, 2.0f);
            rig.face = BuildKit.MakeEmissive(FaceLight, 1.7f);
            rig.core = BuildKit.MakeEmissive(accent, 1.6f);
            rig.antenna = BuildKit.MakeEmissive(accent, 1.4f);

            rig.root = parent;
            BuildLegs(parent, shell, joint, glow);

            rig.hips = Joint("Hips", parent, new Vector3(0f, HipsY, 0f));
            Vector3 hipsToRoot = Vector3.down * HipsY;
            Block("Pelvis", rig.hips, hipsToRoot, 0.80f, 0.95f,
                new Vector2(0.125f, 0.085f), new Vector2(0.150f, 0.100f), 0.016f, shell);
            Block("Abdomen", rig.hips, hipsToRoot, 0.93f, 1.025f,
                new Vector2(0.105f, 0.075f), new Vector2(0.105f, 0.075f), 0.010f, joint);
            foreach (float y in new[] { 0.958f, 0.990f })
                Box("Rib", rig.hips, new Vector3(0f, y, 0f) + hipsToRoot, new Vector3(0.226f, 0.010f, 0.164f), joint);

            rig.chest = Joint("Chest", rig.hips, new Vector3(0f, ChestY - HipsY, 0f));
            Vector3 toRoot = Vector3.down * ChestY;
            Block("ChestShell", rig.chest, toRoot, ChestBottom, ChestTop, ChestLow, ChestHigh, 0.022f, shell, ChestZ);
            // Panel seam across the chest and a vent grille under the core.
            float seamHalf = Mathf.Lerp(ChestLow.x, ChestHigh.x, Mathf.InverseLerp(ChestBottom, ChestTop, 1.105f)) - 0.004f;
            Box("ChestSeam", rig.chest, new Vector3(0f, 1.105f, ChestFront(1.105f) + 0.0005f) + toRoot,
                new Vector3(seamHalf * 2f, 0.005f, 0.004f), seam);
            for (int i = 0; i < 3; i++)
                Box("Vent", rig.chest, new Vector3(0f, 1.155f - i * 0.016f, ChestFront(1.155f) + 0.0005f) + toRoot,
                    new Vector3(0.080f, 0.006f, 0.004f), seam);
            BuildChestCore(rig, toRoot, joint);
            BuildBadge(rig.chest, toRoot, accent);

            rig.neck = Joint("Neck", rig.chest, new Vector3(0f, 1.41f - ChestY, -0.004f));
            Feature("NeckMesh", rig.neck, PrimitiveType.Cylinder, new Vector3(0f, 0.045f, 0f),
                new Vector3(0.075f, 0.050f, 0.075f), joint);
            Box("NeckRing", rig.neck, new Vector3(0f, 0.030f, 0f), new Vector3(0.090f, 0.012f, 0.090f), joint);

            rig.head = Joint("Head", rig.neck, new Vector3(0f, HeadJointY - 1.41f, 0.004f));
            Block("HeadShell", rig.head, Vector3.zero, HeadBottom, HeadTop, HeadHalf, HeadHalf, 0.022f, shell);
            Box("HeadSeam", rig.head, new Vector3(0f, 0.178f, 0f),
                new Vector3(HeadHalf.x * 2f + 0.001f, 0.005f, HeadHalf.y * 2f + 0.001f), seam);
            BuildFace(rig, joint, shell, glow);

            BuildArm(rig, toRoot, +1, shell, joint, glow);
            BuildArm(rig, toRoot, -1, shell, joint, glow);
            return rig;
        }

        /// <summary>The chest's front surface at height y (it tapers outward).</summary>
        private static float ChestFront(float y) =>
            ChestZ + Mathf.Lerp(ChestLow.y, ChestHigh.y, Mathf.InverseLerp(ChestBottom, ChestTop, y));

        // ---- Body ------------------------------------------------------------

        private static void BuildLegs(Transform root, Material shell, Material joint, Material glow)
        {
            // Hip axle: one dark hinge through both legs.
            Hinge("HipAxle", root, new Vector3(0f, 0.865f, 0f), 0.050f, 0.250f, joint);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.090f;
                string s = side < 0 ? "L" : "R";
                Vector3 at = new Vector3(x, 0f, 0f);

                Block("Foot" + s, root, at, 0.010f, 0.072f, new Vector2(0.046f, 0.105f), new Vector2(0.042f, 0.095f),
                    0.012f, shell, 0.040f);
                Box("Sole" + s, root, new Vector3(x, 0.006f, 0.040f), new Vector3(0.094f, 0.012f, 0.214f), joint);
                Box("SoleLight" + s, root, new Vector3(x + side * 0.0475f, 0.011f, 0.040f), new Vector3(0.003f, 0.005f, 0.160f), glow);

                Box("Ankle" + s, root, new Vector3(x, 0.086f, 0f), new Vector3(0.060f, 0.040f, 0.060f), joint);
                Block("Shin" + s, root, at, 0.100f, 0.455f, new Vector2(0.044f, 0.048f), new Vector2(0.052f, 0.056f),
                    0.010f, shell);
                Box("ShinLight" + s, root, new Vector3(x, 0.280f, 0.0525f), new Vector3(0.008f, 0.170f, 0.003f), glow);

                Hinge("Knee" + s, root, new Vector3(x, 0.482f, 0f), 0.044f, 0.094f, joint);
                Block("KneeCap" + s, root, at, 0.450f, 0.515f,
                    new Vector2(0.034f, 0.014f), new Vector2(0.034f, 0.014f), 0.006f, shell, 0.050f);

                float inward = -side * 0.004f;
                Block("Thigh" + s, root, new Vector3(x + inward, 0f, 0f), 0.515f, 0.860f,
                    new Vector2(0.048f, 0.052f), new Vector2(0.058f, 0.062f), 0.010f, shell);
            }
        }

        /// <summary>A round glowing power core in a square dark housing.</summary>
        private static void BuildChestCore(Rig rig, Vector3 toRoot, Material joint)
        {
            const float y = 1.245f;
            float z = ChestFront(y);
            Box("CoreHousing", rig.chest, new Vector3(0f, y, z + 0.002f) + toRoot, new Vector3(0.082f, 0.082f, 0.010f), joint);
            Feature("Core", rig.chest, PrimitiveType.Sphere, new Vector3(0f, y, z + 0.006f) + toRoot,
                new Vector3(0.050f, 0.050f, 0.010f), rig.core);
            var ring = new GameObject("CoreRing", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            ring.SetParent(rig.chest, false);
            ring.localPosition = new Vector3(0f, y, z + 0.0075f) + toRoot;
            ring.GetComponent<MeshFilter>().sharedMesh = CharacterMesh.Arc(0.030f, 0.034f, 0f, 360f, 32, "CoreRing");
            ring.GetComponent<MeshRenderer>().sharedMaterial = rig.core;
        }

        /// <summary>An ID badge on the right of the chest — the same identity
        /// card the player enrolls for in this level.</summary>
        private static void BuildBadge(Transform chest, Vector3 toRoot, Color accent)
        {
            const float x = 0.105f, y = 1.325f;
            var badge = new GameObject("Badge").transform;
            badge.SetParent(chest, false);
            badge.localPosition = new Vector3(x, y, ChestFront(y) + 0.0035f) + toRoot;
            Box("Card", badge, Vector3.zero, new Vector3(0.046f, 0.064f, 0.003f), Mat(new Color(0.95f, 0.96f, 0.98f), 0.5f));
            Box("Stripe", badge, new Vector3(0f, 0.024f, 0.0018f), new Vector3(0.046f, 0.012f, 0.001f), BuildKit.MakeEmissive(accent, 1.2f));
            Box("Photo", badge, new Vector3(-0.010f, -0.002f, 0.0018f), new Vector3(0.018f, 0.022f, 0.001f), Mat(new Color(0.25f, 0.30f, 0.38f), 0.4f));
            Box("Line", badge, new Vector3(0.012f, 0.001f, 0.0018f), new Vector3(0.016f, 0.004f, 0.001f), Mat(new Color(0.35f, 0.40f, 0.48f), 0.4f));
            Box("Line", badge, new Vector3(0.012f, -0.007f, 0.0018f), new Vector3(0.016f, 0.004f, 0.001f), Mat(new Color(0.35f, 0.40f, 0.48f), 0.4f));
        }

        private static void BuildArm(Rig rig, Vector3 toRoot, int side, Material shell, Material joint, Material glow)
        {
            string s = side < 0 ? "L" : "R";
            Transform shoulder = Joint("Shoulder" + s, rig.chest, new Vector3(side * ShoulderX, ShoulderY, -0.004f) + toRoot);
            Box("ShoulderJoint", shoulder, Vector3.zero, new Vector3(0.080f, 0.080f, 0.080f), joint);
            // A chunky pauldron block over the joint.
            Block("Pauldron", shoulder, new Vector3(side * 0.010f, 0f, 0f), -0.030f, 0.062f,
                new Vector2(0.060f, 0.062f), new Vector2(0.056f, 0.058f), 0.016f, shell);

            Block("UpperArm" + s, shoulder, Vector3.zero, -0.250f, -0.030f,
                new Vector2(0.040f, 0.043f), new Vector2(0.044f, 0.047f), 0.008f, shell);

            Transform elbow = Joint("Elbow" + s, shoulder, new Vector3(0f, -UpperArmLength, 0f));
            Hinge("ElbowHinge", elbow, Vector3.zero, 0.037f, 0.084f, joint);
            Block("Forearm" + s, elbow, Vector3.zero, -0.215f, -0.022f,
                new Vector2(0.035f, 0.035f), new Vector2(0.042f, 0.042f), 0.008f, shell);
            Block("WristLight" + s, elbow, Vector3.zero, -0.228f, -0.214f,
                new Vector2(0.036f, 0.036f), new Vector2(0.036f, 0.036f), 0.003f, glow);

            Transform wrist = Joint("Wrist" + s, elbow, new Vector3(0f, -ForearmLength, 0f));
            Box("WristJoint", wrist, Vector3.zero, new Vector3(0.044f, 0.030f, 0.044f), joint);
            // The left hand lies flat under the tablet it carries.
            Transform hand = BuildHand(wrist, side, joint, shell, side < 0 ? -3f : 14f);

            if (side < 0) { rig.shoulderL = shoulder; rig.elbowL = elbow; rig.wristL = wrist; rig.handL = hand; }
            else { rig.shoulderR = shoulder; rig.elbowR = elbow; rig.wristR = wrist; rig.handR = hand; }
        }

        /// <summary>A blocky metal hand: palm with a white back plate, four
        /// fingers and a thumb. Built with the palm's width along X and the palm
        /// facing local +Z, then turned so the palm faces the body.</summary>
        private static Transform BuildHand(Transform wrist, int side, Material metal, Material shell, float curl)
        {
            var hand = new GameObject("Hand").transform;
            hand.SetParent(wrist, false);
            hand.localRotation = Quaternion.Euler(0f, side * -90f, 0f);

            Block("Palm", hand, Vector3.zero, -0.088f, 0.004f, new Vector2(0.036f, 0.015f), new Vector2(0.030f, 0.015f), 0.005f, metal);
            Block("HandPlate", hand, Vector3.zero, -0.080f, -0.010f, new Vector2(0.030f, 0.004f), new Vector2(0.026f, 0.004f),
                0.002f, shell, -0.016f);

            float[] lengths = { 0.054f, 0.064f, 0.060f, 0.046f };
            for (int i = 0; i < 4; i++)
            {
                var finger = new GameObject("Finger" + i).transform;
                finger.SetParent(hand, false);
                finger.localPosition = new Vector3(Mathf.Lerp(-0.025f, 0.025f, i / 3f), -0.088f, 0f);
                finger.localRotation = Quaternion.Euler(-curl, 0f, 0f);
                Block("Finger", finger, Vector3.zero, -lengths[i], 0.004f,
                    new Vector2(0.0075f, 0.0080f), new Vector2(0.0080f, 0.0085f), 0.002f, metal);
            }

            // Palm faces local +Z; on either hand that puts the thumb toward the
            // front once the hand is turned (right hand +X, left hand -X).
            var thumb = new GameObject("Thumb").transform;
            thumb.SetParent(hand, false);
            thumb.localPosition = new Vector3(side * 0.032f, -0.022f, 0.008f);
            thumb.localRotation = Quaternion.Euler(-25f, 0f, side * 38f);
            Block("Thumb", thumb, Vector3.zero, -0.050f, 0.004f, new Vector2(0.009f, 0.0085f), new Vector2(0.0105f, 0.010f), 0.002f, metal);
            return hand;
        }

        // ---- Head ------------------------------------------------------------

        /// <summary>A flat visor in a bezel on the head's front face, with square
        /// light eyes, bar brows and a five-bar mouth; ear blocks with light
        /// panels; a boxy antenna.</summary>
        private static void BuildFace(Rig rig, Material joint, Material shell, Material glow)
        {
            Transform head = rig.head;
            float front = HeadHalf.y;
            Box("Bezel", head, new Vector3(0f, 0.095f, front + 0.0005f), new Vector3(0.188f, 0.158f, 0.004f), Mat(BezelColor, 0.6f, 0.4f));
            Box("Visor", head, new Vector3(0f, 0.095f, front + 0.0020f), new Vector3(0.172f, 0.142f, 0.004f), Mat(VisorColor, 0.9f, 0.3f));
            float lit = front + 0.0045f;

            rig.eyeScale = new Vector3(0.034f, 0.036f, 0.003f);
            for (int side = -1; side <= 1; side += 2)
            {
                Transform eye = Box(side < 0 ? "EyeL" : "EyeR", head, new Vector3(side * 0.036f, 0.110f, lit), rig.eyeScale, rig.face);
                // Level brows sitting well above the eyes: open and friendly.
                Transform brow = Box(side < 0 ? "BrowL" : "BrowR", head, new Vector3(side * 0.036f, 0.152f, lit),
                    new Vector3(0.032f, 0.006f, 0.003f), rig.face);
                if (side < 0) { rig.eyeL = eye; rig.eyeLRest = eye.localPosition; rig.browL = brow; rig.browLRest = brow.localPosition; }
                else { rig.eyeR = eye; rig.eyeRRest = eye.localPosition; rig.browR = brow; rig.browRRest = brow.localPosition; }
            }

            // Five bars on a shallow curve, so at rest the mouth is a small smile.
            rig.mouthBars = new Transform[5];
            for (int i = 0; i < 5; i++)
                rig.mouthBars[i] = Box("MouthBar" + i, head,
                    new Vector3((i - 2) * 0.0115f, 0.058f + 0.0018f * (i - 2) * (i - 2), lit),
                    new Vector3(0.0068f, 0.004f, 0.003f), rig.face);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (HeadHalf.x + 0.010f);
                Box(side < 0 ? "EarL" : "EarR", head, new Vector3(x, 0.098f, -0.004f), new Vector3(0.020f, 0.072f, 0.072f), shell);
                Box("EarLight", head, new Vector3(x + side * 0.0105f, 0.098f, -0.004f), new Vector3(0.002f, 0.040f, 0.040f), glow);
            }

            var antenna = new GameObject("Antenna").transform;
            antenna.SetParent(head, false);
            antenna.localPosition = new Vector3(0.055f, HeadTop, -0.020f);
            Box("Mast", antenna, new Vector3(0f, 0.032f, 0f), new Vector3(0.010f, 0.064f, 0.010f), joint);
            rig.antennaTip = Box("Tip", antenna, new Vector3(0f, 0.072f, 0f), Vector3.one * 0.024f, rig.antenna);
        }

        // ---- Helpers ---------------------------------------------------------

        private static Transform Joint(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        /// <summary>A flat-shaded bevelled block (see CharacterMesh.BevelBlock).</summary>
        private static Transform Block(string name, Transform parent, Vector3 offset, float y0, float y1,
            Vector2 bottom, Vector2 top, float bevel, Material mat, float oz = 0f)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            go.GetComponent<MeshFilter>().sharedMesh = CharacterMesh.BevelBlock(y0, y1, bottom, top, bevel, oz, name);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>A dark cylinder lying along X: elbow, knee and hip hinges.</summary>
        private static void Hinge(string name, Transform parent, Vector3 pos, float radius, float length, Material mat)
        {
            Transform t = Feature(name, parent, PrimitiveType.Cylinder, pos,
                new Vector3(radius * 2f, length * 0.5f, radius * 2f), mat);
            t.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private static Transform Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat) =>
            Feature(name, parent, PrimitiveType.Cube, pos, size, mat);

        private static Transform Feature(string name, Transform parent, PrimitiveType type, Vector3 localPos,
            Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            BuildKit.StripCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private static Material Mat(Color c, float smoothness, float metallic = 0f) =>
            BuildKit.MakeStandard(c, smoothness, metallic);
    }
}
