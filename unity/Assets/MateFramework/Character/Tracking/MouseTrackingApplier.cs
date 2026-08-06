using Mate.Core;
using Mate.Interfaces;
using UnityEngine;

namespace Mate.Character.Tracking
{
    /// <summary>
    /// Applies the cursor-tracking blend values computed by IMouseTracker to the
    /// loaded character's head and spine bones. Bone lookup prefers the model's
    /// Animator humanoid bones and falls back to a name search so it works for
    /// both VRM0 and VRM10 models. The rotation limits (max angles) come from
    /// IConfiguration and are not hardcoded.
    /// </summary>
    public class MouseTrackingApplier : MonoBehaviour
    {
        private IConfiguration _config;
        private IMouseTracker _tracker;
        private ICharacterService _character;

        private Transform _head;
        private Transform _spine;
        private GameObject _boundModel;

        /// <summary>Bind the services this applier reads from. Called by the bootstrap.</summary>
        public void Bind(IConfiguration config, IMouseTracker tracker, ICharacterService character)
        {
            _config = config;
            _tracker = tracker;
            _character = character;
        }

        /// <summary>Called by Unity each frame and by tests to drive tracking.</summary>
        public void Update()
        {
            if (_tracker == null || _character == null)
                return;

            var model = _character.CurrentModel;
            if (model == null)
            {
                _head = _spine = null;
                _boundModel = null;
                return;
            }

            if (model != _boundModel)
                BindBones(model);

            if (_head == null)
                return;

            var blends = _tracker.GetBlendValues();
            float headMax = _config?.GetFloat("headMaxAngle", 20f) ?? 20f;
            float spineMax = _config?.GetFloat("spineMaxAngle", 10f) ?? 10f;

            // Head yaw rotates around the model's up (Y) axis; pitch around X.
            _head.localRotation = Quaternion.Euler(
                blends.HeadPitch * headMax,
                blends.HeadYaw * headMax,
                0f);
            if (_spine != null)
                _spine.localRotation = Quaternion.Euler(0f, blends.SpineYaw * spineMax, 0f);
        }

        private void BindBones(GameObject model)
        {
            _boundModel = model;

            var animator = model.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                _head = animator.GetBoneTransform(HumanBodyBones.Head);
                _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            }

            if (_head == null)
                _head = FindBoneByName(model.transform, "Head");
            if (_spine == null)
                _spine = FindBoneByName(model.transform, "Spine");
        }

        private static Transform FindBoneByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith(name, global::System.StringComparison.OrdinalIgnoreCase))
                    return t;
            }
            return null;
        }
    }
}