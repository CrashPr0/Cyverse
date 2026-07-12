using UnityEngine;
using UnityEngine.SceneManagement;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// A level-select doorway. Two modes:
    ///  - LevelGate (hub): open/locked state comes from LevelProgress
    ///    (level N unlocks when N-1 is complete); completed levels show gold.
    ///  - Manual (inside a level, e.g. the "return to hub" exit): the level
    ///    manager calls SetUnlocked when its task is done.
    /// Interacting while open loads the target scene; while locked it explains
    /// what's required. An empty sceneName marks an in-development door.
    /// </summary>
    public class HubDoor : MonoBehaviour, IInteractable
    {
        public enum Mode { LevelGate, Manual }

        public Mode mode = Mode.LevelGate;
        public string sceneName;
        public string displayName = "Level";
        public int levelIndex;

        private bool manualUnlocked;
        private Renderer panelRenderer;
        private TextMesh statusText;

        private bool InDevelopment => string.IsNullOrEmpty(sceneName);
        private bool Unlocked => !InDevelopment &&
            (mode == Mode.Manual ? manualUnlocked : LevelProgress.IsUnlocked(levelIndex));
        private bool Completed => mode == Mode.LevelGate && LevelProgress.IsCompleted(levelIndex);

        public string Prompt
        {
            get
            {
                if (InDevelopment) return $"{displayName}  (in development)";
                if (!Unlocked) return $"{displayName}  (locked)";
                return Completed ? $"Enter {displayName}  ✓" : $"Enter {displayName}";
            }
        }

        public bool CanInteract => true;

        void Start()
        {
            RefreshVisuals();
        }

        public void SetUnlocked(bool value)
        {
            manualUnlocked = value;
            RefreshVisuals();
        }

        public void Interact(GameObject interactor)
        {
            if (InDevelopment)
            {
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast($"{displayName} is still in development.", new Color(0.7f, 0.8f, 0.9f));
                return;
            }
            if (!Unlocked)
            {
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast(
                        mode == Mode.Manual
                            ? "Complete this level's task first."
                            : $"Complete the previous level to unlock {displayName}.",
                        new Color(1f, 0.55f, 0.4f));
                return;
            }

            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
            else if (HudUI.Instance != null)
            {
                // Scene exists but isn't registered — point at the fix.
                HudUI.Instance.ShowToast(
                    $"Scene '{sceneName}' is not in Build Settings — run CyVerse > Add Scenes To Build Settings.",
                    new Color(1f, 0.55f, 0.4f));
            }
        }

        private void RefreshVisuals()
        {
            if (panelRenderer != null && panelRenderer.material.HasProperty("_Color"))
            {
                Color c = InDevelopment ? new Color(0.35f, 0.40f, 0.48f)
                        : !Unlocked ? new Color(0.95f, 0.35f, 0.25f)
                        : Completed ? new Color(0.90f, 0.66f, 0.14f)
                        : new Color(0.30f, 1f, 0.45f);
                panelRenderer.material.SetColor("_Color", c);
            }
            if (statusText != null)
            {
                statusText.text = InDevelopment ? "IN DEVELOPMENT"
                    : !Unlocked ? "LOCKED"
                    : Completed ? "COMPLETE — REPLAY" : "READY";
            }
        }

        // ---- Construction ----------------------------------------------------

        public static HubDoor Build(Vector3 position, float rotY, string displayName,
            string sceneName, int levelIndex, Color accent, Mode mode = Mode.LevelGate)
        {
            var root = new GameObject("HubDoor_" + displayName.Replace(' ', '_'));
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            Material frameMat = BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.55f, 0.35f);

            Frame(root.transform, new Vector3(-1.5f, 2f, 0f), new Vector3(0.4f, 4f, 0.5f), frameMat);
            Frame(root.transform, new Vector3(1.5f, 2f, 0f), new Vector3(0.4f, 4f, 0.5f), frameMat);
            Frame(root.transform, new Vector3(0f, 4.1f, 0f), new Vector3(3.4f, 0.35f, 0.5f), frameMat);

            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "Portal";
            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = new Vector3(0f, 2f, 0f);
            panel.transform.localScale = new Vector3(2.7f, 3.9f, 1f);
            panel.GetComponent<Renderer>().sharedMaterial = BuildKit.MakeHologram(accent);
            // Keep the quad's collider: it's what the interact raycast hits.

            var door = root.AddComponent<HubDoor>();
            door.displayName = displayName;
            door.sceneName = sceneName;
            door.levelIndex = levelIndex;
            door.mode = mode;
            door.panelRenderer = panel.GetComponent<Renderer>();

            BuildKit.MakeSign(root.transform, position + new Vector3(0f, 4.8f, 0f),
                displayName.ToUpperInvariant(), accent, 0.032f);

            var statusGo = new GameObject("Status");
            statusGo.transform.SetParent(root.transform, false);
            statusGo.transform.localPosition = new Vector3(0f, 0.35f, -0.3f);
            var font = HudUI.LoadFont();
            var tm = statusGo.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = 0.028f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 1f, 1f, 0.9f);
            statusGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            door.statusText = tm;

            return door;
        }

        private static void Frame(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Frame";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
