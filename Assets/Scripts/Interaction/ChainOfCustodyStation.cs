using UnityEngine;
using Cyverse.Forensics;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>Physical evidence-intake station that opens the custody form.</summary>
    public sealed class ChainOfCustodyStation : MonoBehaviour, IInteractable
    {
        public static ChainOfCustodyStation Instance { get; private set; }

        public bool IsComplete => ChainOfCustodyForm.Instance != null && ChainOfCustodyForm.Instance.IsComplete;
        public bool CanInteract => true;
        public string Prompt => IsComplete ? "Review chain-of-custody record" : "Complete chain-of-custody form";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Interact(GameObject interactor)
        {
            if (ChainOfCustodyForm.Instance == null)
            {
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("Custody form unavailable.", new Color(1f, 0.55f, 0.4f));
                return;
            }
            ChainOfCustodyForm.Instance.Open();
        }

        public static ChainOfCustodyStation Ensure()
        {
            if (Instance != null) return Instance;
            ChainOfCustodyStation found = FindObjectOfType<ChainOfCustodyStation>();
            if (found != null) return found;
            return Build(new Vector3(-11.5f, 0f, 8.15f), 0f, Level3ForensicsSceneFactory.ForensicGreen);
        }

        public static ChainOfCustodyStation Build(Vector3 position, float yaw, Color accent)
        {
            GameObject root = new GameObject("ChainOfCustodyStation");
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Material body = BuildKit.MakeStandard(new Color(0.075f, 0.11f, 0.105f), 0.66f, 0.48f);
            Material panel = BuildKit.MakeStandard(new Color(0.055f, 0.075f, 0.075f), 0.62f, 0.54f);

            BuildKit.SpawnLocal(PrimitiveType.Cube, "IntakePlinth", root.transform,
                new Vector3(0f, 0.48f, 0f), Vector3.zero, new Vector3(2.6f, 0.92f, 1.65f), body, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "EvidenceCase", root.transform,
                new Vector3(-0.62f, 1.02f, 0.12f), Vector3.zero, new Vector3(1.05f, 0.16f, 0.85f), panel, collider: false);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "CustodyTablet", root.transform,
                new Vector3(0.62f, 1.08f, -0.18f), new Vector3(12f, 0f, 0f), new Vector3(0.95f, 0.06f, 0.72f),
                BuildKit.MakeEmissive(new Color(0.04f, 0.18f, 0.11f), 0.72f), collider: false);
            BuildKit.MakeLabel(root.transform, new Vector3(0.62f, 1.18f, -0.56f),
                "CUSTODY FORM\nCLICK E TO OPEN", accent, 0.016f, billboard: false);
            BuildKit.MakeSign(root.transform, position + new Vector3(0f, 2.25f, 0f),
                "01  EVIDENCE INTAKE", accent, 0.028f);
            BuildKit.AddAimCollider(root, 2.4f, 3.0f);
            return root.AddComponent<ChainOfCustodyStation>();
        }
    }
}
