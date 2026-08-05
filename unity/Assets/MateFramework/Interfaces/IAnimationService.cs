namespace Mate.Interfaces
{
    /// <summary>Animation state machine: dance triggering and idle selection.</summary>
    public interface IAnimationService
    {
        bool IsDancing { get; }
        float DanceSwitchTime { get; }
        float IdleSwitchTime { get; }
        void TriggerDance();
        void StopDance();
        void SetIdleState(int index);
    }
}