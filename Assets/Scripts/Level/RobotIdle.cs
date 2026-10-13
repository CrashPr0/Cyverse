using UnityEngine;
using Cyverse.Dialogue;
using Cyverse.Settings;

namespace Cyverse.Level
{
    /// <summary>
    /// Keeps a <see cref="ProceduralRobot"/> alive without a full behaviour
    /// script: it breathes, sways a little, blinks, turns its head toward the
    /// player, rests its arms, and flickers its mouth bars while a dialogue line
    /// from <see cref="speaker"/> is playing. The MFA Specialist has its own,
    /// richer behaviour; this is for robots that mostly stand and greet.
    /// </summary>
    public class RobotIdle : MonoBehaviour
    {
        public string speaker = "";

        private ProceduralRobot.Rig rig;
        private Vector3[] mouthRest;
        private float seed, blinkTimer = 2f, blinkPhase = -1f;
        private bool talking;
        private Quaternion headLook = Quaternion.identity;

        // Right arm rest (shoulder, elbow, wrist); the left arm mirrors it.
        private static readonly Vector3 ShoulderRest = new Vector3(-4f, 0f, 6f);
        private static readonly Vector3 ElbowRest = new Vector3(-14f, 0f, 0f);

        public void Bind(ProceduralRobot.Rig robot, string speakerName)
        {
            rig = robot;
            speaker = speakerName ?? "";
            seed = Random.Range(0f, 10f);
            mouthRest = new Vector3[rig.mouthBars != null ? rig.mouthBars.Length : 0];
            for (int i = 0; i < mouthRest.Length; i++) mouthRest[i] = rig.mouthBars[i].localScale;
            rig.shoulderR.localRotation = Quaternion.Euler(ShoulderRest);
            rig.elbowR.localRotation = Quaternion.Euler(ElbowRest);
            rig.shoulderL.localRotation = Quaternion.Euler(ShoulderRest.x, -ShoulderRest.y, -ShoulderRest.z);
            rig.elbowL.localRotation = Quaternion.Euler(ElbowRest);
        }

        void OnEnable()
        {
            if (DialogueManager.Instance != null) DialogueManager.Instance.LineStarted += OnLine;
        }

        void OnDisable()
        {
            if (DialogueManager.Instance != null) DialogueManager.Instance.LineStarted -= OnLine;
        }

        private void OnLine(DialogueLine line) => talking = line.speaker == speaker;

        void Update()
        {
            if (rig == null) return;
            bool calm = AccessibilitySettings.ReduceMotion;
            float t = Time.time + seed;

            float breath = Mathf.Sin(t * Mathf.PI * 2f * 0.23f) * (calm ? 0.3f : 1f);
            rig.chest.localScale = new Vector3(1f, 1f + 0.004f * breath, 1f + 0.011f * breath);
            if (!calm)
                rig.hips.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.31f) * 1.2f, Mathf.Sin(t * 0.23f) * 0.8f);

            Look();
            Blink();
            Mouth(t);
        }

        private void Look()
        {
            Camera cam = Camera.main;
            Quaternion target = Quaternion.identity;
            if (cam != null)
            {
                Vector3 local = rig.neck.parent.InverseTransformPoint(cam.transform.position);
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -50f, 50f);
                float pitch = Mathf.Clamp(-Mathf.Atan2(local.y - rig.head.localPosition.y - 0.4f,
                    new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -18f, 18f);
                if (local.z > -0.5f) target = Quaternion.Euler(pitch, yaw, 0f);
            }
            headLook = Quaternion.Slerp(headLook, target, 1f - Mathf.Exp(-6f * Time.deltaTime));
            rig.head.localRotation = headLook;
        }

        private void Blink()
        {
            if (blinkPhase < 0f)
            {
                blinkTimer -= Time.deltaTime;
                if (blinkTimer <= 0f)
                {
                    blinkPhase = 0f;
                    blinkTimer = Random.value < 0.18f ? 0.22f : Random.Range(2.4f, 5.6f);
                }
            }
            float closed = 0f;
            if (blinkPhase >= 0f)
            {
                blinkPhase += Time.deltaTime / 0.15f;
                closed = Mathf.Sin(Mathf.Clamp01(blinkPhase) * Mathf.PI);
                if (blinkPhase >= 1f) blinkPhase = -1f;
            }
            Vector3 eye = new Vector3(rig.eyeScale.x, rig.eyeScale.y * Mathf.Lerp(1f, 0.12f, closed), rig.eyeScale.z);
            rig.eyeL.localScale = eye;
            rig.eyeR.localScale = eye;
        }

        private void Mouth(float t)
        {
            bool speaking = talking && DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying;
            for (int i = 0; i < mouthRest.Length; i++)
            {
                float open = speaking ? 0.55f + 0.9f * Mathf.PerlinNoise(t * 9f, i * 1.7f) : 1f;
                Vector3 s = mouthRest[i];
                rig.mouthBars[i].localScale = new Vector3(s.x, s.y * open, s.z);
            }
        }
    }
}
