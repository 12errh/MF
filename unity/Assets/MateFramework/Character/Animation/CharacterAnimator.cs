using Mate.Core;
using Mate.Interfaces;

namespace Mate.Character.Animation
{
    /// <summary>
    /// Animation state machine. Dance/idle switch times are read from IConfiguration;
    /// state transitions are published on IEventBus. No direct Animator lookup —
    /// scene wiring is handled by the application layer.
    /// </summary>
    public class CharacterAnimator : IAnimationService
    {
        private readonly IConfiguration _config;
        private readonly IEventBus _eventBus;
        private bool _isDancing;

        public bool IsDancing => _isDancing;
        public float DanceSwitchTime => _config.GetFloat("danceSwitchTime", 15.0f);
        public float IdleSwitchTime => _config.GetFloat("idleSwitchTime", 30.0f);

        public CharacterAnimator(IConfiguration config, IEventBus eventBus)
        {
            _config = config;
            _eventBus = eventBus;
        }

        public void TriggerDance()
        {
            if (_isDancing) return;
            _isDancing = true;

            string danceType = _config.GetString("danceAnimation", "dance_0");
            _eventBus.Publish(new DanceStartedEvent(danceType));
        }

        public void StopDance()
        {
            if (!_isDancing) return;
            _isDancing = false;

            _eventBus.Publish(new DanceStoppedEvent());
        }

        public void SetIdleState(int index)
        {
            _eventBus.Publish(new IdleChangedEvent(index));
        }
    }

    public record DanceStartedEvent(string DanceType);
    public record DanceStoppedEvent();
    public record IdleChangedEvent(int Index);
}