using System.Threading.Tasks;
using Mate.AI;
using Mate.Audio;
using Mate.Character;
using Mate.Character.Animation;
using Mate.Character.Tracking;
using Mate.Core;
using Mate.Interfaces;
using Mate.Mods;
using Mate.Platform;
using Mate.System;
using Mate.Window;

namespace Mate.Bootstrap
{
    /// <summary>
    /// Composition root: registers every service into a MateContext backed by
    /// the project's mate.toml. Adapters (IVrmLoader/IPulseAudio) are injectable
    /// so the wiring is testable without a scene; defaults use the real grabbed
    /// monoliths.
    /// </summary>
    public static class BootstrapComposer
    {
        public sealed class Adapters
        {
            public IVrmLoader VrmLoader;
            public IPulseAudio PulseAudio;
            public IWindowBackend WindowBackend;
        }

        /// <summary>Compose a fully wired MateContext for a project directory.</summary>
        public static MateContext Compose(string projectDir, Adapters adapters = null)
        {
            var ctx = new MateContext();
            var config = new MateTomlConfig(projectDir);
            var bus = new SimpleEventBus();

            ctx.RegisterSingleton<IConfiguration>(config);
            ctx.RegisterSingleton<IEventBus>(bus);

            var vrm = adapters?.VrmLoader ?? new VrmLoaderAdapter();
            var pulse = adapters?.PulseAudio;
            // The window backend is the ported X11 implementation; tests inject
            // a fake so native calls never run in EditMode tests.
            var windowBackend = adapters?.WindowBackend ?? new X11WindowBackend();
            // Stateful services are singletons so model/audio state persists
            // across resolves (the bootstrap and consumers share one instance).
            ctx.RegisterSingleton<ICharacterService>(new CharacterService(config, bus, vrm));
            ctx.RegisterSingleton<IMouseTracker>(new MouseTracker(config, bus));
            ctx.RegisterSingleton<IAnimationService>(new CharacterAnimator(config, bus));
            ctx.RegisterSingleton<IAudioService>(new PulseAudioService(config, bus, pulse));
            ctx.RegisterSingleton<ISystemService>(new SystemTrayService(config, bus));
            ctx.RegisterSingleton<IAIService>(new OllamaProvider(config, bus));
            ctx.RegisterSingleton<IModService>(new ModService());
            ctx.RegisterSingleton<IWindowService>(new WindowService(windowBackend, config));

            // Cross-module bridge: audio peaks trigger dance events. Registered
            // so MateContext.Dispose runs its IDisposable.Dispose (unsubscribe).
            ctx.RegisterSingleton<AudioReactiveBridge>(new AudioReactiveBridge(bus, config));

            return ctx;
        }

        /// <summary>Load the project's configured model, if any.</summary>
        public static async Task LoadConfiguredModelAsync(MateContext ctx, string projectDir)
        {
            var config = ctx.Resolve<IConfiguration>();
            var modelPath = config.GetString("modelPath", string.Empty);
            if (string.IsNullOrEmpty(modelPath))
                return;

            var character = ctx.Resolve<ICharacterService>();
            var fullPath = modelPath;
            if (!global::System.IO.Path.IsPathRooted(modelPath))
                fullPath = global::System.IO.Path.Combine(projectDir, modelPath);

            var result = await character.LoadModel(fullPath);
            if (!result.IsSuccess)
                UnityEngine.Debug.LogWarning($"Could not load model '{modelPath}': {result.Error}");
        }
    }
}