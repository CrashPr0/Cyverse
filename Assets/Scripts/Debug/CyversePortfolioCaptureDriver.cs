#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Cyverse.Core;
using Cyverse.Forensics;
using Cyverse.Interaction;
using Cyverse.Level;
using Cyverse.Player;
using Cyverse.Quiz;
using Cyverse.UI;

namespace Cyverse.Testing
{
    /// <summary>
    /// Play Mode driver for the editor capture toolbar. Jumps each level to a
    /// representative state, frames the camera, and writes scaled 1080p PNGs.
    /// Editor-only: this file is compiled out of player builds.
    /// </summary>
    public sealed class CyversePortfolioCaptureDriver : MonoBehaviour
    {
        public static CyversePortfolioCaptureDriver Instance { get; private set; }

        public bool Busy { get; private set; }
        public bool Finished { get; private set; }
        public bool Passed { get; private set; }
        public string Failure { get; private set; }
        public string Status { get; private set; }
        public PortfolioShot CurrentShot { get; private set; }

        private Coroutine running;
        private readonly List<string> failures = new List<string>();

        public static CyversePortfolioCaptureDriver Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Cyverse Portfolio Capture");
            DontDestroyOnLoad(go);
            return go.AddComponent<CyversePortfolioCaptureDriver>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void BeginJump(PortfolioShot shot, bool capture)
        {
            if (shot == null) return;
            Finished = false;
            Passed = false;
            Failure = null;
            StartRoutine(JumpAndMaybeCapture(shot, capture));
        }

        public void BeginCaptureCurrent()
        {
            Finished = false;
            Passed = false;
            Failure = null;
            string name = CurrentShot != null ? CurrentShot.FileName : "current.png";
            StartRoutine(CaptureOnly(name));
        }

        public void BeginCaptureAll()
        {
            Finished = false;
            Passed = false;
            Failure = null;
            failures.Clear();
            StartRoutine(CaptureAllRoutine());
        }

        private void StartRoutine(IEnumerator routine)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Run(routine));
        }

        private IEnumerator Run(IEnumerator routine)
        {
            Busy = true;
            yield return Pump(routine);
            if (string.IsNullOrEmpty(Failure) && failures.Count == 0)
                Passed = true;
            else if (failures.Count > 0)
                Failure = string.Join("; ", failures.ToArray());
            Busy = false;
            Finished = true;
            running = null;
            if (!string.IsNullOrEmpty(Failure))
                Debug.LogError(CyversePortfolioCapture.LogPrefix + " " + Failure);
            else
                Debug.Log(CyversePortfolioCapture.LogPrefix + " Ready.");
        }

        /// <summary>Advances <paramref name="routine"/> one step at a time so
        /// exceptions can be caught without yielding inside a try/catch (CS1626).</summary>
        private IEnumerator Pump(IEnumerator routine)
        {
            while (true)
            {
                object current = null;
                bool moved = false;
                bool failed = false;
                try
                {
                    moved = routine.MoveNext();
                    if (moved)
                        current = routine.Current;
                }
                catch (Exception exception)
                {
                    Failure = exception.Message;
                    Debug.LogException(exception);
                    failed = true;
                }

                if (failed || !moved)
                    yield break;

                // Nested enumerators are pumped here too, so a throw inside
                // LoadScene or PrepareState is still caught on MoveNext.
                IEnumerator nested = current as IEnumerator;
                if (nested != null)
                    yield return Pump(nested);
                else
                    yield return current;
            }
        }

        private IEnumerator CaptureAllRoutine()
        {
            foreach (PortfolioShot shot in CyversePortfolioCapture.Shots)
            {
                yield return JumpAndMaybeCapture(shot, true);
                if (!string.IsNullOrEmpty(Failure))
                {
                    failures.Add(shot.Id + ": " + Failure);
                    Failure = null;
                }
            }

            if (failures.Count == 0)
                Status = "Captured all " + CyversePortfolioCapture.Shots.Length + " shots.";
            else
                Status = "Finished with " + failures.Count + " failure(s).";
            Debug.Log(CyversePortfolioCapture.LogPrefix + " " + Status);
        }

        private IEnumerator CaptureOnly(string fileName)
        {
            Status = "Capturing " + fileName;
            bool ok = false;
            yield return SavePng(Path.Combine(CyversePortfolioCapture.OutputDirectory, fileName),
                written => ok = written);
            if (!ok) Failure = "Could not write " + fileName;
            else Status = "Wrote " + fileName;
        }

        private IEnumerator JumpAndMaybeCapture(PortfolioShot shot, bool capture)
        {
            CurrentShot = shot;
            Status = (capture ? "Capturing " : "Jumping to ") + shot.Title;
            Debug.Log(CyversePortfolioCapture.LogPrefix + " " + Status);

            yield return LoadScene(shot.SceneName);
            DismissTitleAndOnboarding();
            HideTransientUi();
            yield return null;
            yield return PrepareState(shot);
            FrameCamera(shot);
            yield return new WaitForSecondsRealtime(0.35f);

            if (!capture) yield break;

            bool ok = false;
            yield return SavePng(CyversePortfolioCapture.PathFor(shot), written => ok = written);
            if (!ok) Failure = "Could not write " + shot.FileName;
            else
            {
                Status = "Wrote " + shot.FileName;
                Debug.Log(CyversePortfolioCapture.LogPrefix + " " + Status + " → " +
                          CyversePortfolioCapture.PathFor(shot));
            }
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
                throw new InvalidOperationException("Scene is not in Build Settings: " + sceneName);

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (load == null)
                throw new InvalidOperationException("Failed to load scene " + sceneName);
            while (!load.isDone) yield return null;
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.4f);
        }

        private static void DismissTitleAndOnboarding()
        {
            if (MainMenu.Active && MainMenu.Instance != null)
            {
                MethodInfo dismiss = typeof(MainMenu).GetMethod("Dismiss",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                dismiss?.Invoke(MainMenu.Instance, null);
            }
            HubOnboarding.Cancel();
        }

        private static void HideTransientUi()
        {
            GameObject fade = GameObject.Find("FadeCanvas");
            if (fade != null) fade.SetActive(false);
            FirstPersonController.LockCursor(false);
            TaskCelebration.Dismiss();
            if (HudUI.Instance != null) HudUI.Instance.ClearToasts();
        }

        private IEnumerator PrepareState(PortfolioShot shot)
        {
            switch (shot.Id)
            {
                case "hub":
                    yield break;
                case "level1-briefing":
                    PlayBriefing();
                    yield break;
                case "level2-soc-alert-queue":
                    FinishBriefing();
                    yield return null;
                    OpenSocAlertBoard();
                    yield break;
                case "level2-soc-workstations":
                    FinishBriefing();
                    yield break;
                case "level2-evidence-locker":
                    FinishBriefing();
                    yield return null;
                    SealSocLocker();
                    yield break;
                case "level3-imaging-hash":
                    FinishBriefing();
                    if (PlugInStation.Instance != null) PlugInStation.Instance.CompleteNow();
                    yield break;
                case "level3-evidence-explorer":
                    FinishBriefing();
                    if (PlugInStation.Instance != null) PlugInStation.Instance.CompleteNow();
                    OpenEvidenceExplorer();
                    yield break;
                case "level3-knowledge-check":
                    FinishBriefing();
                    ShowKnowledgeCheck();
                    yield break;
                case "level4-spot-the-phish":
                    yield return OpenArcade(Level4CyberAttackContent.StationKind.SpotThePhish);
                    yield break;
                case "level4-phish-frenzy":
                    yield return OpenArcade(Level4CyberAttackContent.StationKind.BypassMfa);
                    yield break;
                case "level4-access-grabber":
                    yield return OpenArcade(Level4CyberAttackContent.StationKind.EscalatePrivileges);
                    yield break;
                case "level4-network-attack":
                    yield return OpenArcade(Level4CyberAttackContent.StationKind.NetworkAttack);
                    yield break;
                case "level4-certificate":
                    FinishBriefing();
                    ShowCertificate();
                    yield break;
            }
        }

        private static void PlayBriefing()
        {
            VideoStation briefing = FindObjectOfType<VideoStation>();
            if (briefing == null) return;
            briefing.Interact(null);
        }

        private static void FinishBriefing()
        {
            VideoStation briefing = FindObjectOfType<VideoStation>();
            if (briefing != null)
                DeterministicGameplayAdapter.FinishBriefing(briefing);
        }

        private static void OpenSocAlertBoard()
        {
            SiemConsole board = FindObjectOfType<SiemConsole>();
            if (board != null) board.Interact(null);
        }

        private static void SealSocLocker()
        {
            EvidenceLocker locker = EvidenceLocker.Instance;
            if (locker == null || locker.side != EvidenceLocker.Side.Soc) return;
            if (locker.IsSealed) return;

            Carryable device = null;
            foreach (Carryable candidate in FindObjectsOfType<Carryable>())
                if (candidate.id == EvidenceLocker.SeizedDeviceId) { device = candidate; break; }
            if (device == null)
                EvidenceLocker.BuildSeizedDevice(locker.transform.TransformPoint(new Vector3(0f, 0.9f, -1.4f)));
            DeterministicGameplayAdapter.SecureSeizedDevice(locker);
        }

        private static void OpenEvidenceExplorer()
        {
            ForensicsConsole console = FindObjectOfType<ForensicsConsole>();
            if (console == null || console.Phone == null || console.ActiveCase == null) return;
            console.Phone.Processed = true;
            if (EvidenceExplorer.Instance == null)
            {
                GameObject host = console.gameObject;
                host.AddComponent<EvidenceExplorer>();
            }
            EvidenceExplorer.Instance.Open(console.Phone, console.ActiveCase);
        }

        private static void ShowKnowledgeCheck()
        {
            if (QuizSystem.Instance == null)
            {
                GameObject host = Level3ForensicsManager.Instance != null
                    ? Level3ForensicsManager.Instance.gameObject
                    : new GameObject("QuizSystem");
                host.AddComponent<QuizSystem>();
            }
            QuizSystem quiz = QuizSystem.Instance;
            quiz.skipReadDelay = true;
            QuizQuestion first = Level3KnowledgeCheck.Questions[0].Shuffled();
            quiz.Ask(first, _ => { }, readDelay: 0f,
                header: "KNOWLEDGE CHECK  1 / " + Level3KnowledgeCheck.Questions.Length);
        }

        private IEnumerator OpenArcade(Level4CyberAttackContent.StationKind kind)
        {
            FinishBriefing();
            yield return null;

            CyberAttackStation target = StationOf(kind);
            if (target == null) yield break;
            yield return CompleteStationsBefore(target);
            yield return null;

            CyberAttackStation.ArcadeRoundsEnabled = true;
            switch (kind)
            {
                case Level4CyberAttackContent.StationKind.SpotThePhish:
                    ArcadeRound.Launch<SpotPhishRound>(null);
                    break;
                case Level4CyberAttackContent.StationKind.BypassMfa:
                    ArcadeRound.Launch<MailRunRound>(null);
                    break;
                case Level4CyberAttackContent.StationKind.EscalatePrivileges:
                    ArcadeRound.Launch<ClawDropRound>(null);
                    break;
                case Level4CyberAttackContent.StationKind.NetworkAttack:
                    ArcadeRound.Launch<NetworkAttackRound>(null);
                    break;
            }
            yield return null;
            if (ArcadeRound.Active != null) ArcadeRound.Active.StartForTest();
        }

        private static IEnumerator CompleteStationsBefore(CyberAttackStation target)
        {
            Level4CyberAttackManager manager = Level4CyberAttackManager.Instance;
            if (manager == null || target == null) yield break;
            CyberAttackStation[] stations = FindObjectsOfType<CyberAttackStation>();
            for (int i = 0; i < target.StationIndex; i++)
            {
                CyberAttackStation station = StationAt(stations, i);
                if (station == null) continue;
                Level4CyberAttackContent.StationScenario scenario = manager.ScenarioFor(station);
                if (scenario == null) continue;
                GameplayActions.TryApply(station, GameplayAction.Interact());
                yield return null;
                GameplayActions.TryApply(station, GameplayAction.Choose(scenario.correctOption));
                yield return null;
            }
        }

        private static CyberAttackStation StationOf(Level4CyberAttackContent.StationKind kind)
        {
            foreach (CyberAttackStation station in FindObjectsOfType<CyberAttackStation>())
                if (station.Kind == kind) return station;
            return null;
        }

        private static CyberAttackStation StationAt(CyberAttackStation[] stations, int index)
        {
            foreach (CyberAttackStation station in stations)
                if (station.StationIndex == index) return station;
            return null;
        }

        private static void ShowCertificate()
        {
            for (int level = 1; level <= 4; level++)
                if (!LevelProgress.IsCompleted(level))
                    LevelProgress.MarkCompleted(level);

            if (ResultsScreen.Instance == null)
            {
                GameObject host = Level4CyberAttackManager.Instance != null
                    ? Level4CyberAttackManager.Instance.gameObject
                    : Instance.gameObject;
                host.AddComponent<ResultsScreen>();
            }

            ResultsScreen results = ResultsScreen.Instance;
            results.Show(1280, 4, 4, 12f * 60f,
                "LEVEL 4 COMPLETE",
                "SIMULATION COMPLETE — DATA STOLEN 100% / CAUGHT 0%",
                "Campaign complete — replay to improve your stealth score.",
                "Level 4",
                1280);
            results.ShowCertificate();
        }

        // Helper so ShowCertificate can be static-ish without capturing this incorrectly
        // when Instance is used; kept private and only used above via a local function
        // would be cleaner. This is a fallback host.
        private void FrameCamera(PortfolioShot shot)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            WorldTextScreenshotTour.PrepareCaptureCamera(camera);

            switch (shot.Id)
            {
                case "hub":
                    Place(camera, new Vector3(-4f, 4.2f, -11f), new Vector3(10f, 1.6f, 4f));
                    break;
                case "level1-briefing":
                    WorldTextScreenshotTour.TryApplyView(camera, "briefing");
                    break;
                case "level2-soc-alert-queue":
                    WorldTextScreenshotTour.TryApplyView(camera, "alert");
                    break;
                case "level2-soc-workstations":
                    WorldTextScreenshotTour.TryApplyView(camera, "workstations");
                    break;
                case "level2-evidence-locker":
                    WorldTextScreenshotTour.TryApplyView(camera, "locker");
                    break;
                case "level3-imaging-hash":
                    WorldTextScreenshotTour.TryApplyView(camera, "upload");
                    break;
                case "level3-evidence-explorer":
                case "level3-knowledge-check":
                    WorldTextScreenshotTour.TryApplyView(camera, "forensics");
                    break;
                case "level4-spot-the-phish":
                    FrameStation(camera, Level4CyberAttackContent.StationKind.SpotThePhish);
                    break;
                case "level4-phish-frenzy":
                    FrameStation(camera, Level4CyberAttackContent.StationKind.BypassMfa);
                    break;
                case "level4-access-grabber":
                    FrameStation(camera, Level4CyberAttackContent.StationKind.EscalatePrivileges);
                    break;
                case "level4-network-attack":
                    FrameStation(camera, Level4CyberAttackContent.StationKind.NetworkAttack);
                    break;
                case "level4-certificate":
                    Place(camera, new Vector3(0f, 3.2f, -6f), new Vector3(0f, 1.6f, 6f));
                    break;
            }

            if (WorldTextLayoutManager.Instance != null)
            {
                WorldTextLayoutManager.Instance.RefreshNow();
                WorldTextLayoutManager.Instance.EvaluateNow();
            }
        }

        private static void FrameStation(Camera camera, Level4CyberAttackContent.StationKind kind)
        {
            CyberAttackStation station = StationOf(kind);
            if (station == null)
            {
                Place(camera, new Vector3(0f, 3.2f, -6f), new Vector3(0f, 1.6f, 6f));
                return;
            }
            Vector3 target = station.transform.position + Vector3.up * 1.55f;
            Vector3 position = target - station.transform.forward * 3.4f + Vector3.up * 0.35f;
            Place(camera, position, target);
        }

        private static void Place(Camera camera, Vector3 position, Vector3 target)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(target - position, Vector3.up);
        }

        private IEnumerator SavePng(string path, Action<bool> done)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            yield return new WaitForEndOfFrame();

            int scale = CyversePortfolioCapture.Scale;
            int targetWidth = CyversePortfolioCapture.ScaledWidth;
            int targetHeight = CyversePortfolioCapture.ScaledHeight;

            Texture2D source = null;
            Texture2D sized = null;
            bool written = false;
            var restore = BeginScaledOverlayRender(targetWidth, targetHeight);
            // Give CanvasScaler a frame to pick up the RenderTexture pixel size
            // (camera.pixelWidth can still be the Game View size on the same frame).
            yield return null;
            yield return new WaitForEndOfFrame();
            try
            {
                source = FinishScaledOverlayRender(restore, targetWidth, targetHeight);
                if (source == null) source = TryScreenshotTexture(scale);
                if (source == null)
                    throw new InvalidOperationException("No graphics device available to capture.");

                sized = EnsureSize(source, targetWidth, targetHeight);
                File.WriteAllBytes(path, sized.EncodeToPNG());
                written = File.Exists(path) && new FileInfo(path).Length > 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(CyversePortfolioCapture.LogPrefix + " Capture failed: " + exception.Message);
            }
            finally
            {
                RestoreScaledOverlayRender(restore);
                if (source != null) Destroy(source);
                if (sized != null && sized != source) Destroy(sized);
            }
            done(written);
        }

        private struct OverlayCanvasState
        {
            public Canvas canvas;
            public Camera originalCamera;
            public float originalPlaneDistance;
        }

        private struct ScalerBackup
        {
            public CanvasScaler scaler;
            public CanvasScaler.ScaleMode mode;
            public float scaleFactor;
        }

        private class ScaledRenderState
        {
            public Camera camera;
            public RenderTexture target;
            public RenderTexture previousActive;
            public RenderTexture previousCameraTarget;
            public List<OverlayCanvasState> overlayStates = new List<OverlayCanvasState>();
            public List<ScalerBackup> scalerBackups = new List<ScalerBackup>();
            public bool prepared;
        }

        /// <summary>
        /// Points the main camera at a scaled RenderTexture and switches active
        /// ScreenSpaceOverlay canvases (plus their CanvasScalers) to render into
        /// it at a ConstantPixelSize of <c>width / 1920</c>, so 1080p layout is
        /// preserved at 2x and 3x. Call <see cref="FinishScaledOverlayRender"/>
        /// after one frame, then <see cref="RestoreScaledOverlayRender"/>.
        /// </summary>
        private static ScaledRenderState BeginScaledOverlayRender(int width, int height)
        {
            var state = new ScaledRenderState();
            Camera camera = Camera.main;
            if (camera == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return state;

            state.camera = camera;
            state.previousActive = RenderTexture.active;
            state.previousCameraTarget = camera.targetTexture;
            state.target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            state.target.Create();
            camera.targetTexture = state.target;

            float scaleFactor = width / (float)CyversePortfolioCapture.Width;
            foreach (Canvas canvas in FindObjectsOfType<Canvas>(false))
            {
                if (canvas == null || !canvas.isRootCanvas) continue;
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                state.overlayStates.Add(new OverlayCanvasState
                {
                    canvas = canvas,
                    originalCamera = canvas.worldCamera,
                    originalPlaneDistance = canvas.planeDistance,
                });
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(1f, camera.nearClipPlane + 0.05f);

                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    state.scalerBackups.Add(new ScalerBackup
                    {
                        scaler = scaler,
                        mode = scaler.uiScaleMode,
                        scaleFactor = scaler.scaleFactor,
                    });
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.scaleFactor = scaleFactor;
                }
            }

            state.prepared = true;
            return state;
        }

        private static Texture2D FinishScaledOverlayRender(ScaledRenderState state, int width, int height)
        {
            if (state == null || !state.prepared || state.camera == null || state.target == null)
                return null;

            try
            {
                Canvas.ForceUpdateCanvases();
                Canvas.ForceUpdateCanvases();
                state.camera.Render();

                RenderTexture.active = state.target;
                var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                pixels.Apply();
                return pixels;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void RestoreScaledOverlayRender(ScaledRenderState state)
        {
            if (state == null) return;

            for (int i = state.scalerBackups.Count - 1; i >= 0; i--)
            {
                ScalerBackup backup = state.scalerBackups[i];
                if (backup.scaler != null)
                {
                    backup.scaler.uiScaleMode = backup.mode;
                    backup.scaler.scaleFactor = backup.scaleFactor;
                }
            }
            for (int i = state.overlayStates.Count - 1; i >= 0; i--)
            {
                OverlayCanvasState overlay = state.overlayStates[i];
                if (overlay.canvas != null)
                {
                    overlay.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    overlay.canvas.worldCamera = overlay.originalCamera;
                    overlay.canvas.planeDistance = overlay.originalPlaneDistance;
                }
            }
            if (state.overlayStates.Count > 0)
                Canvas.ForceUpdateCanvases();

            RenderTexture.active = state.previousActive;
            if (state.camera != null) state.camera.targetTexture = state.previousCameraTarget;
            if (state.target != null)
            {
                state.target.Release();
                Destroy(state.target);
            }
        }

        private static Texture2D TryScreenshotTexture(int superSize)
        {
            try
            {
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    return null;
                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture(Mathf.Max(1, superSize));
                if (shot == null || shot.width < 8 || shot.height < 8)
                {
                    if (shot != null) Destroy(shot);
                    return null;
                }
                return shot;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Texture2D EnsureSize(Texture2D source, int width, int height)
        {
            if (source.width == width && source.height == height) return source;

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var sized = new Texture2D(width, height, TextureFormat.RGB24, false);
            sized.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            sized.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return sized;
        }
    }
}
#endif
