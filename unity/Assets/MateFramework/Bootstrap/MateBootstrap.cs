using System;
using Mate.Audio;
using Mate.Character;
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

            // Apply mate.toml [window] settings (always-on-top, borderless,
            // click-through, window type, position) to the native window.
            // The handle is IntPtr.Zero: the X11 backend locates the player
            // window by PID itself.
            _ = _window.Initialize(IntPtr.Zero);

            // Fall back to the current model dir if the project path is unusable.
            if (!global::System.IO.Directory.Exists(projectDir))
                projectDir = Application.dataPath;

            _ = BootstrapComposer.LoadConfiguredModelAsync(_ctx, projectDir);
        }

        private void Update()
        {
            if (_audio != null)
                _audio.Poll();
            if (_mouse != null)
                _mouse.Update();
        }

        private void OnDestroy()
        {
            _ctx?.Dispose();
            _ctx = null;
        }

        private static Camera EnsureCamera()
        {
            if (Camera.main != null)
                return Camera.main;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();

            // Transparent background (alpha 0) so the window shows the desktop
            // behind the character instead of a black box. Matches the reference
            // scene: SolidColor clear, background alpha 0, orthographic, positioned
            // looking at the model at the origin.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.orthographic = true;
            cam.orthographicSize = 1.1f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 10f;
            cam.transform.position = new Vector3(0f, 0.27f, -3.27f);
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