using System.Collections.Generic;
using UnityEngine;
using Ring = Cyverse.Level.CharacterMesh.Ring;

namespace Cyverse.Level
{
    /// <summary>
    /// The prize counter's prizes. Each one is a small recognisable toy
    /// (a rocket, a trophy, a rubber duck, a ring stacker and so on) in place
    /// of a coloured box. Every piece is sorted into a bin by material, and
    /// the bins are batched at the end, so the whole display stays a dozen
    /// renderers however many prizes are on the shelves.
    /// </summary>
    public static partial class Level4GameRoom
    {
        /// <summary>Prize pieces sorted by the material they share.</summary>
        private sealed class PrizeBins
        {
            private readonly Dictionary<Material, List<CombineInstance>> bins = new Dictionary<Material, List<CombineInstance>>();

            public void Add(Material material, CombineInstance piece)
            {
                if (!bins.TryGetValue(material, out List<CombineInstance> list)) bins[material] = list = new List<CombineInstance>();
                list.Add(piece);
            }

            public void Build(Transform parent)
            {
                int i = 0;
                foreach (KeyValuePair<Material, List<CombineInstance>> bin in bins)
                    BlockKit.Batch("Prizes_" + i++, parent, bin.Key, bin.Value);
            }
        }

        private static readonly Color PrizeWhite = new Color(0.93f, 0.93f, 0.96f);
        private static readonly Color PrizeDark = new Color(0.07f, 0.06f, 0.09f);
        private static readonly Color PrizeOrange = new Color(0.98f, 0.5f, 0.12f);
        private static readonly Color PrizeRed = new Color(0.88f, 0.14f, 0.2f);

        /// <summary>A round bar of diameter and height 1, standing on y = 0.</summary>
        private static Mesh Round => Shared("prizeRound", () => CharacterMesh.Prism(16, 0.5f, 0.5f, 0f, 1f, "PrizeRound"));
        /// <summary>A cone of base diameter and height 1, nearly pointed.</summary>
        private static Mesh Cone => Shared("prizeCone", () => CharacterMesh.Prism(16, 0.5f, 0.04f, 0f, 1f, "PrizeCone"));
        /// <summary>A bar that widens from 0.5 to 1 across its height: cups, bowls and shades upside down.</summary>
        private static Mesh Flare => Shared("prizeFlare", () => CharacterMesh.Prism(16, 0.25f, 0.5f, 0f, 1f, "PrizeFlare"));
        /// <summary>A flat ring of outer diameter 1 and height 1.</summary>
        private static Mesh Hoop => Shared("prizeHoop", () => CharacterMesh.Annulus(16, 0.24f, 0.5f, 0f, 1f, "PrizeHoop"));

        private static CombineInstance Part(Mesh mesh, Vector3 at, float diameter, float height) =>
            BlockKit.Piece(mesh, at, Vector3.zero, new Vector3(diameter, height, diameter));

        /// <summary>Fills one shelf with nine prizes, starting from the kind
        /// at <paramref name="first"/> so the two shelves differ.</summary>
        private static void BuildPrizeShelf(PrizeBins bins, float shelfTop, float z, int first)
        {
            Color[] colours = { Magenta, Teal, Lemon, Violet };
            for (int k = 0; k < 9; k++)
            {
                Vector3 at = new Vector3(-2.0f + k * 0.5f, shelfTop, z);
                Color c = colours[(k + first) % 4];
                switch ((k * 4 + first) % 9)
                {
                    case 0: PrizeRocket(bins, at, c); break;
                    case 1: PrizeDuck(bins, at); break;
                    case 2: PrizeTrophy(bins, at); break;
                    case 3: PrizeLamp(bins, at, c); break;
                    case 4: PrizeRingStack(bins, at); break;
                    case 5: PrizeGumballs(bins, at, c); break;
                    case 6: PrizePins(bins, at); break;
                    case 7: PrizeGem(bins, at, c); break;
                    default: PrizeLollipops(bins, at); break;
                }
            }
        }

        private static void PrizeRocket(PrizeBins bins, Vector3 at, Color c)
        {
            Material accent = Plain(c * 0.9f);
            bins.Add(Plain(PrizeDark), Part(Flare, at, 0.07f, 0.04f));
            bins.Add(Plain(PrizeWhite), Part(Round, at + Vector3.up * 0.04f, 0.11f, 0.19f));
            bins.Add(accent, Part(Cone, at + Vector3.up * 0.23f, 0.11f, 0.12f));
            bins.Add(accent, Part(Round, at + Vector3.up * 0.1f, 0.116f, 0.02f));
            for (int i = 0; i < 3; i++)
            {
                float yaw = i * 120f + 60f;
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                bins.Add(accent, BlockKit.Piece(BlockKit.CubeMesh, at + dir * 0.075f + Vector3.up * 0.06f,
                    new Vector3(-18f, yaw, 0f), new Vector3(0.014f, 0.11f, 0.07f)));
            }
            bins.Add(Glow(Teal, 0.9f), Ball(at + new Vector3(0f, 0.17f, -0.05f), new Vector3(0.05f, 0.05f, 0.03f)));
        }

        private static void PrizeDuck(PrizeBins bins, Vector3 at)
        {
            Material yellow = Plain(new Color(1f, 0.82f, 0.12f));
            bins.Add(yellow, Ball(at + new Vector3(0f, 0.07f, 0.01f), new Vector3(0.18f, 0.14f, 0.22f)));
            bins.Add(yellow, Ball(at + new Vector3(0f, 0.11f, 0.11f), new Vector3(0.08f, 0.07f, 0.1f)));   // tail
            bins.Add(yellow, Ball(at + new Vector3(0f, 0.18f, -0.05f), Vector3.one * 0.12f));              // head
            bins.Add(Plain(PrizeOrange), Ball(at + new Vector3(0f, 0.165f, -0.115f), new Vector3(0.06f, 0.025f, 0.06f)));
            foreach (float x in new[] { -0.035f, 0.035f })
                bins.Add(Plain(PrizeDark), Ball(at + new Vector3(x, 0.2f, -0.095f), Vector3.one * 0.02f));
        }

        private static void PrizeTrophy(PrizeBins bins, Vector3 at)
        {
            Material gold = Glow(new Color(1f, 0.74f, 0.2f), 0.45f);
            bins.Add(Plain(PrizeDark), Part(Round, at, 0.14f, 0.04f));
            bins.Add(gold, Part(Round, at + Vector3.up * 0.04f, 0.09f, 0.015f));
            bins.Add(gold, Part(Round, at + Vector3.up * 0.05f, 0.03f, 0.09f));
            bins.Add(gold, Part(Flare, at + Vector3.up * 0.14f, 0.17f, 0.15f));
            bins.Add(gold, Part(Round, at + Vector3.up * 0.285f, 0.18f, 0.012f));
            foreach (float x in new[] { -0.095f, 0.095f })
                bins.Add(gold, BlockKit.Piece(Hoop, at + new Vector3(x, 0.225f, 0.007f), new Vector3(-90f, 0f, 0f),
                    new Vector3(0.085f, 0.014f, 0.085f)));
        }

        /// <summary>A lava lamp, lit in its colour.</summary>
        private static void PrizeLamp(PrizeBins bins, Vector3 at, Color c)
        {
            Material metal = Plain(new Color(0.6f, 0.62f, 0.68f));
            bins.Add(metal, BlockKit.Piece(Flare, at + Vector3.up * 0.09f, new Vector3(180f, 0f, 0f), new Vector3(0.12f, 0.09f, 0.12f)));
            bins.Add(Glow(c, 1.0f), BlockKit.Piece(Flare, at + Vector3.up * 0.3f, new Vector3(180f, 0f, 0f), new Vector3(0.1f, 0.21f, 0.1f)));
            bins.Add(metal, Part(Cone, at + Vector3.up * 0.3f, 0.055f, 0.05f));
        }

        /// <summary>A ring stacker: four rings on a post, biggest at the bottom.</summary>
        private static void PrizeRingStack(PrizeBins bins, Vector3 at)
        {
            Color[] rings = { Magenta, Lemon, Teal, Violet };
            bins.Add(Plain(PrizeWhite), Part(Round, at, 0.2f, 0.02f));
            bins.Add(Plain(PrizeWhite), Part(Round, at, 0.03f, 0.27f));
            for (int i = 0; i < 4; i++)
                bins.Add(Plain(rings[i] * 0.9f), Part(Hoop, at + Vector3.up * (0.025f + i * 0.052f), 0.19f - i * 0.03f, 0.046f));
            bins.Add(Plain(PrizeRed), Ball(at + Vector3.up * 0.275f, Vector3.one * 0.055f));
        }

        /// <summary>A gumball machine: a globe of gumballs on a coloured stand.</summary>
        private static void PrizeGumballs(PrizeBins bins, Vector3 at, Color c)
        {
            Material stand = Plain(c * 0.85f);
            bins.Add(stand, BlockKit.Piece(Flare, at + Vector3.up * 0.11f, new Vector3(180f, 0f, 0f), new Vector3(0.15f, 0.11f, 0.15f)));
            bins.Add(Plain(PrizeDark), BlockKit.Piece(BlockKit.CubeMesh, at + new Vector3(0f, 0.04f, -0.07f), Vector3.zero,
                new Vector3(0.05f, 0.03f, 0.02f)));
            Vector3 globe = at + Vector3.up * 0.195f;
            bins.Add(Plain(new Color(0.8f, 0.88f, 0.96f)), Ball(globe, Vector3.one * 0.17f));
            Color[] gum = { Magenta, Lemon, Teal, Violet, PrizeRed, PrizeOrange };
            var rnd = new System.Random(31);
            for (int i = 0; i < 16; i++)
            {
                // Gumballs showing through the front and lower half of the globe.
                float yaw = (float)rnd.NextDouble() * 360f, pitch = -50f + (float)rnd.NextDouble() * 85f;
                Vector3 dir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
                bins.Add(Plain(gum[i % gum.Length]), Ball(globe + dir * 0.072f, Vector3.one * 0.036f));
            }
            bins.Add(stand, Part(Round, globe + Vector3.up * 0.078f, 0.07f, 0.025f));
        }

        /// <summary>Three bowling pins and a ball.</summary>
        private static void PrizePins(PrizeBins bins, Vector3 at)
        {
            Mesh pin = Shared("prizePin", () => CharacterMesh.Loft(new[]
            {
                new Ring(0f, 0.026f, 0.026f), new Ring(0.05f, 0.04f, 0.04f), new Ring(0.1f, 0.04f, 0.04f),
                new Ring(0.16f, 0.019f, 0.019f), new Ring(0.2f, 0.026f, 0.026f), new Ring(0.235f, 0.02f, 0.02f),
                new Ring(0.25f, 0.008f, 0.008f),
            }, 12, 2, "PrizePin"));
            foreach (Vector3 offset in new[] { new Vector3(-0.07f, 0f, 0.04f), new Vector3(0.03f, 0f, 0.06f), new Vector3(0.11f, 0f, 0f) })
            {
                bins.Add(Plain(PrizeWhite), BlockKit.Piece(pin, at + offset, Vector3.zero, Vector3.one));
                bins.Add(Plain(PrizeRed), Part(Round, at + offset + Vector3.up * 0.152f, 0.047f, 0.016f));
            }
            bins.Add(Plain(Violet * 0.8f), Ball(at + new Vector3(-0.02f, 0.055f, -0.06f), Vector3.one * 0.11f));
        }

        /// <summary>A cut gem on a stand, lit in its colour.</summary>
        private static void PrizeGem(PrizeBins bins, Vector3 at, Color c)
        {
            Mesh pavilion = Shared("prizeGemLow", () => CharacterMesh.Prism(8, 0.03f, 0.5f, 0f, 1f, "GemLow"));
            Mesh crown = Shared("prizeGemHigh", () => CharacterMesh.Prism(8, 0.5f, 0.3f, 0f, 1f, "GemHigh"));
            bins.Add(Plain(PrizeDark), Part(Round, at, 0.13f, 0.03f));
            bins.Add(Plain(PrizeDark), Part(Round, at, 0.02f, 0.09f));
            Material gem = Glow(c, 1.1f);
            bins.Add(gem, Part(pavilion, at + Vector3.up * 0.07f, 0.17f, 0.13f));
            bins.Add(gem, Part(crown, at + Vector3.up * 0.2f, 0.17f, 0.045f));
        }

        /// <summary>A jar of big round lollipops.</summary>
        private static void PrizeLollipops(PrizeBins bins, Vector3 at)
        {
            bins.Add(Plain(new Color(0.8f, 0.88f, 0.96f)), Part(Flare, at, 0.13f, 0.11f));
            Color[] sweets = { Magenta, Teal, PrizeOrange };
            Vector3[] tops = { new Vector3(-0.07f, 0.27f, 0.01f), new Vector3(0.005f, 0.33f, 0f), new Vector3(0.075f, 0.25f, -0.01f) };
            for (int i = 0; i < 3; i++)
            {
                bins.Add(Plain(PrizeWhite), Strut(at + new Vector3(0f, 0.03f, 0f), at + tops[i], 0.012f));
                bins.Add(Plain(sweets[i] * 0.95f), BlockKit.Piece(Round, at + tops[i] + new Vector3(0f, 0f, 0.012f),
                    new Vector3(-90f, 0f, 0f), new Vector3(0.1f, 0.024f, 0.1f)));
                bins.Add(Plain(PrizeWhite), BlockKit.Piece(Hoop, at + tops[i] + new Vector3(0f, 0f, -0.002f),
                    new Vector3(-90f, 0f, 0f), new Vector3(0.062f, 0.012f, 0.062f)));
            }
        }

        /// <summary>A jar of bouncy balls and a roll of tickets for the counter top.</summary>
        private static void BuildCounterPrizes(PrizeBins bins, Vector3 jarAt, Vector3 ticketsAt)
        {
            Material glass = Plain(new Color(0.8f, 0.88f, 0.96f));
            bins.Add(glass, Part(Round, jarAt, 0.2f, 0.2f));
            bins.Add(Plain(PrizeRed), Part(Round, jarAt + Vector3.up * 0.2f, 0.21f, 0.02f));
            Color[] balls = { Magenta, Lemon, Teal, Violet, PrizeOrange };
            var rnd = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                float yaw = (float)rnd.NextDouble() * 360f;
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                bins.Add(Plain(balls[i % balls.Length]), Ball(jarAt + dir * 0.082f + Vector3.up * (0.03f + (float)rnd.NextDouble() * 0.14f),
                    Vector3.one * 0.05f));
            }

            // A roll of tickets lying on its side, with a strip trailing off.
            Material ticket = Plain(new Color(1f, 0.8f, 0.3f));
            bins.Add(ticket, BlockKit.Piece(Hoop, ticketsAt + new Vector3(0f, 0.07f, 0.015f), new Vector3(-90f, 0f, 0f), new Vector3(0.14f, 0.03f, 0.14f)));
            bins.Add(ticket, BlockKit.Piece(BlockKit.CubeMesh, ticketsAt + new Vector3(0.12f, 0.003f, 0f), new Vector3(0f, -14f, 0f),
                new Vector3(0.2f, 0.003f, 0.03f)));
        }
    }
}
