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
            /// <summary>The screen this one was opened on top of, if any.</summary>
            internal Lease under;
            /// <summary>True when Settings may open on top of this screen
            /// (it does not use the cancel key itself).</summary>
            internal bool allowsSettingsOverlay;

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

            /// <summary>Lets Settings open on top of this screen. For screens
            /// that do not use the cancel key themselves.</summary>
            public void AllowSettingsOverlay() => allowsSettingsOverlay = true;

            public void MarkTransition()
            {
                if (IsOpen) GameState.MenuTransitionFrame = Time.frameCount;
            }

            public void Close()
            {
                if (!open) return;
                open = false;
                if (!ReferenceEquals(active, this)) return;

                // An overlay hands the screen back to what it covered.
                active = under != null && under.open ? under : null;
                under = null;
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

        /// <summary>True while Settings may open on top of what is showing:
        /// a screen that asked for it (a quiz question), or the results card
        /// on its own.</summary>
        public static bool SettingsOverlayAllowed
        {
            get
            {
                if (GameState.MenuOpen || GameState.TitleActive || GameState.GlossaryOpen ||
                    GameState.SocInvestigationOpen) return false;
                if (active != null) return active.allowsSettingsOverlay;
                return GameState.LevelComplete && !GameState.QuizActive;
            }
        }

        public static bool TryOpen(object owner, Channel channel, out Lease lease,
            bool releaseCursor = false, bool pauseTime = false)
        {
            lease = null;
            if (owner == null) return false;

            Lease covered = null;
            if (active != null)
            {
                if (active.IsOwnedBy(owner, channel))
                {
                    lease = active;
                    return true;
                }
                if (channel != Channel.Settings || !SettingsOverlayAllowed) return false;
                covered = active;
            }
            // Legacy screens not yet migrated still project through GameState;
            // honor them so the seam is safe during incremental adoption.
            else if (GameState.AnyMenuOpen &&
                     !(channel == Channel.Settings && SettingsOverlayAllowed)) return false;

            active = new Lease(owner, channel, releaseCursor, pauseTime,
                Cursor.lockState, Cursor.visible, Time.timeScale)
            { under = covered };
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
            while (active != null)
            {
                Lease top = active;
                top.Close();
                if (ReferenceEquals(active, top)) break;
            }
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
