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
        public float HeadBlend;
        public float EyeBlend;
        public float SpineBlend;
    }
}