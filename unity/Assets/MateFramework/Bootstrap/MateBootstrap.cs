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
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            return camGo.GetComponent<Camera>();
        }

        private static void EnsureVrmLoader()
        {
            if (UnityEngine.Object.FindFirstObjectByType<VRMLoader>() == null)
            {
                var go = new GameObject("VRMLoader");
                go.AddComponent<VRMLoader>();
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