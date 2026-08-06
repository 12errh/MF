using UnityEditor;
using UnityEngine;

namespace Mate.Bootstrap.EditorTools
{
    /// <summary>
    /// Editor tool that generates the default MateDance animation clip. The clip
    /// is a simple procedural bob + sway on the root transform so any humanoid
    /// character can dance out of the box. A dev can supply their own dance clip
    /// instead by setting [animation] dance_animation in mate.toml; this tool
    /// only provides the framework default.
    /// </summary>
    public static class MateDanceBuilder
    {
        private const string ClipPath = "Assets/MateFramework/Animations/MateDance.anim";

        [MenuItem("Tools/Mate/Build Dance Clip")]
        public static void BuildDanceClip()
        {
            var clip = new AnimationClip();
            clip.name = "MateDance";
            clip.frameRate = 30f;

            // Root bob: translate up/down on Y.
            var bobCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.25f, 0.15f),
                new Keyframe(0.5f, 0f),
                new Keyframe(0.75f, -0.15f),
                new Keyframe(1f, 0f));
            clip.SetCurve("", typeof(Transform), "localPosition.y", bobCurve);

            // Root sway: rotate around Y (a gentle dance twist).
            var swayCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 20f),
                new Keyframe(1f, 0f));
            clip.SetCurve("", typeof(Transform), "localEulerAngles.y", swayCurve);

            clip.wrapMode = WrapMode.Loop;
            clip.EnsureQuaternionContinuity();

            AssetDatabase.CreateAsset(clip, ClipPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MateDanceBuilder] Dance clip written to {ClipPath}");
        }
    }
}