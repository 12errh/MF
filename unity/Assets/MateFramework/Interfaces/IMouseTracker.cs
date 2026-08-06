namespace Mate.Interfaces
{
    /// <summary>Mouse-driven avatar tracking. Blend values are normalized 0..1.</summary>
    public interface IMouseTracker
    {
        MouseBlendValues GetBlendValues();
        void Update();
    }

    public struct MouseBlendValues
    {
        // Magnitude (0..1) of how far the cursor is from screen center.
        public float HeadBlend;
        public float EyeBlend;
        public float SpineBlend;
        // Signed (-1..1) direction of the cursor offset from screen center.
        // Positive X = cursor right of center; positive Y = cursor above center.
        public float HeadYaw;
        public float HeadPitch;
        public float EyeYaw;
        public float EyePitch;
        public float SpineYaw;
    }
}