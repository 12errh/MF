using System;
using Mate.Core;
using Mate.Interfaces;

namespace Mate.Character.Animation
{
    /// <summary>
    /// Consumes DanceStartedEvent/DanceStoppedEvent and drives the character's
    /// animator to a dance state. The clip name is read from IConfiguration
    /// (`danceAnimation`, default "MateDance") so a dev can supply their own
    /// clip without changing code.
    /// </summary>
    public class DanceReaction : IDisposable
    {
        private IEventBus _eventBus;
        private IConfiguration _config;
        private IAnimatorDriver _driver;
        private readonly SubscriptionToken _startToken;
        private readonly SubscriptionToken _stopToken;

        public DanceReaction(IEventBus eventBus, IConfiguration config, IAnimatorDriver driver)
        {
            _eventBus = eventBus;
            _config = config;
            _driver = driver;
            _startToken = eventBus.Subscribe<DanceStartedEvent>(OnDanceStarted);
            _stopToken = eventBus.Subscribe<DanceStoppedEvent>(OnDanceStopped);
        }

        private void OnDanceStarted(DanceStartedEvent evt)
        {
            string clip = _config.GetString("danceAnimation", "MateDance");
            _driver.PlayDance(clip);
        }

        private void OnDanceStopped(DanceStoppedEvent evt)
        {
            _driver.PlayIdle();
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe(_startToken);
            _eventBus.Unsubscribe(_stopToken);
        }
    }
}