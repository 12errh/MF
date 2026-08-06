using Mate.Interfaces;
using UnityEngine;

namespace Mate.Character.Animation
{
    /// <summary>
    /// IAnimatorDriver that resolves the Animator from the currently loaded
    /// character model at call time, so it works with async model loading.
    /// </summary>
    public class ModelAnimatorDriver : IAnimatorDriver
    {
        private readonly ICharacterService _character;

        public ModelAnimatorDriver(ICharacterService character)
        {
            _character = character;
        }

        private Animator CurrentAnimator
        {
            get
            {
                var model = _character?.CurrentModel;
                return model != null ? model.GetComponentInChildren<Animator>() : null;
            }
        }

        public void PlayDance(string clipName)
        {
            var animator = CurrentAnimator;
            if (animator == null || string.IsNullOrEmpty(clipName))
                return;
            animator.Play(clipName);
        }

        public void PlayIdle()
        {
            var animator = CurrentAnimator;
            if (animator != null)
                animator.Play("Idle");
        }
    }
}