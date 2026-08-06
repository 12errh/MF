using System.Linq;
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

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Ensure the controller has a layer with a real state machine. If the
            // asset was previously written without a valid layer (state machine
            // fileID 0), recreate it fresh so the idle state serializes.
            if (controller.layers.Length == 0 || controller.layers[0].stateMachine == null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
                AssetDatabase.Refresh();
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            var sm = controller.layers[0].stateMachine;
            sm.name = "Base Layer";

            // Idempotent: if an Idle state already exists, reuse it instead of
            // adding "Idle 0", "Idle 1", ... on every build.
            var idle = sm.states.FirstOrDefault(s => s.state.name == "Idle").state;
            if (idle == null)
            {
                idle = sm.AddState("Idle");
            }
            idle.motion = clip;
            sm.defaultState = idle;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MateAnimatorBuilder] Controller written to {ControllerPath} (state={idle.name}, motion={clip.name})");
        }
    }
}