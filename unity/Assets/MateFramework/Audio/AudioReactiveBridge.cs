using System;
using Mate.Character.Animation;
using Mate.Core;
using Mate.Interfaces;

namespace Mate.Audio
{
    /// <summary>
    /// Event-driven bridge between audio monitoring and dance animation.
    /// Replaces the tight coupling where AvatarAnimatorController directly
    /// accessed PulseAudioManager.Instance.
    /// </summary>
    public class AudioReactiveBridge : IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly IConfiguration _config;
        private readonly SubscriptionToken _peakToken;

        public AudioReactiveBridge(IEventBus eventBus, IConfiguration config)
        {
            _eventBus = eventBus;
            _config = config;
            _peakToken = _eventBus.Subscribe<AudioPeakEvent>(OnPeakLevel);
        }

        private void OnPeakLevel(AudioPeakEvent evt)
        {
            float threshold = _config.GetFloat("soundThreshold", 0.2f);
            if (evt.Level >= threshold)
            {
                _eventBus.Publish(new DanceStartedEvent("dance_audio_reactive"));
            }
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe(_peakToken);
        }
    }
}