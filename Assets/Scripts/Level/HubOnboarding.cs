using System;
using UnityEngine;
using Cyverse.Core;
using Cyverse.Dialogue;
using Cyverse.Interaction;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// The game's first minute, in the Hub. The password terminal drops a new
    /// player straight into this room, so the opening happens here rather
    /// than inside the optional Orientation level: the title screen (with the
    /// callsign), then three short steps that teach the controls by using
    /// them: look, walk, press E on the Concierge, who points at Level 1.
    /// Runs once per playthrough; skipped in batch runs and once any story
    /// level is done.
    /// </summary>
    public class HubOnboarding : MonoBehaviour
    {
        public const string DoneKey = "cv_hub_intro_done";

        private enum Step { Title, Look, Walk, Talk, Done }

        public static HubOnboarding Instance { get; private set; }
        public bool IsRunning => step != Step.Done;

        private Step step = Step.Title;
        private Action onDone;
        private Transform cam;
        private Transform body;
        private Quaternion lastLook;
        private float lookTurned;
        private Vector3 lastPosition;
        private float walked;
        private GuardNPC concierge;

        private static readonly Color Gold = new Color(0.90f, 0.66f, 0.14f);
        private const float LookDegrees = 60f;
        private const float WalkMetres = 3f;

        public static bool ShouldRun() =>
            !Application.isBatchMode &&
            PlayerPrefs.GetInt(DoneKey, 0) == 0 &&
            LevelProgress.CompletedStoryLevels() == 0;

        /// <summary>Ends the onboarding at once (automated playthroughs).</summary>
        public static void Cancel()
        {
            if (Instance != null && Instance.IsRunning) Instance.Finish();
        }

        public void Begin(Action done)
        {
            Instance = this;
            onDone = done;
            // The title sets the callsign; once it is dismissed the intro
            // will not repeat this playthrough, even if the player heads
            // straight for a door instead of finishing the three steps.
            if (MainMenu.Instance == null) gameObject.AddComponent<MainMenu>();
            MainMenu.Instance.Show(AfterTitle,
                subtitle: "Cybersecurity Training Simulation",
                blurb: "Four hands-on levels: identity and access, cyber defense,\ndigital forensics and a simulated attack.",
                prompt: "[ ENTER ]  Start",
                note: "Play in one sitting: reloading the page starts over.");
        }

        private void AfterTitle()
        {
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();

            Camera main = Camera.main;
            cam = main != null ? main.transform : null;
            var controller = FindObjectOfType<Cyverse.Player.FirstPersonController>();
            body = controller != null ? controller.transform : cam;
            if (cam == null || body == null) { Finish(); return; }

            foreach (GuardNPC npc in FindObjectsOfType<GuardNPC>())
                if (npc.displayName == "Concierge") concierge = npc;

            lastLook = cam.rotation;
            lastPosition = body.position;
            Enter(Step.Look);
        }

        private void Enter(Step next)
        {
            step = next;
            if (HudUI.Instance == null) return;
            switch (step)
            {
                case Step.Look:
                    HudUI.Instance.ShowStep(1, 3, "Move the mouse to look around");
                    break;
                case Step.Walk:
                    HudUI.Instance.ShowStep(2, 3, "Walk with W A S D  (hold Shift to run)");
                    break;
                case Step.Talk:
                    if (concierge == null) { Finish(); return; }
                    HudUI.Instance.ShowStep(3, 3, "Walk to the CONCIERGE and press E to talk");
                    ObjectiveBeacon.Ensure().PointAt(concierge.transform, "TALK TO THE CONCIERGE", Gold);
                    break;
            }
        }

        void Update()
        {
            if (step == Step.Title || step == Step.Done || GameState.AnyMenuOpen) return;
            if (cam == null || body == null) { Finish(); return; }

            switch (step)
            {
                case Step.Look:
                    lookTurned += Quaternion.Angle(lastLook, cam.rotation);
                    lastLook = cam.rotation;
                    if (lookTurned >= LookDegrees) Enter(Step.Walk);
                    break;
                case Step.Walk:
                    Vector3 delta = body.position - lastPosition;
                    delta.y = 0f;
                    walked += delta.magnitude;
                    lastPosition = body.position;
                    if (walked >= WalkMetres) Enter(Step.Talk);
                    break;
                case Step.Talk:
                    if (DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying)
                        Finish();
                    break;
            }
        }

        private void Finish()
        {
            if (step == Step.Done) return;
            step = Step.Done;
            Action cb = onDone;
            onDone = null;
            cb?.Invoke();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
