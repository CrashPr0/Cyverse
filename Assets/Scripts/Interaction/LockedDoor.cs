using System.Collections;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// A physical sliding door that starts locked. Interacting while locked
    /// explains what's required; <see cref="Unlock"/> slides the panel into
    /// the floor and disables its collider so the player can walk through.
    /// Built procedurally via <see cref="Build"/> (frame + panel + sign).
    /// </summary>
    public class LockedDoor : MonoBehaviour, IInteractable
    {
        [TextArea] public string lockedMessage = "This door is locked.";
        public float slideSeconds = 1.2f;

        public bool IsLocked { get; private set; } = true;

        private Transform panel;
        private Collider panelCollider;

        public string Prompt => "Door (locked)";
        public bool CanInteract => IsLocked; // once open, no prompt at all

        public void Interact(GameObject interactor)
        {
            if (!IsLocked) return;
            if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast(lockedMessage, new Color(1f, 0.55f, 0.4f));
        }

        public void Unlock()
        {
            if (!IsLocked) return;
            IsLocked = false;
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            if (panel != null) StartCoroutine(SlideOpen());
        }

        private IEnumerator SlideOpen()
        {
            if (panelCollider != null) panelCollider.enabled = false;

            Vector3 start = panel.localPosition;
            Vector3 end = start + Vector3.down * 3.2f; // sink into the floor
            if (Settings.AccessibilitySettings.ReduceMotion)
            {
                panel.localPosition = end;
                yield break;
            }

            float t = 0f;
            while (t < slideSeconds)
            {
                t += Time.deltaTime;
                panel.localPosition = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / slideSeconds));
                yield return null;
            }
            panel.localPosition = end;
        }

        // ---- Construction ----------------------------------------------------

        /// <summary>Door assembly centred at <paramref name="position"/>, panel
        /// spanning <paramref name="width"/> across local X. Edit/play safe.</summary>
        public static LockedDoor Build(Vector3 position, float rotY, float width,
            string signText, string lockedMessage, Color accent)
        {
            var root = new GameObject("LockedDoor_" + signText.Replace(' ', '_'));
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            Material frameMat = BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.55f, 0.35f);

            Post(root.transform, new Vector3(-width * 0.5f - 0.25f, 2f, 0f), frameMat);
            Post(root.transform, new Vector3(width * 0.5f + 0.25f, 2f, 0f), frameMat);

            var header = GameObject.CreatePrimitive(PrimitiveType.Cube);
            header.name = "Header";
            header.transform.SetParent(root.transform, false);
            header.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            header.transform.localScale = new Vector3(width + 1f, 0.4f, 0.6f);
            header.GetComponent<Renderer>().sharedMaterial = frameMat;

            var panelGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panelGo.name = "Panel";
            panelGo.transform.SetParent(root.transform, false);
            panelGo.transform.localPosition = new Vector3(0f, 2f, 0f);
            panelGo.transform.localScale = new Vector3(width, 4f, 0.25f);
            panelGo.GetComponent<Renderer>().sharedMaterial = BuildKit.MakeHologram(accent);

            var door = root.AddComponent<LockedDoor>();
            door.lockedMessage = lockedMessage;
            door.panel = panelGo.transform;
            door.panelCollider = panelGo.GetComponent<Collider>();

            BuildKit.MakeSign(root.transform, position + new Vector3(0f, 4.9f, 0f), signText, accent, 0.035f);
            return door;
        }

        private static void Post(Transform parent, Vector3 localPos, Material mat)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Post";
            post.transform.SetParent(parent, false);
            post.transform.localPosition = localPos;
            post.transform.localScale = new Vector3(0.5f, 4f, 0.6f);
            post.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
