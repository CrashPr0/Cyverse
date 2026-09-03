using System;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// One workstation on the SOC floor. In the current investigation loop its
    /// two-line screen is compared with the row flagged on the Alert Board.
    /// The legacy EDR isolation behavior remains as a fallback for older scenes.
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
        public SiemConsole alertBoard;
        public int points = 70;

        public bool Isolated { get; private set; }

        private TextMesh statusText;
        private TextMesh hostnameText;
        private TextMesh activityText;
        private Renderer screenRenderer;

        private static readonly Vector3 SocScreenScale = new Vector3(1.55f, 0.86f, 1f);
        private static readonly Vector3 SocMonitorBodyScale = new Vector3(1.66f, 0.96f, 0.06f);

        public bool CanInteract => alertBoard != null ? !alertBoard.IsComplete : !Isolated;
        public string Prompt => alertBoard != null
            ? $"Investigate {def?.hostname ?? "workstation"}"
            : $"Isolate {def?.hostname ?? "workstation"} from the network";

        /// <summary>Turns a saved EDR desk into one of the SOC verification
        /// workstations without requiring the visual-pass scene to be rebuilt.</summary>
        public void ConfigureSoc(Level2Content.EndpointDef definition, SiemConsole board)
        {
            def = definition;
            alertBoard = board;
            fleet = null;
            Isolated = false;
            EnlargeSocMonitor();
            ResolveLabels();
            name = "Endpoint_" + def.hostname;
            if (hostnameText != null) hostnameText.text = def.hostname;
            SetSocActivity(def.processes);
            if (statusText != null) statusText.text = "READY — FLAG A ROW";
            FitMonitorText();
            Tint(new Color(0.55f, 0.85f, 1f));
        }

        private void EnlargeSocMonitor()
        {
            Transform screen = transform.Find("Screen");
            if (screen != null)
            {
                screen.localScale = SocScreenScale;
                screenRenderer = screen.GetComponent<Renderer>();
            }

            Transform monitorBody = transform.Find("MonBody");
            if (monitorBody != null) monitorBody.localScale = SocMonitorBodyScale;
        }

        public void SetSocActivity(string[] lines)
        {
            ResolveLabels();
            if (activityText != null && lines != null)
                activityText.text = string.Join("\n", lines);
            FitMonitorText();
        }

        public void MarkCollectedAsEvidence()
        {
            Isolated = true;
            if (statusText != null) statusText.text = "EVIDENCE COLLECTED";
            if (activityText != null) activityText.text = "POWERED DOWN\nCHAIN OF CUSTODY ATTACHED";
            Tint(new Color(0.90f, 0.66f, 0.14f));
        }

        private void ResolveLabels()
        {
            if (screenRenderer == null)
            {
                var screen = transform.Find("Screen");
                if (screen != null) screenRenderer = screen.GetComponent<Renderer>();
            }
            hostnameText = null;
            statusText = null;
            activityText = null;
            TextMesh[] labels = GetComponentsInChildren<TextMesh>(true);
            foreach (var label in labels)
            {
                string identity = (label.gameObject.name + " " + label.text).ToUpperInvariant();
                if (hostnameText == null &&
                    (label.text.StartsWith("WS-") || label.gameObject.name.StartsWith("Label_WS-")))
                    hostnameText = label;
                else if (statusText == null &&
                         (identity.Contains("ONLINE") || identity.Contains("ISOLATED") ||
                          identity.Contains("OFFLINE") || identity.Contains("READY")))
                    statusText = label;
            }

            // The saved visual-pass labels contain .ps1, spreadsheets and
            // plain status phrases as well as .exe. Identify the remaining
            // middle monitor label structurally instead of guessing by file
            // extension or its old content.
            float bestDistance = float.MaxValue;
            foreach (var label in labels)
            {
                if (label == hostnameText || label == statusText) continue;
                float distance = Mathf.Abs(label.transform.localPosition.y - 1.30f);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                activityText = label;
            }
        }

        /// <summary>Mount the three readouts on the physical monitor and
        /// shrink only as much as necessary to keep every glyph inside it.</summary>
        private void FitMonitorText()
        {
            if (screenRenderer == null) return;
            FitMonitorLabel(hostnameText, 1.62f, 0.026f, 0.88f);
            FitMonitorLabel(activityText, 1.33f, 0.019f, 0.90f);
            FitMonitorLabel(statusText, 1.05f, 0.016f, 0.88f);
        }

        private void FitMonitorLabel(TextMesh label, float localY, float maximumSize, float widthRatio)
        {
            if (label == null) return;
            label.transform.localPosition = new Vector3(0f, localY, 0.04f);
            label.transform.localRotation = Quaternion.identity;
            // Start from the intended readable size on every content change.
            // Otherwise one long scenario permanently leaves later, shorter
            // messages at the previously shrunken size.
            label.characterSize = maximumSize;

            Billboard billboard = label.GetComponent<Billboard>();
            if (billboard != null) billboard.enabled = false;
            SignFX motion = label.GetComponent<SignFX>();
            if (motion != null) motion.enabled = false;

            Renderer renderer = label.GetComponent<Renderer>();
            if (renderer == null) return;
            float width = ProjectedSize(renderer.bounds, transform.right);
            float allowedWidth = Mathf.Abs(screenRenderer.transform.lossyScale.x) * widthRatio;
            if (width > allowedWidth && width > 0.001f)
                label.characterSize *= allowedWidth / width;
        }

        private static float ProjectedSize(Bounds bounds, Vector3 axis)
        {
            axis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
            return 2f * Vector3.Dot(bounds.extents, axis);
        }

        public void Interact(GameObject interactor)
        {
            if (alertBoard != null)
            {
                alertBoard.Investigate(def.hostname);
                return;
            }
            if (Isolated) return;
            Isolated = true;

            if (def.compromised)
            {
                ScoreSystem_Add(points);
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                if (statusText != null) statusText.text = "● ISOLATED";
                Tint(new Color(0.30f, 1f, 0.45f));
                BurstFX.SpawnAbove(transform, new Color(0.30f, 1f, 0.45f), 20, minimumHeight: 1.8f);
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
                new Vector3(0f, 1.32f, 0.1f), Vector3.zero, SocMonitorBodyScale, bodyMat, collider: true);
            var screen = BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", root.transform,
                new Vector3(0f, 1.32f, 0.06f), Vector3.zero, SocScreenScale,
                BuildKit.MakeHologram(accent), collider: false);

            var station = root.AddComponent<EndpointStation>();
            station.def = def;
            station.fleet = fleet;
            station.screenRenderer = screen.GetComponent<Renderer>();

            station.hostnameText = BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.60f, 0.04f),
                def.hostname, accent, 0.026f);

            // The process list IS the puzzle — always visible, no interaction
            // needed to read it.
            var sb = new System.Text.StringBuilder();
            foreach (var p in def.processes) sb.AppendLine(p);
            station.activityText = BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.30f, 0.04f),
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
