using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cyverse.Tests
{
    /// <summary>
    /// The UI motion helpers: tweens run on unscaled time (menus pause the
    /// game), snap to their end when told to, never lose a resting position,
    /// and die quietly with their target.
    /// </summary>
    public sealed class UiMotionPlayModeTests
    {
        private Type motion;
        private GameObject canvas;
        private float timeScaleWas;

        [SetUp]
        public void SetUp()
        {
            motion = FindType("Cyverse.UI.UiMotion");
            timeScaleWas = Time.timeScale;
            canvas = new GameObject("MotionTestCanvas", typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [TearDown]
        public void TearDown()
        {
            Set("Instant", false);
            Set("AnimateInBatch", false);
            Time.timeScale = timeScaleWas;
            Object.Destroy(canvas);
        }

        [UnityTest]
        public IEnumerator Instant_SnapsAPanelInOnTheSpot()
        {
            Set("Instant", true);
            GameObject panel = MakePanel("Panel");
            Call("PanelIn", new[] { typeof(GameObject), typeof(Transform), typeof(float) },
                panel, null, 0.9f);

            Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(panel.transform.localScale, Is.EqualTo(Vector3.one));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tweens_RunWhileTheGameIsPaused()
        {
            Set("AnimateInBatch", true);
            GameObject panel = MakePanel("Panel");
            Call("FadeIn", new[] { typeof(GameObject), typeof(float), typeof(float) }, panel, 0.2f, 0f);
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.EqualTo(0f), "A fade starts from clear.");

            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.4f);
            yield return null;
            Assert.That(group.alpha, Is.EqualTo(1f), "Paused menus must still finish their fades.");
        }

        [UnityTest]
        public IEnumerator SlideIn_TwiceInARow_KeepsTheRestingPosition()
        {
            Set("AnimateInBatch", true);
            var rt = MakePanel("Row").GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(10f, 20f);
            MethodInfo slide = motion.GetMethod("SlideIn");
            slide.Invoke(null, new object[] { rt, new Vector2(0f, -12f), 0.15f, 0f });
            yield return null;
            slide.Invoke(null, new object[] { rt, new Vector2(0f, -12f), 0.15f, 0f });
            yield return new WaitForSecondsRealtime(0.3f);
            yield return null;
            Assert.That(rt.anchoredPosition, Is.EqualTo(new Vector2(10f, 20f)));
        }

        [UnityTest]
        public IEnumerator Tweens_DieWithTheirTarget()
        {
            Set("AnimateInBatch", true);
            GameObject panel = MakePanel("Doomed");
            Call("PanelIn", new[] { typeof(GameObject), typeof(Transform), typeof(float) },
                panel, null, 0.97f);
            Object.Destroy(panel);
            for (int i = 0; i < 20; i++) yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private GameObject MakePanel(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            return go;
        }

        private void Set(string property, bool value) =>
            motion.GetProperty(property).SetValue(null, value);

        private void Call(string method, Type[] signature, params object[] args)
        {
            MethodInfo m = motion.GetMethod(method, signature);
            Assert.That(m, Is.Not.Null, method);
            m.Invoke(null, args);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            Assert.Fail("Type not found: " + fullName);
            return null;
        }
    }
}
