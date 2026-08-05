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
                return;
            }

            var center = screen * 0.5f;
            var delta = _mousePosition() - center;

            _headBlend = Mathf.Clamp01(Mathf.Abs(delta.x) / center.x * headSensitivity);
            _eyeBlend = Mathf.Clamp01(Mathf.Abs(delta.y) / center.y * eyeSensitivity);
            _spineBlend = Mathf.Clamp01(Mathf.Abs(delta.x) / center.x * spineSensitivity);
        }
    }
}