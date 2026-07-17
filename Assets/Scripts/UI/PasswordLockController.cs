using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cyverse.UI
{
    /// <summary>
    /// The game's entry scene: a CyberVerse "secure terminal" that requires a
    /// password before loading the Hub. Two modes:
    ///  - Gate mode (default): a real beta-access gate — the code is NOT shown;
    ///    testers get it from the team. Repeated failures trigger a cooldown.
    ///  - revealPassword: the original educational mode — the password is
    ///    printed on a security memo with a strength lesson; the exercise is
    ///    the authentication ritual, not a memory test.
    /// SECURITY NOTE: this check runs entirely in the client. The string ships
    /// inside the WebGL build (and in this repo's source), so treat it as a
    /// speed bump that keeps casual visitors out of a beta — never as real
    /// protection for anything sensitive. Real gating belongs on the host
    /// (itch.io restricted page, HTTP basic auth, private link).
    /// Fully standalone: builds its own canvas, no HudUI/GameSystems required.
    /// Keyboard line-editor input, Tab toggles masking, Enter submits.
    /// </summary>
    public class PasswordLockController : MonoBehaviour
    {
        public string password = "C1@scg2laC!";
        public string nextScene = "Hub";
        public int maxLength = 24;

        [Tooltip("Print the password on the memo (educational demo mode). " +
                 "Leave off for real gate use, e.g. website beta tests.")]
        public bool revealPassword = false;
        public int lockoutAfterAttempts = 5;
        public float lockoutSeconds = 30f;

        private float lockedUntil;

        private Canvas canvas;
        private Text inputText;
        private Text feedbackText;
        private RectTransform card;
        private string typed = "";
        private bool masked = true;
        private int attempts;
        private bool unlocked;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Build();
            RefreshInput();
        }

        void Update()
        {
            if (unlocked) return;

            if (Time.unscaledTime < lockedUntil)
            {
                int remain = Mathf.CeilToInt(lockedUntil - Time.unscaledTime);
                feedbackText.text =
                    $"<color=#FF8866><b>LOCKED</b></color>   Too many attempts — try again in {remain}s.";
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                masked = !masked;
                RefreshInput();
            }

            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                {
                    if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1);
                }
                else if (c == '\n' || c == '\r')
                {
                    Submit();
                    return;
                }
                else if (!char.IsControl(c) && typed.Length < maxLength)
                {
                    typed += c;
                }
            }
            RefreshInput();
        }

        private void Submit()
        {
            if (typed == password)
            {
                unlocked = true;
                feedbackText.text = "<color=#4CE087><b>ACCESS GRANTED</b></color>   Welcome to CyberVerse.";
                StartCoroutine(LoadNext());
            }
            else
            {
                attempts++;
                typed = "";
                RefreshInput();
                if (lockoutAfterAttempts > 0 && attempts % lockoutAfterAttempts == 0)
                {
                    lockedUntil = Time.unscaledTime + lockoutSeconds;
                    feedbackText.text =
                        $"<color=#FF8866><b>LOCKED</b></color>   Too many attempts — try again in {Mathf.CeilToInt(lockoutSeconds)}s.";
                }
                else if (revealPassword && attempts >= 2)
                    feedbackText.text = "<color=#FF8866><b>ACCESS DENIED</b></color>   Check the security memo — type the password exactly, including symbols.";
                else
                    feedbackText.text = "<color=#FF8866><b>ACCESS DENIED</b></color>   Incorrect access code.";
                StartCoroutine(Shake());
            }
        }

        private IEnumerator LoadNext()
        {
            yield return new WaitForSecondsRealtime(1.4f);
            if (Application.CanStreamedLevelBeLoaded(nextScene))
                SceneManager.LoadScene(nextScene);
            else
                feedbackText.text =
                    $"<color=#FF8866>Scene '{nextScene}' is not in Build Settings — run CyVerse > Add Scenes To Build Settings.</color>";
        }

        private IEnumerator Shake()
        {
            if (card == null) yield break;
            Vector2 basePos = card.anchoredPosition;
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                card.anchoredPosition = basePos + Vector2.right * Mathf.Sin(t * 70f) * 9f * (1f - t / 0.35f);
                yield return null;
            }
            card.anchoredPosition = basePos;
        }

        private void RefreshInput()
        {
            if (inputText == null) return;
            string shown = masked ? new string('•', typed.Length) : typed;
            bool blink = Mathf.Sin(Time.unscaledTime * 6f) > 0f;
            inputText.text = $"PASSWORD:  <color=#5BC8FF>{shown}{(blink && !unlocked ? "_" : " ")}</color>";
        }

        // ---- Construction ----------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("PasswordCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = NewRect("Backdrop", canvas.transform);
            Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = new Color(0.008f, 0.016f, 0.035f, 1f);

            MakeText(canvas.transform, "Title", "CYVERSE", 92, FontStyle.Bold,
                new Color(0.35f, 0.85f, 1f), new Vector2(0, 320), 30);
            MakeText(canvas.transform, "Subtitle",
                revealPassword ? "SECURE TERMINAL — EMPLOYEE ACCESS" : "SECURE TERMINAL — BETA ACCESS",
                26, FontStyle.Normal, new Color(0.85f, 0.92f, 1f), new Vector2(0, 245), 40);

            var memoGo = NewRect("Memo", canvas.transform);
            memoGo.sizeDelta = new Vector2(860, 250);
            memoGo.anchoredPosition = new Vector2(0, 90);
            var memoBg = memoGo.gameObject.AddComponent<Image>();
            memoBg.color = new Color(0.05f, 0.08f, 0.12f, 0.95f);
            var memoOutline = memoGo.gameObject.AddComponent<Outline>();
            memoOutline.effectColor = new Color(0.90f, 0.66f, 0.14f, 0.9f);
            memoOutline.effectDistance = new Vector2(2f, 2f);

            // Educational mode: the password is GIVEN — the exercise is the
            // authentication ritual plus the strength lesson. Gate mode: real
            // beta access — the code is never printed anywhere in the UI.
            string memoBody = revealPassword
                ? "SECURITY MEMO — KEEP PRIVATE\n\n" +
                  $"Your temporary password:   <b><color=#E5A823>{password}</color></b>\n\n" +
                  "<size=20><color=#8FB8CC>Why it's strong: 11 characters mixing upper/lower case, numbers and symbols —\n" +
                  "built from a passphrase: \"CyberVerse Is A Super Cool Game 2 Learn About Cybersecurity!\"</color></size>"
                : "CLOSED BETA\n\n" +
                  "Enter the access code you received from the CyVerse team.\n\n" +
                  "<size=20><color=#8FB8CC>Codes are case-sensitive — type them exactly, including symbols.\n" +
                  "Don't have a code? Contact the project team to join the beta.</color></size>";
            var memo = MakeText(memoGo, "MemoText", memoBody,
                24, FontStyle.Normal, Color.white, Vector2.zero, 0);
            Stretch(memo.rectTransform);

            var cardGo = NewRect("EntryCard", canvas.transform);
            cardGo.sizeDelta = new Vector2(860, 90);
            cardGo.anchoredPosition = new Vector2(0, -110);
            card = cardGo;
            var cardBg = cardGo.gameObject.AddComponent<Image>();
            cardBg.color = new Color(0.02f, 0.04f, 0.07f, 0.95f);
            var cardOutline = cardGo.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.35f, 0.85f, 1f, 0.9f);
            cardOutline.effectDistance = new Vector2(2f, 2f);

            inputText = MakeText(cardGo, "Input", "PASSWORD:  _", 30, FontStyle.Bold,
                Color.white, Vector2.zero, 0);
            Stretch(inputText.rectTransform);

            feedbackText = MakeText(canvas.transform, "Feedback", "", 24, FontStyle.Normal,
                Color.white, new Vector2(0, -190), 40);

            MakeText(canvas.transform, "Hint", "Type the password   ·   ENTER submit   ·   TAB show/hide   ·   BACKSPACE delete",
                20, FontStyle.Normal, new Color(0.45f, 0.55f, 0.68f), new Vector2(0, -250), 30);
            MakeText(canvas.transform, "Credit", "San José State University  ·  CyberVerse Project",
                18, FontStyle.Normal, new Color(0.40f, 0.48f, 0.60f), new Vector2(0, -420), 26);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20, 10);
            rt.offsetMax = new Vector2(-20, -10);
        }

        private Text MakeText(Component parent, string name, string text, int size,
            FontStyle style, Color color, Vector2 pos, float height)
        {
            var rt = NewRect(name, parent.transform);
            if (height > 0)
            {
                rt.anchoredPosition = pos;
                rt.sizeDelta = new Vector2(1600, height + size);
            }
            var t = rt.gameObject.AddComponent<Text>();
            t.font = HudUI.LoadFont();
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;
            return t;
        }
    }
}
