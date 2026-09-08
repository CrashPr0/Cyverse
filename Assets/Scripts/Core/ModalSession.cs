using UnityEngine;
using UnityEngine.EventSystems;

namespace Cyverse.Core
{
    /// <summary>
    /// Owns the one-modal-at-a-time protocol: exclusivity, the matching
    /// GameState projection, transition-frame input suppression, cursor state,
    /// optional pause, and safe teardown. Modal implementations only retain a
    /// lease for as long as their screen is visible.
    /// </summary>
    public static class ModalSession
    {
        public enum Channel
        {
            Quiz,
            SocInvestigation,
            Glossary,
            Settings,
            Title,
        }

        public sealed class Lease
        {
            private readonly object owner;
            private readonly Channel channel;
            private readonly bool releasedCursor;
            private readonly bool pausedTime;
            private readonly CursorLockMode previousCursorLock;
            private readonly bool previousCursorVisible;
            private readonly float previousTimeScale;
            private bool open = true;

            internal Lease(object owner, Channel channel, bool releasedCursor, bool pausedTime,
                CursorLockMode previousCursorLock, bool previousCursorVisible,
                float previousTimeScale)
            {
                this.owner = owner;
                this.channel = channel;
                this.releasedCursor = releasedCursor;
                this.pausedTime = pausedTime;
                this.previousCursorLock = previousCursorLock;
                this.previousCursorVisible = previousCursorVisible;
                this.previousTimeScale = previousTimeScale;
            }

            public bool IsOpen => open && ReferenceEquals(active, this);

            public void MarkTransition()
            {
                if (IsOpen) GameState.MenuTransitionFrame = Time.frameCount;
            }

            public void Close()
            {
                if (!open) return;
                open = false;
                if (!ReferenceEquals(active, this)) return;

                active = null;
                SetChannel(channel, false);
                GameState.MenuTransitionFrame = Time.frameCount;
                if (pausedTime) Time.timeScale = previousTimeScale;
                if (releasedCursor)
                {
                    Cursor.lockState = previousCursorLock;
                    Cursor.visible = previousCursorVisible;
                }
            }

            internal bool IsOwnedBy(object candidate, Channel candidateChannel) =>
                ReferenceEquals(owner, candidate) && channel == candidateChannel;
        }

        private static Lease active;

        public static bool TryOpen(object owner, Channel channel, out Lease lease,
            bool releaseCursor = false, bool pauseTime = false)
        {
            lease = null;
            if (owner == null) return false;

            if (active != null)
            {
                if (!active.IsOwnedBy(owner, channel)) return false;
                lease = active;
                return true;
            }

            // Legacy screens not yet migrated still project through GameState;
            // honor them so the seam is safe during incremental adoption.
            if (GameState.AnyMenuOpen) return false;

            active = new Lease(owner, channel, releaseCursor, pauseTime,
                Cursor.lockState, Cursor.visible, Time.timeScale);
            lease = active;
            SetChannel(channel, true);
            GameState.MenuTransitionFrame = Time.frameCount;
            if (pauseTime) Time.timeScale = 0f;
            if (releaseCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return true;
        }

        public static bool IsTransitionFrame =>
            Time.frameCount == GameState.MenuTransitionFrame;

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject events = new GameObject("UIEventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(events);
        }

        internal static void Reset()
        {
            // A reset is also teardown: never leak a paused clock or released
            // cursor merely because the owning screen disappeared with a scene.
            if (active != null) active.Close();
            active = null;
        }

        private static void SetChannel(Channel channel, bool value)
        {
            switch (channel)
            {
                case Channel.Quiz: GameState.QuizActive = value; break;
                case Channel.SocInvestigation: GameState.SocInvestigationOpen = value; break;
                case Channel.Glossary: GameState.GlossaryOpen = value; break;
                case Channel.Settings: GameState.MenuOpen = value; break;
                case Channel.Title: GameState.TitleActive = value; break;
            }
        }
    }
}
