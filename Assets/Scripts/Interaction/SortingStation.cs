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
            BurstFX.Spawn(zone.transform.position + Vector3.up * 1.4f, new Color(0.30f, 1f, 0.45f), 22);
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

            for (int i = 0; i < crates.Length; i++)
            {
                float x = -1.2f + i * (2.4f / Mathf.Max(1, crates.Length - 1));
                var crate = Carryable.Build(tablePos + new Vector3(x, 0.9f, 0f),
                    crates[i].label, crates[i].id, accent);
                crate.gate = gate;
                crate.gateMessage = gateMessage;
            }

            var defs = crates; // captured by the zone closures below
            foreach (var (role, pos) in pedestals)
            {
                var zone = DropZone.Build(pos, role, accent);
                string zoneRole = role;
                zone.accepts = item => Find(defs, item.id)?.role == zoneRole;
                zone.onAccepted = item => station.OnDelivered(zone, item, Find(defs, item.id));
                zone.onRejected = item => station.OnRejected(zone, item);
            }

            return station;
        }

        private static Level1IamContent.CrateDef Find(Level1IamContent.CrateDef[] defs, string id)
        {
            foreach (var d in defs) if (d.id == id) return d;
            return null;
        }
    }
}
