namespace Mate.Interfaces
{
    /// <summary>
    /// Abstraction over the character's Animator so dance reactions are testable
    /// without a real Animator. The clip/state names come from the caller (which
    /// reads them from config), so nothing is hardcoded here.
    /// </summary>
    public interface IAnimatorDriver
    {
        /// <summary>Play the named dance clip/state.</summary>
        void PlayDance(string clipName);
        /// <summary>Return to the idle state.</summary>
        void PlayIdle();
    }
}