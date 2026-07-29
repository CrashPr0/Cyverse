using System;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// Task 2 — EDR. One workstation on the SOC floor. Its screen always shows
    /// the running process list; pressing E isolates the machine from the
    /// network.
    ///
    /// The judgement is the gameplay: the player walks the floor reading
    /// process lists and decides which machines are compromised. Isolating a
    /// healthy machine is a real cost in a SOC (you just cut off a colleague),
    /// so it is penalised with a service-disruption warning rather than being
    /// silently allowed.
    /// </summary>
    public class EndpointStation : MonoBehaviour, IInteractable
    {
        public Level2Content.EndpointDef def;
        public EdrFleet fleet;
        public int points = 70;

        public bool Isolated { get; private set; }

        private TextMesh statusText;
        private Renderer screenRenderer;

        public bool CanInteract => !Isolated;
        public string Prompt => $"Isolate {def.hostname} from the network";

        public void Interact(GameObject interactor)
        {
            if (Isolated) return;
            Isolated = true;

            if (def.compromised)
            {
                ScoreSystem_Add(points);
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                if (statusText != null) statusText.text = "● ISOLATED";
                Tint(new Color(0.30f, 1f, 0.45f));
                BurstFX.Spawn(transform.position + Vector3.up * 1.8f, new Color(0.30f, 1f, 0.45f), 20);
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast($"{def.hostname} CONTAINED  +{points}  ·  {def.why}",
                        new Color(0.30f, 1f, 0.45f));
            }
            else
            {
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                if (statusText != null) statusText.text = "● OFFLINE (no threat)";
                Tint(new Color(1f, 0.55f, 0.4f));
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast(
                        $"SERVICE DISRUPTED — {def.hostname} was clean. {def.why}",
                        new Color(1f, 0.55f, 0.4f));
            }

            if (fleet != null) fleet.NotifyDecision(this);
        }

        private static void ScoreSystem_Add(int p) => Core.ScoreSystem.Add(p);

        private void Tint(Color c)
        {
            if (screenRenderer == null) return;
            var m = screenRenderer.material;
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 1.6f);
        }

        // ---- Construction ----------------------------------------------------

        public static EndpointStation Build(Vector3 pos, float rotY,
            Level2Content.EndpointDef def, EdrFleet fleet, Color accent)
        {
            var root = new GameObject("Endpoint_" + def.hostname);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            var bodyMat = BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Desk", root.transform,
                new Vector3(0f, 0.37f, 0f), Vector3.zero, new Vector3(1.5f, 0.74f, 0.8f), bodyMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "MonStand", root.transform,
                new Vector3(0f, 0.85f, 0.1f), Vector3.zero, new Vector3(0.08f, 0.22f, 0.08f), bodyMat, collider: false);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "MonBody", root.transform,
                new Vector3(0f, 1.32f, 0.1f), Vector3.zero, new Vector3(1.25f, 0.78f, 0.06f), bodyMat, collider: true);
            var screen = BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", root.transform,
                new Vector3(0f, 1.32f, 0.06f), Vector3.zero, new Vector3(1.15f, 0.68f, 1f),
                BuildKit.MakeHologram(accent), collider: false);

            var station = root.AddComponent<EndpointStation>();
            station.def = def;
            station.fleet = fleet;
            station.screenRenderer = screen.GetComponent<Renderer>();

            BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.60f, 0.04f),
                def.hostname, accent, 0.026f);

            // The process list IS the puzzle — always visible, no interaction
            // needed to read it.
            var sb = new System.Text.StringBuilder();
            foreach (var p in def.processes) sb.AppendLine(p);
            BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.30f, 0.04f),
                sb.ToString().TrimEnd(), new Color(0.90f, 0.95f, 1f), 0.017f,
                billboard: false, anchor: TextAnchor.MiddleCenter, style: FontStyle.Normal);

            station.statusText = BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.06f, 0.04f),
                "● ONLINE", new Color(0.55f, 0.70f, 0.85f), 0.016f,
                billboard: false, anchor: TextAnchor.MiddleCenter, style: FontStyle.Normal);

            BuildKit.SpawnLocal(PrimitiveType.Cube, "Keyboard", root.transform,
                new Vector3(0f, 0.76f, -0.22f), Vector3.zero, new Vector3(0.55f, 0.03f, 0.18f),
                BuildKit.MakeStandard(new Color(0.07f, 0.08f, 0.11f), 0.4f, 0.2f), collider: false);

            return station;
        }
    }
}
