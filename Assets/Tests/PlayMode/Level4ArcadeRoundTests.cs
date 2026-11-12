using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Cyverse.Tests
{
    /// <summary>
    /// The arcade rounds on Level 4's machines (SPOT THE PHISH, PHISH FRENZY,
    /// ACCESS GRABBER, NETWORK ATTACK): a person's correct pick launches the
    /// machine's round, every way out of it (win, loss, Esc) lands on the
    /// lesson card, and automated routes never see it.
    /// The capture test writes screenshots when
    /// CYVERSE_LEVEL4_VISUAL_CAPTURE=1 (output: CYVERSE_LEVEL4_VISUAL_OUTPUT).
    /// </summary>
    public sealed class Level4ArcadeRoundTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly string[] progressKeys = { "cv_done_3", "cv_done_4", "cv_best" };
        private readonly bool[] hadKeys = new bool[3];
        private readonly int[] savedValues = new int[3];

        [SetUp]
        public void PreserveProgress()
        {
            for (int i = 0; i < progressKeys.Length; i++)
            {
                hadKeys[i] = PlayerPrefs.HasKey(progressKeys[i]);
                savedValues[i] = PlayerPrefs.GetInt(progressKeys[i], 0);
                PlayerPrefs.DeleteKey(progressKeys[i]);
            }
            PlayerPrefs.Save();
        }

        [TearDown]
        public void RestoreProgress()
        {
            for (int i = 0; i < progressKeys.Length; i++)
            {
                if (hadKeys[i]) PlayerPrefs.SetInt(progressKeys[i], savedValues[i]);
                else PlayerPrefs.DeleteKey(progressKeys[i]);
            }
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator PlayerPick_LaunchesClawRound_AndWinLandsOnLesson()
        {
            yield return LoadAndReach("EscalatePrivileges");
            Component station = StationOf("EscalatePrivileges");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            PlayerChooseCorrect(station);

            Component round = ActiveRound();
            Assert.That(round, Is.Not.Null, "A person's correct pick on ACCESS GRABBER should start the claw round.");
            Assert.That(round.GetType().Name, Is.EqualTo("ClawDropRound"));
            Assert.That((bool)StationType.GetProperty("RoundPlaying").GetValue(station), Is.True);
            Assert.That(ChoicePanel(station).activeSelf, Is.False, "The choice panel hides while the round runs.");
            Assert.That(CurrentStationIndex(), Is.EqualTo(3), "The pick itself already counts; the round is a bonus.");

            round.GetType().GetMethod("CompleteForTest").Invoke(round, new object[] { true });
            yield return null;
            Assert.That(ActiveRound(), Is.Null);
            Assert.That((bool)StationType.GetProperty("RoundPlaying").GetValue(station), Is.False);
            Assert.That((bool)StationType.GetProperty("LessonOpen").GetValue(station), Is.True,
                "Winning the round should hand back to the lesson card.");
            Assert.That(ChoicePanel(station).activeSelf, Is.True);
            Assert.That((int)RoundType().GetProperty("TicketsThisRun").GetValue(null), Is.GreaterThan(0));

            Assert.That(GameplayActionTestDriver.Cancel(station), Is.True);
            yield return null;
            Assert.That((bool)StationType.GetProperty("LessonOpen").GetValue(station), Is.False);
        }

        [UnityTest]
        public IEnumerator EscFromRound_SkipsToLessonWithoutTickets()
        {
            yield return LoadAndReach("EscalatePrivileges");
            Component station = StationOf("EscalatePrivileges");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            PlayerChooseCorrect(station);
            Component round = ActiveRound();
            Assert.That(round, Is.Not.Null);
            round.GetType().GetMethod("StartForTest").Invoke(round, null);
            yield return null;
            Assert.That(round.GetType().GetProperty("DropsLeft").GetValue(round), Is.EqualTo(3));

            round.GetType().GetMethod("SkipForTest").Invoke(round, null);
            Assert.That((bool)round.GetType().GetProperty("Skipped").GetValue(round), Is.True);
            Assert.That((int)round.GetType().GetProperty("Tickets").GetValue(round), Is.EqualTo(0));
            yield return null;
            Assert.That(ActiveRound(), Is.Null);
            Assert.That((bool)StationType.GetProperty("LessonOpen").GetValue(station), Is.True,
                "Skipping the round must still show the lesson.");
            Assert.That(GameplayActionTestDriver.Cancel(station), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AutomatedChoice_NeverStartsARound()
        {
            yield return LoadAndReach("EscalatePrivileges");
            Component station = StationOf("EscalatePrivileges");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            Assert.That(GameplayActionTestDriver.Choose(station, CorrectOption(station)), Is.True);
            yield return null;
            Assert.That(ActiveRound(), Is.Null, "Automated routes (tests, replay) skip the round.");
            Assert.That(CurrentStationIndex(), Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator ClawGrabsGoldCard_WhenDroppedOverIt()
        {
            yield return LoadAndReach("EscalatePrivileges");
            Component station = StationOf("EscalatePrivileges");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            PlayerChooseCorrect(station);
            Component round = ActiveRound();
            Type type = round.GetType();
            type.GetMethod("StartForTest").Invoke(round, null);
            type.GetMethod("AimAtGoldForTest").Invoke(round, null);
            type.GetMethod("DropForTest").Invoke(round, null);

            bool held = false;
            float waited = 0f;
            while (waited < 12f && ActiveRound() != null &&
                   type.GetProperty("Phase").GetValue(round).ToString() != "Result")
            {
                held |= (bool)type.GetProperty("HoldingCard").GetValue(round);
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(held, Is.True, "The claw should lift the gold card it was dropped on.");
            Assert.That(type.GetProperty("Phase").GetValue(round).ToString(), Is.EqualTo("Result"));
            Assert.That((bool)type.GetProperty("Won").GetValue(round), Is.True);
            Assert.That((int)type.GetProperty("Tickets").GetValue(round), Is.EqualTo(40),
                "A first-drop win pays 20 plus 10 for each spare drop.");
            type.GetMethod("CompleteForTest").Invoke(round, new object[] { true });
            yield return null;
            Assert.That((bool)StationType.GetProperty("LessonOpen").GetValue(station), Is.True);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CapturesClawRoundStates()
        {
            if (Environment.GetEnvironmentVariable("CYVERSE_LEVEL4_VISUAL_CAPTURE") != "1")
                yield break;

            yield return LoadAndReach("EscalatePrivileges");
            string output = Environment.GetEnvironmentVariable("CYVERSE_LEVEL4_VISUAL_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "VisualQA", "level4-captures");
            Directory.CreateDirectory(output);

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Component station = StationOf("EscalatePrivileges");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            for (int i = 0; i < 20; i++) yield return null;
            ToCameraCanvases(camera);
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-claw-question.png"));
            PlayerChooseCorrect(station);
            Component round = ActiveRound();
            Type type = round.GetType();
            for (int i = 0; i < 20; i++) yield return null;
            ToCameraCanvases(camera);
            Assert.That(Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-claw-attract.png")), Is.True);

            type.GetMethod("StartForTest").Invoke(round, null);
            for (int i = 0; i < 30; i++) yield return null;
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-claw-aim.png"));

            type.GetMethod("AimAtGoldForTest").Invoke(round, null);
            type.GetMethod("DropForTest").Invoke(round, null);
            bool carried = false, revealed = false;
            float waited = 0f;
            while (waited < 12f && type.GetProperty("Phase").GetValue(round).ToString() == "Playing")
            {
                string claw = type.GetField("claw", Private).GetValue(round).ToString();
                if (!carried && claw == "Carry")
                {
                    carried = true;
                    Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-claw-carry.png"));
                }
                if (!revealed && claw == "Reveal" && (float)type.GetField("stateTime", Private).GetValue(round) > 1.45f)
                {
                    revealed = true;
                    Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-claw-reveal.png"));
                }
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            for (int i = 0; i < 10; i++) yield return null;
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-claw-result.png"));

            type.GetMethod("CompleteForTest").Invoke(round, new object[] { true });
            for (int i = 0; i < 20; i++) yield return null;
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-claw-lesson.png"));
        }

        // ---- PHISH FRENZY: Mail Run ----------------------------------------------------

        [UnityTest]
        public IEnumerator MailRun_PlayerPickOnPhishFrenzy_LaunchesAndWinLandsOnLesson()
        {
            yield return LoadAndReach("BypassMfa");
            Component station = StationOf("BypassMfa");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            PlayerChooseCorrect(station);
            Component round = ActiveRound();
            Assert.That(round, Is.Not.Null, "A person's correct pick on PHISH FRENZY should start Mail Run.");
            Assert.That(round.GetType().Name, Is.EqualTo("MailRunRound"));
            Assert.That(ChoicePanel(station).activeSelf, Is.False);
            Assert.That(CurrentStationIndex(), Is.EqualTo(2));

            round.GetType().GetMethod("CompleteForTest").Invoke(round, new object[] { true });
            yield return null;
            Assert.That(ActiveRound(), Is.Null);
            Assert.That((bool)StationType.GetProperty("LessonOpen").GetValue(station), Is.True);
            Assert.That(GameplayActionTestDriver.Cancel(station), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MailRun_SendUnderTestMailbox_Lands_AndFiveHitsPlayThePhone()
        {
            yield return LoadAndReach("BypassMfa");
            Component station = StationOf("BypassMfa");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            PlayerChooseCorrect(station);
            Component round = ActiveRound();
            Type type = round.GetType();
            type.GetMethod("StartForTest").Invoke(round, null);
            type.GetMethod("LineUpForTest").Invoke(round, null);

            for (int shot = 1; shot <= 5; shot++)
            {
                Assert.That((bool)type.GetMethod("SendForTest").Invoke(round, null), Is.True);
                float waited = 0f;
                while (waited < 3f && (int)type.GetProperty("Hits").GetValue(round) < shot)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                Assert.That((int)type.GetProperty("Hits").GetValue(round), Is.EqualTo(shot),
                    "An email sent straight under the test mailbox should land.");
            }
            Assert.That((int)type.GetProperty("Strikes").GetValue(round), Is.EqualTo(0));
            Assert.That((bool)type.GetProperty("ShowingPhone").GetValue(round), Is.True);

            float wait = 0f;
            while (wait < 10f && type.GetProperty("Phase").GetValue(round).ToString() != "Result")
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(type.GetProperty("Phase").GetValue(round).ToString(), Is.EqualTo("Result"));
            Assert.That((bool)type.GetProperty("Won").GetValue(round), Is.True);
            Assert.That((int)type.GetProperty("Tickets").GetValue(round), Is.GreaterThan(20));
            type.GetMethod("CompleteForTest").Invoke(round, new object[] { true });
            yield return null;
            Assert.That((bool)StationType.GetProperty("LessonOpen").GetValue(station), Is.True);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CapturesMailRunStates()
        {
            if (Environment.GetEnvironmentVariable("CYVERSE_LEVEL4_VISUAL_CAPTURE") != "1")
                yield break;

            yield return LoadAndReach("BypassMfa");
            string output = Environment.GetEnvironmentVariable("CYVERSE_LEVEL4_VISUAL_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "VisualQA", "level4-captures");
            Directory.CreateDirectory(output);
            Camera camera = Camera.main;
            Component station = StationOf("BypassMfa");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            PlayerChooseCorrect(station);
            Component round = ActiveRound();
            Type type = round.GetType();
            for (int i = 0; i < 60; i++) yield return null;
            ToCameraCanvases(camera);
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-mail-attract.png"));

            type.GetMethod("StartForTest").Invoke(round, null);
            for (int i = 0; i < 40; i++) yield return null;
            type.GetMethod("SendForTest").Invoke(round, null);
            for (int i = 0; i < 6; i++) yield return null;
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-mail-play.png"));

            type.GetMethod("LineUpForTest").Invoke(round, null);
            for (int shot = 1; shot <= 5; shot++)
            {
                type.GetMethod("SendForTest").Invoke(round, null);
                float waited = 0f;
                while (waited < 3f && (int)type.GetProperty("Hits").GetValue(round) < shot)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            string[] shots = { "arcade-mail-phone-prompt", "arcade-mail-phone-approved", "arcade-mail-phone-blocked" };
            float[] at = { 0.8f, 2.4f, 4.2f };
            float t = 0f;
            for (int i = 0; i < shots.Length; i++)
            {
                while (t < at[i] && type.GetProperty("Phase").GetValue(round).ToString() == "Playing")
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                Capture(camera, Path.Combine(output, "Level4_CyberAttack__" + shots[i] + ".png"));
            }
            float w = 0f;
            while (w < 6f && type.GetProperty("Phase").GetValue(round).ToString() != "Result")
            {
                w += Time.unscaledDeltaTime;
                yield return null;
            }
            for (int i = 0; i < 30; i++) yield return null;
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-mail-result.png"));
            type.GetMethod("CompleteForTest").Invoke(round, new object[] { true });
            for (int i = 0; i < 30; i++) yield return null;
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-mail-lesson.png"));
        }

        // ---- SPOT THE PHISH -------------------------------------------------------------

        [UnityTest]
        public IEnumerator SpotPhish_IsFirst_AndAllRightCallsWinWithTickets()
        {
            yield return LoadAndReach("SpotThePhish");
            Component station = StationOf("SpotThePhish");
            Assert.That((int)StationType.GetProperty("StationIndex").GetValue(station), Is.EqualTo(0),
                "The scam-or-legit machine leads the level.");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            PlayerChooseCorrect(station);
            Component round = ActiveRound();
            Assert.That(round, Is.Not.Null);
            Assert.That(round.GetType().Name, Is.EqualTo("SpotPhishRound"));
            Assert.That(CurrentStationIndex(), Is.EqualTo(1));
            Type type = round.GetType();

            // The first two messages are the fake job post and the hijacked friend.
            Array messages = (Array)type.GetField("Messages").GetValue(null);
            string first = (string)messages.GetValue(0).GetType().GetField("app").GetValue(messages.GetValue(0));
            string second = (string)messages.GetValue(1).GetType().GetField("body").GetValue(messages.GetValue(1));
            StringAssert.Contains("JOB", first);
            StringAssert.Contains("code", second);

            type.GetMethod("StartForTest").Invoke(round, null);
            int count = messages.Length;
            for (int i = 0; i < count; i++)
            {
                Assert.That((int)type.GetProperty("Index").GetValue(round), Is.EqualTo(i));
                bool scam = (bool)type.GetProperty("CurrentIsScam").GetValue(round);
                type.GetMethod("AnswerForTest").Invoke(round, new object[] { scam });
                Assert.That((bool)type.GetProperty("Revealing").GetValue(round), Is.True);
                yield return null;
                type.GetMethod("NextForTest").Invoke(round, null);
                yield return null;
            }
            Assert.That(type.GetProperty("Phase").GetValue(round).ToString(), Is.EqualTo("Result"));
            Assert.That((bool)type.GetProperty("Won").GetValue(round), Is.True);
            Assert.That((int)type.GetProperty("Correct").GetValue(round), Is.EqualTo(count));
            Assert.That((int)type.GetProperty("Tickets").GetValue(round), Is.EqualTo(count * 4 + 10));
            type.GetMethod("CompleteForTest").Invoke(round, new object[] { true });
            yield return null;
            Assert.That((bool)StationType.GetProperty("LessonOpen").GetValue(station), Is.True);
            Assert.That(GameplayActionTestDriver.Cancel(station), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpotPhish_WrongCallsLose_ButStillLandOnLesson()
        {
            yield return LoadAndReach("SpotThePhish");
            Component station = StationOf("SpotThePhish");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            PlayerChooseCorrect(station);
            Component round = ActiveRound();
            Type type = round.GetType();
            type.GetMethod("StartForTest").Invoke(round, null);
            int count = ((Array)type.GetField("Messages").GetValue(null)).Length;
            for (int i = 0; i < count; i++)
            {
                bool scam = (bool)type.GetProperty("CurrentIsScam").GetValue(round);
                type.GetMethod("AnswerForTest").Invoke(round, new object[] { !scam });
                type.GetMethod("NextForTest").Invoke(round, null);
            }
            Assert.That((bool)type.GetProperty("Won").GetValue(round), Is.False);
            Assert.That((int)type.GetProperty("Correct").GetValue(round), Is.EqualTo(0));
            type.GetMethod("CompleteForTest").Invoke(round, new object[] { false });
            yield return null;
            Assert.That((bool)StationType.GetProperty("LessonOpen").GetValue(station), Is.True);
        }

        // ---- NETWORK ATTACK -----------------------------------------------------------

        [UnityTest]
        public IEnumerator NetworkAttack_IsLast_AndPlacingEveryAttackWins()
        {
            yield return LoadAndReach("NetworkAttack");
            Component station = StationOf("NetworkAttack");
            Assert.That((int)StationType.GetProperty("StationIndex").GetValue(station), Is.EqualTo(3));
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            PlayerChooseCorrect(station);
            Component round = ActiveRound();
            Assert.That(round, Is.Not.Null);
            Assert.That(round.GetType().Name, Is.EqualTo("NetworkAttackRound"));
            Type type = round.GetType();

            // Only the three attack points.
            Array attacks = (Array)type.GetField("Attacks").GetValue(null);
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (object attack in attacks)
                seen.Add(attack.GetType().GetField("target").GetValue(attack).ToString());
            Assert.That(seen, Is.EquivalentTo(new[] { "Endpoint", "Router", "Cloud" }));

            type.GetMethod("StartForTest").Invoke(round, null);
            for (int i = 0; i < attacks.Length; i++)
            {
                int target = (int)type.GetProperty("CurrentTarget").GetValue(round);
                type.GetMethod("PickForTest").Invoke(round, new object[] { target });
                float waited = 0f;
                while (waited < 3f && !(bool)type.GetProperty("Revealing").GetValue(round))
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                Assert.That((bool)type.GetProperty("Revealing").GetValue(round), Is.True,
                    "The attack should fly to its target and show the defense.");
                type.GetMethod("NextForTest").Invoke(round, null);
                yield return null;
            }
            Assert.That(type.GetProperty("Phase").GetValue(round).ToString(), Is.EqualTo("Result"));
            Assert.That((bool)type.GetProperty("Won").GetValue(round), Is.True);
            Assert.That((int)type.GetProperty("Tickets").GetValue(round), Is.EqualTo(attacks.Length * 4 + 12));
            type.GetMethod("CompleteForTest").Invoke(round, new object[] { true });
            yield return null;
            Assert.That((bool)StationType.GetProperty("LessonOpen").GetValue(station), Is.True);
            // The last machine finishes the level once the lesson closes.
            Assert.That(GameplayActionTestDriver.Cancel(station), Is.True);
            float wait = 0f;
            object manager = Manager();
            while (wait < 3f && manager.GetType().GetProperty("CurrentPhase").GetValue(manager).ToString() != "Complete")
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(manager.GetType().GetProperty("CurrentPhase").GetValue(manager).ToString(), Is.EqualTo("Complete"));
        }

        [UnityTest]
        public IEnumerator NetworkAttack_WrongPickCountsAsMiss()
        {
            yield return LoadAndReach("NetworkAttack");
            Component station = StationOf("NetworkAttack");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            PlayerChooseCorrect(station);
            Component round = ActiveRound();
            Type type = round.GetType();
            type.GetMethod("StartForTest").Invoke(round, null);
            int target = (int)type.GetProperty("CurrentTarget").GetValue(round);
            type.GetMethod("PickForTest").Invoke(round, new object[] { (target + 1) % 3 });
            type.GetMethod("LandForTest").Invoke(round, null);
            Assert.That((bool)type.GetProperty("Revealing").GetValue(round), Is.True);
            Assert.That((int)type.GetProperty("Correct").GetValue(round), Is.EqualTo(0));
            type.GetMethod("SkipForTest").Invoke(round, null);
            yield return null;
            Assert.That((bool)StationType.GetProperty("LessonOpen").GetValue(station), Is.True);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CapturesNewRoundStates()
        {
            if (Environment.GetEnvironmentVariable("CYVERSE_LEVEL4_VISUAL_CAPTURE") != "1")
                yield break;
            string output = Environment.GetEnvironmentVariable("CYVERSE_LEVEL4_VISUAL_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "VisualQA", "level4-captures");
            Directory.CreateDirectory(output);

            // SPOT THE PHISH
            yield return LoadAndReach("SpotThePhish");
            Camera camera = Camera.main;
            Component station = StationOf("SpotThePhish");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            for (int i = 0; i < 30; i++) yield return null;
            ToCameraCanvases(camera);
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-phish-question.png"));
            PlayerChooseCorrect(station);
            Component round = ActiveRound();
            Type type = round.GetType();
            for (int i = 0; i < 30; i++) yield return null;
            ToCameraCanvases(camera);
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-phish-attract.png"));
            type.GetMethod("StartForTest").Invoke(round, null);
            int count = ((Array)type.GetField("Messages").GetValue(null)).Length;
            for (int m = 0; m < count; m++)
            {
                for (int i = 0; i < 20; i++) yield return null;
                Capture(camera, Path.Combine(output, $"Level4_CyberAttack__arcade-phish-{m + 1}-ask.png"));
                bool scam = (bool)type.GetProperty("CurrentIsScam").GetValue(round);
                // One wrong call so the NOT QUITE state is on record.
                type.GetMethod("AnswerForTest").Invoke(round, new object[] { m == 2 ? !scam : scam });
                for (int i = 0; i < 20; i++) yield return null;
                Capture(camera, Path.Combine(output, $"Level4_CyberAttack__arcade-phish-{m + 1}-clues.png"));
                type.GetMethod("NextForTest").Invoke(round, null);
            }
            for (int i = 0; i < 30; i++) yield return null;
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-phish-result.png"));
            type.GetMethod("CompleteForTest").Invoke(round, new object[] { true });
            for (int i = 0; i < 20; i++) yield return null;
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-phish-lesson.png"));
            GameplayActionTestDriver.Cancel(station);
            yield return null;

            // NETWORK ATTACK
            for (int k = 1; k < 3; k++)
            {
                Component before = Station(k);
                Assert.That(GameplayActionTestDriver.Interact(before), Is.True);
                Assert.That(GameplayActionTestDriver.Choose(before, CorrectOption(before)), Is.True);
                yield return null;
                yield return null;
            }
            station = StationOf("NetworkAttack");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            for (int i = 0; i < 30; i++) yield return null;
            ToCameraCanvases(camera);
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-net-question.png"));
            PlayerChooseCorrect(station);
            round = ActiveRound();
            type = round.GetType();
            for (int i = 0; i < 30; i++) yield return null;
            ToCameraCanvases(camera);
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-net-attract.png"));
            type.GetMethod("StartForTest").Invoke(round, null);
            int attacks = ((Array)type.GetField("Attacks").GetValue(null)).Length;
            for (int a = 0; a < attacks; a++)
            {
                for (int i = 0; i < 20; i++) yield return null;
                if (a < 2) Capture(camera, Path.Combine(output, $"Level4_CyberAttack__arcade-net-{a + 1}-ask.png"));
                int target = (int)type.GetProperty("CurrentTarget").GetValue(round);
                type.GetMethod("PickForTest").Invoke(round, new object[] { a == 1 ? (target + 1) % 3 : target });
                for (int i = 0; i < 8; i++) yield return null;
                if (a == 0) Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-net-1-packet.png"));
                float waited = 0f;
                while (waited < 3f && !(bool)type.GetProperty("Revealing").GetValue(round))
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                for (int i = 0; i < 20; i++) yield return null;
                if (a < 3) Capture(camera, Path.Combine(output, $"Level4_CyberAttack__arcade-net-{a + 1}-reveal.png"));
                type.GetMethod("NextForTest").Invoke(round, null);
            }
            for (int i = 0; i < 30; i++) yield return null;
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-net-result.png"));
            type.GetMethod("CompleteForTest").Invoke(round, new object[] { true });
            for (int i = 0; i < 20; i++) yield return null;
            Capture(camera, Path.Combine(output, "Level4_CyberAttack__arcade-net-lesson.png"));
        }

        // ---- Helpers ------------------------------------------------------------------

        private static Type StationType => FindType("Cyverse.Interaction.CyberAttackStation");
        private static Type RoundType() => FindType("Cyverse.UI.ArcadeRound");

        /// <summary>Loads Level 4, ends the briefing, and clears the machines
        /// before the one of <paramref name="kind"/> by automated picks.</summary>
        private static IEnumerator LoadAndReach(string kind)
        {
            SceneManager.LoadScene("Level4_CyberAttack", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;
            object briefing = Object.FindObjectOfType(FindType("Cyverse.Interaction.VideoStation"));
            Assert.That(briefing, Is.Not.Null);
            Assert.That(GameplayActionTestDriver.Scrub(briefing, float.MaxValue), Is.True);
            yield return null;
            Assert.That(CurrentStationIndex(), Is.EqualTo(0));

            Component target = StationOf(kind);
            int targetIndex = (int)StationType.GetProperty("StationIndex").GetValue(target);
            for (int i = 0; i < targetIndex; i++)
            {
                Component before = Station(i);
                Assert.That(GameplayActionTestDriver.Interact(before), Is.True);
                Assert.That(GameplayActionTestDriver.Choose(before, CorrectOption(before)), Is.True);
                yield return null;
                yield return null;
            }
            Assert.That(CurrentStationIndex(), Is.EqualTo(targetIndex));
        }

        private static Component Station(int index)
        {
            PropertyInfo stationIndex = StationType.GetProperty("StationIndex");
            foreach (Object candidate in Object.FindObjectsOfType(StationType))
                if ((int)stationIndex.GetValue(candidate) == index) return candidate as Component;
            Assert.Fail("No Level 4 station " + index);
            return null;
        }

        private static Component StationOf(string kind)
        {
            PropertyInfo stationKind = StationType.GetProperty("Kind");
            foreach (Object candidate in Object.FindObjectsOfType(StationType))
                if (stationKind.GetValue(candidate).ToString() == kind) return candidate as Component;
            Assert.Fail("No Level 4 station of kind " + kind);
            return null;
        }

        private static object Manager()
        {
            Type managerType = FindType("Cyverse.Level.Level4CyberAttackManager");
            return managerType.GetProperty("Instance").GetValue(null) ?? Object.FindObjectOfType(managerType);
        }

        private static int CurrentStationIndex()
        {
            object manager = Manager();
            return (int)manager.GetType().GetProperty("CurrentStationIndex").GetValue(manager);
        }

        private static int CorrectOption(Component station)
        {
            object manager = Manager();
            object scenario = manager.GetType().GetMethod("ScenarioFor").Invoke(manager, new object[] { station });
            return (int)scenario.GetType().GetField("correctOption").GetValue(scenario);
        }

        /// <summary>The same path a key press or click takes.</summary>
        private static void PlayerChooseCorrect(Component station)
        {
            StationType.GetMethod("PlayerChoose", Private).Invoke(station, new object[] { CorrectOption(station) });
        }

        private static GameObject ChoicePanel(Component station) =>
            StationType.GetField("panel", Private).GetValue(station) as GameObject;

        private static Component ActiveRound() =>
            RoundType().GetProperty("Active").GetValue(null) as Component;

        private static void ToCameraCanvases(Camera camera)
        {
            foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>(true))
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
        }

        private static bool Capture(Camera camera, string path)
        {
            const int width = 1280, height = 720;
            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = null;
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                return File.Exists(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LEVEL4 ARCADE QA] Capture failed: " + exception.Message);
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                if (pixels != null) Object.Destroy(pixels);
                target.Release();
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
