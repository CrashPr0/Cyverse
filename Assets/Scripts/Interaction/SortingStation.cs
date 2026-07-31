using System;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// Task 3 — AUTHORIZATION: Data Triage. Labelled data crates sit on an
    /// intake table; role pedestals stand nearby. Carry each crate to the role
    /// that should have access. Wrong pedestal = denied with a least-privilege
    /// explanation (no punishment beyond the buzz — the crate stays in hand);
    /// right pedestal = points and the reason WHY. All delivered = complete.
    /// </summary>
    public class SortingStation : MonoBehaviour
    {
        public event Action Completed;
        public bool IsComplete { get; private set; }

        public int pointsPerCrate = 60;

        private int delivered;
        private int total;

        /// <summary>Crates filed / crates total (for the HUD checklist).</summary>
        public int Delivered => delivered;
        public int Total => total;

        private void OnDelivered(DropZone zone, Carryable item, Level1IamContent.CrateDef def)
        {
            item.Consume();

            // A small filled marker records the delivery; a pedestal that
            // takes several crates (HR gets two) stacks them.
            int already = 0;
            foreach (Transform child in zone.transform)
                if (child.name.StartsWith("Delivered_")) already++;
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Delivered_" + def.id, zone.transform,
                new Vector3(0f, 1.15f + already * 0.26f, 0f), Vector3.zero, new Vector3(0.34f, 0.24f, 0.34f),
                BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f), collider: false);

            delivered++;
            ScoreSystem.Add(pointsPerCrate);
            if (Audio.Sfx.Instance != null) Audio.Sfx.Instance.PlayConfirm();
            BurstFX.SpawnAbove(zone.transform, new Color(0.30f, 1f, 0.45f), 22, minimumHeight: 1.4f);
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast($"ACCESS GRANTED  +{pointsPerCrate}  ·  {def.why}",
                    new Color(0.30f, 1f, 0.45f));

            if (delivered >= total && !IsComplete)
            {
                IsComplete = true;
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("DATA TRIAGE COMPLETE — least privilege enforced",
                        new Color(0.90f, 0.66f, 0.14f));
                Completed?.Invoke();
            }
        }

        private void OnRejected(DropZone zone, Carryable item)
        {
            // DropZone already played the deny sound.
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast(
                    $"DENIED — {zone.zoneName} doesn't need {item.itemName}. Least privilege: access only for those who NEED it.",
                    new Color(1f, 0.55f, 0.4f));
        }

        // ---- Construction ----------------------------------------------------

        public static SortingStation Build(Vector3 tablePos,
            Level1IamContent.CrateDef[] crates, (string role, Vector3 pos)[] pedestals,
            Color accent, Func<bool> gate, string gateMessage)
        {
            var root = new GameObject("SortingStation");
            root.transform.position = tablePos;

            var station = root.AddComponent<SortingStation>();
            station.total = crates.Length;

            // Intake table with the crates lined up on top.
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Table", root.transform,
                new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(3.4f, 0.9f, 1.1f),
                BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f), collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "TableTrim", root.transform,
                new Vector3(0f, 0.92f, -0.56f), Vector3.zero, new Vector3(3.4f, 0.04f, 0.02f),
                BuildKit.MakeEmissive(accent, 1.4f), collider: false);
            BuildKit.MakeSign(root.transform, tablePos + new Vector3(0f, 2.5f, 0f), "DATA TRIAGE", accent, 0.032f);

            var glow = new GameObject("TriageLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var tl = glow.AddComponent<Light>();
            tl.type = LightType.Point;
            tl.color = accent;
            tl.range = 9f;
            tl.intensity = 1.7f;

            for (int i = 0; i < crates.Length; i++)
            {
                float x = -1.2f + i * (2.4f / Mathf.Max(1, crates.Length - 1));
                var crate = Carryable.Build(tablePos + new Vector3(x, 0.9f, 0f),
                    crates[i].label, crates[i].id, accent);
                crate.gate = gate;
                crate.gateMessage = gateMessage;
            }

            foreach (var (role, pos) in pedestals) DropZone.Build(pos, role, accent);
            station.Rebind(crates);

            return station;
        }

        /// <summary>
        /// Attach the role pedestals' accept/deliver hooks and restore the crate
        /// count. Build() routes through this, but so must the level manager on
        /// Start: <see cref="DropZone.accepts"/> and friends are Func/Action, and
        /// Unity cannot serialize delegates. A scene SAVED with a SortingStation
        /// already in it — every visual-pass copy — therefore loaded with all
        /// three hooks null, so DropZone.Interact() silently did nothing and the
        /// crate was never placed. `total` is likewise unserialized, which is why
        /// the checklist read "Data Triage (0/0 filed)".
        /// Idempotent: zones that already carry hooks are left alone.
        /// </summary>
        public void Rebind(Level1IamContent.CrateDef[] crates)
        {
            if (crates == null || crates.Length == 0) return;
            if (total <= 0) total = crates.Length;

            var defs = crates; // captured by the zone closures below
            foreach (var zone in FindObjectsOfType<DropZone>())
            {
                if (zone.accepts != null || !IsRole(defs, zone.zoneName)) continue;
                var z = zone;
                string zoneRole = z.zoneName;
                z.accepts = item => Find(defs, item.id)?.role == zoneRole;
                z.onAccepted = item => OnDelivered(z, item, Find(defs, item.id));
                z.onRejected = item => OnRejected(z, item);
            }
        }

        private static bool IsRole(Level1IamContent.CrateDef[] defs, string role)
        {
            foreach (var d in defs) if (d.role == role) return true;
            return false;
        }

        private static Level1IamContent.CrateDef Find(Level1IamContent.CrateDef[] defs, string id)
        {
            foreach (var d in defs) if (d.id == id) return d;
            return null;
        }
    }
}
