using UnityEngine;
using Cyverse.Forensics;
using Cyverse.Level;

namespace Cyverse.Interaction
{
    /// <summary>
    /// A pinboard in the SOC that fills up as the investigation progresses:
    /// one card per case question, greyed "?" until that question is solved,
    /// then lit in the accent colour showing the finding. Pure display (not
    /// interactable) — it exists so terminal progress is visible in the world
    /// and the room tells the story of the case at a glance.
    /// </summary>
    public class EvidenceBoard : MonoBehaviour
    {
        private InvestigationCase[] cases;
        private TextMesh[] labels;
        private Renderer[] cards;
        private Material pendingMat, solvedMat;

        void Start()
        {
            // Saved-scene self-heal: bind to the console's cases if the
            // factory didn't (event hookups never serialize).
            if (cases == null)
            {
                var console = FindObjectOfType<ForensicsConsole>();
                if (console != null && console.Cases != null) Bind(console.Cases);
            }
        }

        public void Bind(InvestigationCase[] investigationCases)
        {
            cases = investigationCases;
            foreach (var c in cases) c.QuestionAnswered += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            if (cases == null || labels == null) return;
            int i = 0;
            foreach (var c in cases)
            {
                foreach (var q in c.questions)
                {
                    if (i >= labels.Length) return;
                    if (q.Answered)
                    {
                        labels[i].text = Shorten(q.answers[0]);
                        labels[i].color = new Color(0.95f, 0.98f, 1f);
                        if (cards[i] != null) cards[i].sharedMaterial = solvedMat;
                    }
                    else
                    {
                        labels[i].text = "?";
                        labels[i].color = new Color(0.45f, 0.52f, 0.62f);
                        if (cards[i] != null) cards[i].sharedMaterial = pendingMat;
                    }
                    i++;
                }
            }
        }

        private static string Shorten(string answer)
        {
            string s = answer;
            if (s.StartsWith("http://")) s = s.Substring(7);
            if (s.StartsWith("https://")) s = s.Substring(8);
            if (s.Length > 14) s = s.Substring(0, 13) + "…";
            return s.ToUpperInvariant();
        }

        // ---- Construction ----------------------------------------------------

        public static EvidenceBoard Build(Vector3 pos, float rotY, int slots, Color accent)
        {
            var root = new GameObject("EvidenceBoard");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            var frameMat = BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.55f, 0.35f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Post_L", root.transform,
                new Vector3(-2.1f, 1.6f, 0.08f), Vector3.zero, new Vector3(0.12f, 3.2f, 0.12f), frameMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Post_R", root.transform,
                new Vector3(2.1f, 1.6f, 0.08f), Vector3.zero, new Vector3(0.12f, 3.2f, 0.12f), frameMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Panel", root.transform,
                new Vector3(0f, 2.0f, 0.08f), Vector3.zero, new Vector3(4.4f, 2.4f, 0.08f),
                BuildKit.MakeStandard(new Color(0.10f, 0.09f, 0.07f), 0.3f, 0.1f), collider: true);

            var board = root.AddComponent<EvidenceBoard>();
            board.pendingMat = BuildKit.MakeStandard(new Color(0.16f, 0.15f, 0.13f), 0.3f, 0.05f);
            board.solvedMat = BuildKit.MakeEmissive(accent * 0.55f, 0.7f);

            board.labels = new TextMesh[slots];
            board.cards = new Renderer[slots];
            int perRow = 5;
            for (int i = 0; i < slots; i++)
            {
                int row = i / perRow, col = i % perRow;
                float x = -1.6f + col * 0.8f;
                float y = 2.75f - row * 0.62f;
                var cardGo = BuildKit.SpawnLocal(PrimitiveType.Cube, "Evidence_" + i, root.transform,
                    new Vector3(x, y, 0.02f), Vector3.zero, new Vector3(0.7f, 0.5f, 0.03f),
                    board.pendingMat, collider: false);
                board.cards[i] = cardGo.GetComponent<Renderer>();
                // A pushpin, because it's not an evidence board without pins.
                BuildKit.SpawnLocal(PrimitiveType.Sphere, "Pin", root.transform,
                    new Vector3(x, y + 0.21f, -0.005f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f),
                    BuildKit.MakeEmissive(new Color(0.9f, 0.25f, 0.2f), 1.2f), collider: false);
                board.labels[i] = BuildKit.MakeLabel(root.transform, new Vector3(x, y - 0.03f, 0f),
                    "?", new Color(0.45f, 0.52f, 0.62f), 0.012f);
            }

            BuildKit.MakeSign(root.transform, pos + new Vector3(0f, 3.7f, 0f), "EVIDENCE", accent, 0.032f);
            return board;
        }
    }
}
