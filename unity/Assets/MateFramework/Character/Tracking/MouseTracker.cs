using System;
using Mate.Core;
using Mate.Interfaces;
using UnityEngine;

namespace Mate.Character.Tracking
{
    /// <summary>
    /// Computes normalized head/eye/spine blend values from the cursor offset from
    /// screen center. Sensitivities are read from IConfiguration. Mouse position is
    /// provided by an injected source so tests can exercise Update() without a real cursor.
    /// </summary>
    public class MouseTracker : IMouseTracker
    {
        private readonly IConfiguration _config;
        private readonly IEventBus _eventBus;
        private readonly Func<Vector2> _mousePosition;
        private readonly Func<Vector2> _screenSize;

        private float _headBlend;
        private float _eyeBlend;
        private float _spineBlend;
        private float _headYaw;
        private float _headPitch;
        private float _eyeYaw;
        private float _eyePitch;
        private float _spineYaw;

        public MouseTracker(IConfiguration config, IEventBus eventBus,
            Func<Vector2> mousePosition = null, Func<Vector2> screenSize = null)
        {
            _config = config;
            _eventBus = eventBus;
            _mousePosition = mousePosition ?? (() => (Vector2)Input.mousePosition);
            _screenSize = screenSize ?? (() => new Vector2(Screen.width, Screen.height));
        }

        public MouseBlendValues GetBlendValues()
        {
            return new MouseBlendValues
            {
                HeadBlend = Mathf.Clamp01(_headBlend),
                EyeBlend = Mathf.Clamp01(_eyeBlend),
                SpineBlend = Mathf.Clamp01(_spineBlend),
                HeadYaw = Mathf.Clamp(_headYaw, -1f, 1f),
                HeadPitch = Mathf.Clamp(_headPitch, -1f, 1f),
                EyeYaw = Mathf.Clamp(_eyeYaw, -1f, 1f),
                EyePitch = Mathf.Clamp(_eyePitch, -1f, 1f),
                SpineYaw = Mathf.Clamp(_spineYaw, -1f, 1f),
            };
        }

        public void Update()
        {
            float headSensitivity = _config.GetFloat("headSensitivity", 1.0f);
            float eyeSensitivity = _config.GetFloat("eyeSensitivity", 1.0f);
            float spineSensitivity = _config.GetFloat("spineSensitivity", 0.5f);

            var screen = _screenSize();
            if (screen.x <= 0f || screen.y <= 0f)
            {
                _headBlend = _eyeBlend = _spineBlend = 0f;
                _headYaw = _headPitch = _eyeYaw = _eyePitch = _spineYaw = 0f;
                return;
            }

            var center = screen * 0.5f;
            var delta = _mousePosition() - center;

            // Signed direction, normalized to the half-screen extent, scaled by
            // sensitivity, then clamped to -1..1. The magnitude fields remain the
            // absolute value of the signed values (backward compatible).
            _headYaw = Mathf.Clamp(delta.x / center.x * headSensitivity, -1f, 1f);
            _headPitch = Mathf.Clamp(delta.y / center.y * eyeSensitivity, -1f, 1f);
            _eyeYaw = Mathf.Clamp(delta.x / center.x * eyeSensitivity, -1f, 1f);
            _eyePitch = Mathf.Clamp(delta.y / center.y * eyeSensitivity, -1f, 1f);
            _spineYaw = Mathf.Clamp(delta.x / center.x * spineSensitivity, -1f, 1f);

            _headBlend = Mathf.Clamp01(Mathf.Abs(_headYaw));
            _eyeBlend = Mathf.Clamp01(Mathf.Abs(_headPitch));
            _spineBlend = Mathf.Clamp01(Mathf.Abs(_spineYaw));
        }
    }
}