using UnityEngine;
using Cyverse.Audio;
using Cyverse.Forensics;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// The SOC investigation desk: a three-monitor console that opens the
    /// forensic query terminal. Owns the level's LogDatabase and
    /// InvestigationCase instances, so terminal progress survives stepping
    /// away and re-opening.
    /// </summary>
    public class ForensicsConsole : MonoBehaviour, IInteractable
    {
        public LogDatabase Database { get; private set; }
        public InvestigationCase Case { get; private set; }

        void Awake()
        {
            // Plain C# objects don't serialize into saved scenes — rebuild
            // them on load so editor-saved copies of the level still work.
            if (Database == null) Database = LogDatabase.Build();
            if (Case == null) Case = InvestigationCase.SpartanGold();
        }

        public bool CanInteract => true;
        public string Prompt => Case != null && Case.IsComplete
            ? "Review the case logs"
            : "Work the case — Forensic Terminal";

        public void Interact(GameObject interactor)
        {
            if (QueryTerminal.Instance == null)
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("Terminal offline — no QueryTerminal in scene.", new Color(1f, 0.55f, 0.4f));
                return;
            }
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            QueryTerminal.Instance.Open(Database, Case);
        }

        // ---- Construction ----------------------------------------------------

        public static ForensicsConsole Build(Vector3 pos, float rotY, Color accent)
        {
            var root = new GameObject("ForensicsConsole");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            var bodyMat = BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Desk", root.transform,
                new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(3.2f, 1.0f, 1.1f), bodyMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "DeskTrim", root.transform,
                new Vector3(0f, 1.02f, -0.56f), Vector3.zero, new Vector3(3.2f, 0.04f, 0.02f),
                BuildKit.MakeEmissive(accent, 1.5f), collider: false);

            // Three angled monitors, KC7-appropriately wall-of-data green.
            for (int i = -1; i <= 1; i++)
            {
                float yaw = i * 24f;
                BuildKit.SpawnLocal(PrimitiveType.Cube, "MonBody_" + i, root.transform,
                    new Vector3(i * 1.05f, 1.65f, 0.18f), new Vector3(-8f, yaw, 0f),
                    new Vector3(1.0f, 0.65f, 0.05f), bodyMat, collider: i == 0);
                BuildKit.SpawnLocal(PrimitiveType.Quad, "MonScreen_" + i, root.transform,
                    new Vector3(i * 1.05f, 1.65f, 0.14f), new Vector3(-8f, yaw, 0f),
                    new Vector3(0.9f, 0.55f, 1f),
                    BuildKit.MakeEmissive(new Color(0.05f, 0.22f, 0.10f), 0.8f), collider: false);
            }
            BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.62f, 0.1f),
                "FORENSIC\nTERMINAL", new Color(0.45f, 1f, 0.60f), 0.024f)
                .transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);

            BuildKit.SpawnLocal(PrimitiveType.Cube, "Keyboard", root.transform,
                new Vector3(0f, 1.03f, -0.25f), Vector3.zero, new Vector3(0.7f, 0.03f, 0.22f),
                BuildKit.MakeStandard(new Color(0.07f, 0.08f, 0.11f), 0.4f, 0.2f), collider: false);

            BuildKit.MakeSign(root.transform, pos + new Vector3(0f, 2.9f, 0f), "INVESTIGATION DESK", accent, 0.032f);

            var glow = new GameObject("ConsoleLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 2.2f, -1.2f);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.45f, 1f, 0.60f);
            l.range = 6f;
            l.intensity = 1.8f;

            var console = root.AddComponent<ForensicsConsole>();
            console.Database = LogDatabase.Build();
            console.Case = InvestigationCase.SpartanGold();
            return console;
        }
    }
}
