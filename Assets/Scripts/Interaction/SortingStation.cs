using System;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// Task 3 — AUTHORIZATION: Data Triage. Labelled data crates sit on an
    /// intake desk; each role has a desk with an inbox nearby. Carry each crate to the role
    /// that should have access. Wrong pedestal = denied with a least-privilege
    /// explanation (no punishment beyond the buzz — the crate stays in hand);
    /// right pedestal = points and the reason WHY. All delivered = complete.
    /// </summary>
    public class SortingStation : MonoBehaviour
    {
        public event Action Completed;
        public bool IsComplete { get; private set; }

        public int pointsPerCrate = 60;
        /// <summary>Glow colour for the crates filed into role inboxes.</summary>
        public Color accent = BuildKit.AccentCyan;

        // Crate slots on the intake desk, two rows so long names never draw
        // through one another, with a clear pickup aisle round the desk.
        private static readonly Vector3[] CrateSlots =
        {
            new Vector3(-0.95f, 0.9f, -0.38f),
            new Vector3( 0.95f, 0.9f, -0.38f),
            new Vector3(-0.95f, 0.9f,  0.38f),
            new Vector3( 0.95f, 0.9f,  0.38f),
        };

        private Level1IamContent.CrateDef[] defs;
        // Where each crate sits on the intake desk; a Hard run sends a
        // misfiled crate back here.
        private readonly System.Collections.Generic.Dictionary<string, Vector3> intake =
            new System.Collections.Generic.Dictionary<string, Vector3>();
        private const int HardMisfilePenalty = 50;
        private int delivered;
        private int total;

        /// <summary>Crates filed / crates total (for the HUD checklist).</summary>
        public int Delivered => delivered;
        public int Total => total;

        // ---- Wiring ----------------------------------------------------------

        /// <summary>Installs the runtime-only state: the crate table, the
        /// pickup gate, and the accept/reject delegates on the role pedestals.
        /// Delegates never survive being saved into a scene file, so a
        /// visual-pass scene loads with pedestals that silently swallow every
        /// crate unless this runs. Called by Build and again by the level
        /// factory on load; safe either way.</summary>
        public void Configure(Level1IamContent.CrateDef[] crates, Func<bool> gate, string gateMessage)
        {
            defs = crates;
            total = crates.Length;

            NormalizeLayout();

            foreach (var item in FindObjectsOfType<Carryable>())
            {
                if (Find(defs, item.id) == null) continue; // the MFA token, etc.
                item.gate = gate;
                item.gateMessage = gateMessage;
            }

            foreach (var pedestal in FindObjectsOfType<DropZone>())
            {
                if (!IsRole(defs, pedestal.zoneName)) continue; // not one of ours
                var zone = pedestal;
                zone.accepts = item => Find(defs, item.id)?.role == zone.zoneName;
                zone.onAccepted = item => OnDelivered(zone, item, Find(defs, item.id));
                zone.onRejected = item => OnRejected(zone, item);
            }
        }

        /// <summary>Gives Data Triage its own readable work zone. The original
        /// scene put four long, billboarded crate names in one narrow row and
        /// all three role pedestals far behind it, compressing every label into
        /// the same sightline. This 2x2 intake plus staggered role arc is
        /// re-applied when a saved visual-pass scene loads.</summary>
        private void NormalizeLayout()
        {
            // The older saved visual-pass scene has a plain table; the built
            // station has the intake desk instead.
            Transform table = transform.Find("Table");
            if (table != null) table.localScale = new Vector3(3.8f, 0.9f, 1.8f);
            Transform trim = transform.Find("TableTrim");
            if (trim != null)
            {
                trim.localPosition = new Vector3(0f, 0.92f, -0.91f);
                trim.localScale = new Vector3(3.8f, 0.04f, 0.02f);
            }

            int slot = 0;
            foreach (var def in defs)
            {
                Carryable item = null;
                foreach (var candidate in FindObjectsOfType<Carryable>())
                    if (candidate.id == def.id) { item = candidate; break; }
                if (item == null || slot >= CrateSlots.Length) continue;
                // Each crate's name rides at its own height, so no two names
                // can draw through each other from the front, the side or a
                // diagonal. All stay below eye level, under the role busts.
                float labelHeight = 0.56f + slot * 0.09f;
                item.transform.position = transform.TransformPoint(CrateSlots[slot++]);
                intake[def.id] = item.transform.position;
                TextMesh label = item.GetComponentInChildren<TextMesh>(true);
                if (label != null)
                {
                    label.characterSize = 0.016f;
                    label.transform.localPosition = new Vector3(0f, labelHeight, 0f);
                }
            }

            // A shallow arc makes each destination independently readable from
            // the intake table instead of stacking all labels behind it.
            PlaceRole("INTERN", new Vector3(-3.0f, 0f, 3.5f));
            PlaceRole("HR MANAGER", new Vector3(0f, 0f, 4.8f));
            PlaceRole("SYSADMIN", new Vector3(3.0f, 0f, 3.5f));
        }

        private void PlaceRole(string role, Vector3 localPosition)
        {
            foreach (var zone in FindObjectsOfType<DropZone>())
            {
                if (zone.zoneName != role) continue;
                zone.transform.position = transform.TransformPoint(localPosition);
                TextMesh label = zone.GetComponentInChildren<TextMesh>(true);
                if (label != null) label.characterSize = 0.021f;
                return;
            }
        }

        /// <summary>How many crates belong to <paramref name="role"/> — its desk gets a bay for each.</summary>
        private static int CratesFor(Level1IamContent.CrateDef[] defs, string role)
        {
            int n = 0;
            foreach (var d in defs) if (d.role == role) n++;
            return n;
        }

        private static bool IsRole(Level1IamContent.CrateDef[] defs, string zoneName)
        {
            foreach (var d in defs) if (d.role == zoneName) return true;
            return false;
        }

        private void OnDelivered(DropZone zone, Carryable item, Level1IamContent.CrateDef def)
        {
            item.Consume();

            // The crate is filed in the role's inbox; a role that takes
            // several crates (HR gets two) stacks them.
            int already = 0;
            foreach (Transform child in zone.transform)
                if (child.name.StartsWith("Delivered_")) already++;
            TriageProps.FileCrate(zone.transform, def.id, already, CratesFor(defs, zone.zoneName), accent);

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
            if (Cyverse.Core.HardMode.Active && intake.TryGetValue(item.id, out Vector3 home))
            {
                // Hard: a misfile costs points and the crate goes back to intake.
                SendBackToIntake(item, home);
                ScoreSystem.Add(-HardMisfilePenalty);
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast(
                        $"DENIED  −{HardMisfilePenalty}  ·  {zone.zoneName} doesn't need {item.itemName}. Back to intake it goes.",
                        new Color(1f, 0.55f, 0.4f));
                return;
            }
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast(
                    $"DENIED — {zone.zoneName} doesn't need {item.itemName}. Least privilege: access only for those who NEED it.",
                    new Color(1f, 0.55f, 0.4f));
        }

        /// <summary>Out of the player's hands and back onto its intake bay,
        /// as Carryable.PutDown leaves an item (label shown, colliders on).</summary>
        private static void SendBackToIntake(Carryable item, Vector3 home)
        {
            item.transform.SetParent(null, true);
            item.transform.SetPositionAndRotation(home, Quaternion.identity);
            item.transform.localScale = Vector3.one;
            var label = item.GetComponentInChildren<TextMesh>(true);
            if (label != null) label.gameObject.SetActive(true);
            foreach (Collider c in item.GetComponentsInChildren<Collider>(true))
                if (c != null) c.enabled = true;
            if (Carryable.Carried == item) Carryable.ClearCarried();
        }

        // ---- Construction ----------------------------------------------------

        public static SortingStation Build(Vector3 tablePos,
            Level1IamContent.CrateDef[] crates, (string role, Vector3 pos)[] pedestals,
            Color accent, Func<bool> gate, string gateMessage)
        {
            var root = new GameObject("SortingStation");
            root.transform.position = tablePos;

            var station = root.AddComponent<SortingStation>();
            station.accent = accent;

            // Intake desk; Configure arranges the crates in its 2x2 bays.
            TriageProps.BuildIntakeDesk(root.transform, accent, CrateSlots);
            BuildKit.MakeSign(root.transform, tablePos + new Vector3(0f, 3.8f, 0f), "DATA TRIAGE", accent, 0.032f);

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
                Carryable crate = Carryable.Build(tablePos + Vector3.up * 0.9f,
                    crates[i].label, crates[i].id, accent);
                TriageProps.DressCrate(crate.gameObject, crates[i].id, accent);
            }

            foreach (var (role, pos) in pedestals)
                TriageProps.DressRoleDesk(DropZone.Build(pos, role, accent).gameObject, role, accent,
                    CratesFor(crates, role));

            // Crates and pedestals exist now, so the shared wiring path can
            // find and hook them — the same one a saved scene goes through.
            station.Configure(crates, gate, gateMessage);
            return station;
        }

        private static Level1IamContent.CrateDef Find(Level1IamContent.CrateDef[] defs, string id)
        {
            foreach (var d in defs) if (d.id == id) return d;
            return null;
        }
    }
}
