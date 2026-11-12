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
    /// The arcade round on ACCESS GRABBER (Level 4, station 2): a person's
    /// correct pick launches the claw round, every way out of it (win, loss,
    /// Esc) lands on the lesson card, and automated routes never see it.
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
            yield return LoadAndReachStationTwo();
            Component station = Station(1);
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            PlayerChooseCorrect(station);

            Component round = ActiveRound();
            Assert.That(round, Is.Not.Null, "A person's correct pick on ACCESS GRABBER should start the claw round.");
            Assert.That(round.GetType().Name, Is.EqualTo("ClawDropRound"));
            Assert.That((bool)StationType.GetProperty("RoundPlaying").GetValue(station), Is.True);
            Assert.That(ChoicePanel(station).activeSelf, Is.False, "The choice panel hides while the round runs.");
            Assert.That(CurrentStationIndex(), Is.EqualTo(2), "The pick itself already counts; the round is a bonus.");

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
            yield return LoadAndReachStationTwo();
            Component station = Station(1);
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
            yield return LoadAndReachStationTwo();
            Component station = Station(1);
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            Assert.That(GameplayActionTestDriver.Choose(station, CorrectOption(station)), Is.True);
            yield return null;
            Assert.That(ActiveRound(), Is.Null, "Automated routes (tests, replay) skip the round.");
            Assert.That(CurrentStationIndex(), Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ClawGrabsGoldCard_WhenDroppedOverIt()
        {
            yield return LoadAndReachStationTwo();
            Component station = Station(1);
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

            yield return LoadAndReachStationTwo();
            string output = Environment.GetEnvironmentVariable("CYVERSE_LEVEL4_VISUAL_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "VisualQA", "level4-captures");
            Directory.CreateDirectory(output);

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Component station = Station(1);
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
            yield return LoadAndReachStationOne();
            Component station = Station(0);
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            PlayerChooseCorrect(station);
            Component round = ActiveRound();
            Assert.That(round, Is.Not.Null, "A person's correct pick on PHISH FRENZY should start Mail Run.");
            Assert.That(round.GetType().Name, Is.EqualTo("MailRunRound"));
            Assert.That(ChoicePanel(station).activeSelf, Is.False);
            Assert.That(CurrentStationIndex(), Is.EqualTo(1));

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
            yield return LoadAndReachStationOne();
            Component station = Station(0);
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

            yield return LoadAndReachStationOne();
            string output = Environment.GetEnvironmentVariable("CYVERSE_LEVEL4_VISUAL_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "VisualQA", "level4-captures");
            Directory.CreateDirectory(output);
            Camera camera = Camera.main;
            Component station = Station(0);
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

        // ---- Helpers ------------------------------------------------------------------

        private static Type StationType => FindType("Cyverse.Interaction.CyberAttackStation");
        private static Type RoundType() => FindType("Cyverse.UI.ArcadeRound");

        private static IEnumerator LoadAndReachStationOne()
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
        }

        private static IEnumerator LoadAndReachStationTwo()
        {
            yield return LoadAndReachStationOne();
            Component first = Station(0);
            Assert.That(GameplayActionTestDriver.Interact(first), Is.True);
            Assert.That(GameplayActionTestDriver.Choose(first, CorrectOption(first)), Is.True);
            yield return null;
            yield return null;
            Assert.That(CurrentStationIndex(), Is.EqualTo(1));
        }

        private static Component Station(int index)
        {
            PropertyInfo stationIndex = StationType.GetProperty("StationIndex");
            foreach (Object candidate in Object.FindObjectsOfType(StationType))
                if ((int)stationIndex.GetValue(candidate) == index) return candidate as Component;
            Assert.Fail("No Level 4 station " + index);
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
