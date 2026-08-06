using System;
using System.IO;
using Mate.Audio;
using Mate.Character;
using Mate.Character.Tracking;
using Mate.Core;
using Mate.Interfaces;
using PulseAudio;
using UnityEngine;

namespace Mate.Bootstrap
{
    /// <summary>
    /// Composition root MonoBehaviour. Creates the scene objects the grabbed
    /// monoliths require (VRMLoader, PulseAudioManager), composes the
    /// MateContext from the project's mate.toml, loads the configured model,
    /// and drives audio polling each frame. Place on a bootstrap GameObject in
    /// the entry scene (Tools/Mate/Create Bootstrap Scene).
    /// </summary>
    public class MateBootstrap : MonoBehaviour
    {
        private MateContext _ctx;
        private IAudioService _audio;
        private IMouseTracker _mouse;
        private IWindowService _window;
        private MouseTrackingApplier _trackingApplier;

        /// <summary>The composed service container (null until Awake).</summary>
        public MateContext Context => _ctx;

        private void Awake()
        {
            var projectDir = BootstrapArgs.ParseProjectPath(Environment.GetCommandLineArgs());

            EnsureCamera();
            EnsureVrmLoader();
            EnsurePulseAudioManager();

            _ctx = BootstrapComposer.Compose(projectDir);
            _audio = _ctx.Resolve<IAudioService>();
            _mouse = _ctx.Resolve<IMouseTracker>();
            _window = _ctx.Resolve<IWindowService>();

            // Drive the cursor-tracking applier so the character's head/spine
            // follow the mouse (MouseTracker computes blends; this applies them).
            _trackingApplier = gameObject.AddComponent<MouseTrackingApplier>();
            _trackingApplier.Bind(
                _ctx.Resolve<IConfiguration>(),
                _mouse,
                _ctx.Resolve<ICharacterService>());

            // Apply mate.toml [window] settings (always-on-top, borderless,
            // click-through, window type, position) to the native window.
            // The handle is IntPtr.Zero: the X11 backend locates the player
            // window by PID itself.
            _ = _window.Initialize(IntPtr.Zero);

            // Fall back to the current model dir if the project path is unusable.
            if (!global::System.IO.Directory.Exists(projectDir))
                projectDir = Application.dataPath;

            // Begin monitoring allowed audio apps so Poll() has nodes to read.
            _audio.DiscoverAndMonitor();

            // Show the system tray icon (config-driven icon + tooltip).
            var system = _ctx.Resolve<ISystemService>();
            var iconPath = _ctx.Resolve<IConfiguration>().GetString("trayIcon", string.Empty);
            var tooltip = _ctx.Resolve<IConfiguration>().GetString("trayTooltip", "My Mate");
            _ = system.ShowTrayIcon(iconPath, tooltip);

            // Load mods (config + asset overrides; no code execution in v1).
            var mods = _ctx.Resolve<IModService>();
            var modsPath = _ctx.Resolve<IConfiguration>().GetString("modsPath", "mods/");
            var modsFullPath = Path.IsPathRooted(modsPath)
                ? modsPath
                : Path.Combine(projectDir, modsPath);
            _ = mods.LoadMods(modsFullPath);

            _ = BootstrapComposer.LoadConfiguredModelAsync(_ctx, projectDir);
        }

        private void Update()
        {
            if (_audio != null)
                _audio.Poll();
            if (_mouse != null)
                _mouse.Update();
            // MouseTrackingApplier runs its own Update() each frame.
        }

        private void OnDestroy()
        {
            _ctx?.Dispose();
            _ctx = null;
        }

        private static Camera EnsureCamera()
        {
            Camera cam;
            if (Camera.main != null)
            {
                cam = Camera.main;
            }
            else
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            // Transparent background (alpha 0) so the window shows the desktop
            // behind the character instead of a black box. Matches the reference
            // scene: SolidColor clear, background alpha 0, orthographic, positioned
            // looking at the model at the origin. Configured unconditionally so a
            // pre-existing scene camera (Bootstrap.unity) is also corrected.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.orthographic = true;
            cam.orthographicSize = 1.1f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 10f;
            // Raise the camera so the character's feet sit near the bottom of the
            // window (a grounded desktop-companion look) rather than floating in
            // the middle. View spans y = cameraY ± orthoSize; y=0 is the model's
            // feet, so cameraY ~ +1.0 puts the feet near the bottom edge.
            cam.transform.position = new Vector3(0f, 1.0f, -3.27f);
            cam.transform.localRotation = Quaternion.identity;
            return cam;
        }

        private static void EnsureVrmLoader()
        {
            var loader = UnityEngine.Object.FindFirstObjectByType<VRMLoader>();
            if (loader == null)
            {
                var go = new GameObject("VRMLoader");
                loader = go.AddComponent<VRMLoader>();
            }
            // The grabbed VRMLoader parents loaded models under customModelOutput
            // (FinalizeLoadedModel). The minimal bootstrap scene has no such node,
            // so create one under the loader to avoid a null reference on load.
            if (loader.customModelOutput == null)
            {
                var output = new GameObject("CustomModelOutput");
                output.transform.SetParent(loader.transform, false);
                loader.customModelOutput = output;
            }
            // Assign the idle AnimatorController so the character is not stuck in
            // its T-pose. The controller asset is built by MateAnimatorBuilder and
            // shipped in Resources.
            if (loader.animatorController == null)
            {
                var idle = Resources.Load<RuntimeAnimatorController>("MateIdleController");
                if (idle != null)
                    loader.animatorController = idle;
            }
        }

        private static void EnsurePulseAudioManager()
        {
            if (UnityEngine.Object.FindFirstObjectByType<PulseAudioManager>() == null)
            {
                var go = new GameObject("PulseAudioManager");
                go.AddComponent<PulseAudioManager>();
            }
        }
    }
}