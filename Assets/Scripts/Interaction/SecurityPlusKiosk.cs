using UnityEngine;
using Cyverse.Audio;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// The physical Security+ Prep Terminal prop. Purely a portal to
    /// UI.SecurityPlusTerminal — an optional study kiosk, not part of the
    /// level's review/scanner flow, so it never blocks completion.
    /// </summary>
    public class SecurityPlusKiosk : MonoBehaviour, IInteractable
    {
        public bool CanInteract => true;
        public string Prompt => "Security+ Prep Terminal";

        void Awake() => NormalizeScreen();
        void OnValidate() => NormalizeScreen();

        private void NormalizeScreen()
        {
            Transform body = BuildKit.AlignKioskScreen(transform, "ScreenBody", "Screen");
            BuildKit.PlaceOnKioskScreen(FindScreenLabel("ScreenTitle", "PRACTICE"),
                body, new Vector2(0f, 0.12f), 0.024f);
            BuildKit.PlaceOnKioskScreen(FindScreenLabel("ScreenSubtitle", "NICE ROLE"),
                body, new Vector2(0f, -0.28f), 0.024f);
        }

        /// <summary>Screen text by object name, falling back to a text prefix
        /// for kiosks built before the labels were named. The mounted sign is
        /// skipped — it is a Sign_ object and belongs above the kiosk, not on
        /// its screen.</summary>
        private Transform FindScreenLabel(string childName, string legacyTextPrefix)
        {
            Transform named = transform.Find(childName);
            if (named != null) return named;

            foreach (TextMesh label in GetComponentsInChildren<TextMesh>(true))
            {
                if (label.name.StartsWith("Sign_")) continue;
                if (!string.IsNullOrEmpty(label.text) && label.text.StartsWith(legacyTextPrefix))
                    return label.transform;
            }
            return null;
        }

        public void Interact(GameObject interactor)
        {
            if (SecurityPlusTerminal.Instance == null)
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("Terminal offline.", new Color(1f, 0.55f, 0.4f));
                return;
            }
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
            SecurityPlusTerminal.Instance.Open();
        }

        // ---- Construction ----------------------------------------------------

        public static SecurityPlusKiosk Build(Vector3 pos, float rotY, Color accent)
        {
            var root = new GameObject("SecurityPlusKiosk");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            var bodyMat = BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Pillar", root.transform,
                new Vector3(0f, 0.65f, 0f), Vector3.zero, new Vector3(0.55f, 1.3f, 0.4f), bodyMat, collider: true);

            BuildKit.SpawnLocal(PrimitiveType.Cube, "ScreenBody", root.transform,
                new Vector3(0f, 1.55f, 0f), new Vector3(35f, 0f, 0f), new Vector3(0.95f, 0.85f, 0.06f), bodyMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", root.transform,
                new Vector3(0f, 1.526f, -0.034f), new Vector3(35f, 0f, 0f), new Vector3(0.85f, 0.74f, 1f),
                BuildKit.MakeHologram(accent), collider: false);

            // The screen says what the kiosk DOES; the sign above it says what
            // the kiosk IS. Printing "SECURITY+ PREP" in both places made the
            // billboarded sign read as a doubled, overlapping copy of the
            // screen from anywhere near the terminal.
            var title = BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.62f, -0.04f),
                "PRACTICE\nEXAM", new Color(0.95f, 0.92f, 1f), 0.026f);
            title.gameObject.name = "ScreenTitle";
            title.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);

            var subtitle = BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.15f, -0.03f),
                "NICE ROLE STUDY BANK   ·   [E]", new Color(0.72f, 0.58f, 1f), 0.017f,
                billboard: false, anchor: TextAnchor.MiddleCenter, style: FontStyle.Normal);
            subtitle.gameObject.name = "ScreenSubtitle";
            subtitle.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);

            // Clear of the screen (top edge ≈ 2.0 m) by a full head height —
            // the sign billboards toward the camera, so anything closer sits
            // visually on top of the screen text when read from standing height.
            BuildKit.MakeSign(root.transform, pos + new Vector3(0f, 3.4f, 0f), "SECURITY+ PREP", accent, 0.032f);

            // Aim helper: same reasoning as the other kiosks — the interact
            // ray leaves the camera near eye height and travels level, so a
            // squat pillar needs a taller trigger volume to be reliably aimable.
            BuildKit.AddAimCollider(root, height: 2.2f, width: 1.1f);

            var glow = new GameObject("KioskLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 2.1f, -0.8f);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = accent;
            l.range = 6f;
            l.intensity = 1.8f;

            var kiosk = root.AddComponent<SecurityPlusKiosk>();
            kiosk.NormalizeScreen();
            return kiosk;
        }
    }
}
