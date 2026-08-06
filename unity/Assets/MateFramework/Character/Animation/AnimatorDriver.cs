using Mate.Interfaces;
using UnityEngine;

namespace Mate.Character.Animation
{
    /// <summary>
    /// Default IAnimatorDriver wrapping a Unity Animator. Plays a dance clip by
    /// name (the state must exist on the controller) and returns to Idle.
    /// </summary>
    public class AnimatorDriver : IAnimatorDriver
    {
        private readonly Animator _animator;

        public AnimatorDriver(Animator animator)
        {
            _animator = animator;
        }

        public void PlayDance(string clipName)
        {
            if (_animator == null || string.IsNullOrEmpty(clipName))
                return;
            _animator.Play(clipName);
        }

        public void PlayIdle()
        {
            if (_animator != null)
                _animator.Play("Idle");
        }
    }
}