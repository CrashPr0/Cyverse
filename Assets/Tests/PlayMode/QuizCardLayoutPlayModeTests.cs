using System;
using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Cyverse.Tests
{
    /// <summary>
    /// The knowledge-check card keeps all of its text inside the panel: the
    /// "EXAM NOT PASSED" review after several wrong answers, long wrong-answer
    /// explanations, and an extreme message all fit, and the body never runs
    /// into the "Press Enter" prompt under it.
    /// </summary>
    public sealed class QuizCardLayoutPlayModeTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator CertExam_AllAnswersWrong_ReviewStaysInsideTheCard()
        {
            SceneManager.LoadScene("Level1_IAM", LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            object quiz = QuizInstance();
            Type quizType = quiz.GetType();
            quizType.GetField("feedbackSeconds").SetValue(quiz, 0.01f);
            quizType.GetField("skipReadDelay").SetValue(quiz, true);

            Type stationType = FindType("Cyverse.Interaction.CertExamStation");
            var station = (Component)UnityEngine.Object.FindObjectOfType(stationType);
            Assert.That(station, Is.Not.Null, "Level 1 should have a certification exam.");
            stationType.GetMethod("Activate").Invoke(station, null);
            stationType.GetMethod("Interact").Invoke(station, new object[] { null });

            PropertyInfo awaitingInput = quizType.GetProperty("IsAwaitingInput");
            PropertyInfo showingMessage = quizType.GetProperty("IsShowingMessage");
            PropertyInfo currentQuestion = quizType.GetProperty("CurrentQuestion");

            int wrong = 0;
            float deadline = Time.realtimeSinceStartup + 40f;
            while (!(bool)showingMessage.GetValue(quiz) && Time.realtimeSinceStartup < deadline)
            {
                if ((bool)awaitingInput.GetValue(quiz))
                {
                    object question = currentQuestion.GetValue(quiz);
                    int correct = (int)question.GetType().GetField("correctIndex").GetValue(question);
                    var options = (string[])question.GetType().GetField("options").GetValue(question);
                    Assert.That(GameplayActionTestDriver.Choose(quiz, (correct + 1) % options.Length), Is.True);
                    wrong++;
                    AssertCardFits(quiz, "after wrong answer " + wrong);
                }
                yield return null;
            }

            Assert.That((bool)showingMessage.GetValue(quiz), Is.True, "Missing every question should show the exam review.");
            Assert.That(wrong, Is.GreaterThanOrEqualTo(3));
            Assert.That(TextField(quiz, "headerText").text, Is.EqualTo("EXAM NOT PASSED"));
            StringAssert.Contains("Missed", TextField(quiz, "bodyText").text);
            AssertCardFits(quiz, "exam review");

            CloseMessage(quiz);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator Message_FarTooLong_ShrinksToFitInsideTheScreen()
        {
            SceneManager.LoadScene("Level1_IAM", LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            object quiz = QuizInstance();
            var body = new StringBuilder();
            body.AppendLine("Score: 0/10   Pass mark: 8");
            body.AppendLine();
            for (int i = 0; i < 8; i++)
            {
                body.AppendLine("- Which of these is the strongest reason to turn on multi-factor authentication for every account?");
                body.AppendLine("  A stolen password alone is no longer enough to sign in, because the attacker also needs the second factor.");
            }
            body.AppendLine("Retry the missed questions to reach the pass mark.");

            bool done = false;
            Action onDone = () => done = true;
            quiz.GetType().GetMethod("ShowMessage").Invoke(quiz, new object[] { "EXAM NOT PASSED", body.ToString(), onDone });
            Assert.That((bool)quiz.GetType().GetProperty("IsShowingMessage").GetValue(quiz), Is.True);
            yield return null;
            AssertCardFits(quiz, "very long message");

            CloseMessage(quiz);
            Assert.That(done, Is.True);
        }

        // ---- Helpers ----------------------------------------------------------

        private static void AssertCardFits(object quiz, string when)
        {
            var card = (GameObject)quiz.GetType().GetField("card", Any).GetValue(quiz);
            Text body = TextField(quiz, "bodyText");
            Text feedback = TextField(quiz, "feedbackText");
            var cardRect = (RectTransform)card.transform;
            var canvasRect = (RectTransform)cardRect.parent;

            Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(body.rectTransform.rect.height + 1f),
                $"Body text runs out of its box ({when}).");
            Assert.That(feedback.preferredHeight, Is.LessThanOrEqualTo(feedback.rectTransform.rect.height + 1f),
                $"Feedback text runs out of its strip ({when}).");

            float feedbackTop = feedback.rectTransform.anchoredPosition.y + feedback.rectTransform.rect.height;
            Assert.That(body.rectTransform.offsetMin.y, Is.GreaterThanOrEqualTo(feedbackTop - 0.5f),
                $"Body overlaps the prompt strip ({when}).");

            float half = canvasRect.rect.height * 0.5f;
            float top = cardRect.anchoredPosition.y + cardRect.rect.height * 0.5f;
            float bottom = cardRect.anchoredPosition.y - cardRect.rect.height * 0.5f;
            Assert.That(top, Is.LessThanOrEqualTo(half - 99f), $"Card reaches the objective bar ({when}).");
            Assert.That(bottom, Is.GreaterThanOrEqualTo(-half), $"Card runs off the bottom of the screen ({when}).");
        }

        private static void CloseMessage(object quiz)
        {
            MethodInfo close = quiz.GetType().GetMethod("CloseMessage", Any);
            if ((bool)quiz.GetType().GetProperty("IsShowingMessage").GetValue(quiz)) close.Invoke(quiz, null);
        }

        private static object QuizInstance()
        {
            Type quizType = FindType("Cyverse.Quiz.QuizSystem");
            object quiz = quizType.GetProperty("Instance").GetValue(null);
            Assert.That(quiz, Is.Not.Null, "Level 1 should have a QuizSystem.");
            return quiz;
        }

        private static Text TextField(object quiz, string name) =>
            (Text)quiz.GetType().GetField(name, Any).GetValue(quiz);

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            throw new TypeLoadException("Type not found: " + fullName);
        }
    }
}
