using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Mate.Bootstrap.EditorTools
{
    /// <summary>
    /// Editor tool that builds the minimal AnimatorController used to break the
    /// character out of its T-pose. The controller has a single Idle state bound
    /// to the humanoid idle clip (MateIdle.anim), saved to Resources so the
    /// runtime can load it via Resources.Load. Runs as part of the player build
    /// (see build-player.sh -executeMethod).
    /// </summary>
    public static class MateAnimatorBuilder
    {
        private const string ClipPath = "Assets/MateFramework/Animations/MateIdle.anim";
        private const string ControllerPath = "Assets/MateFramework/Resources/MateIdleController.controller";

        [MenuItem("Tools/Mate/Build Idle Controller")]
        public static void BuildController()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                Debug.LogError($"[MateAnimatorBuilder] Clip not found at {ClipPath}");
                return;
            }

            // Reuse the existing controller if present.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Reset to a single layer with one Idle state bound to the clip.
            controller.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = "Base Layer",
                    defaultWeight = 1f,
                    stateMachine = CreateIdleStateMachine(clip),
                },
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MateAnimatorBuilder] Controller written to {ControllerPath}");
        }

        private static AnimatorStateMachine CreateIdleStateMachine(AnimationClip clip)
        {
            var sm = new AnimatorStateMachine { name = "Base Layer" };
            var idle = sm.AddState("Idle");
            idle.motion = clip;
            sm.defaultState = idle;
            return sm;
        }
    }
}