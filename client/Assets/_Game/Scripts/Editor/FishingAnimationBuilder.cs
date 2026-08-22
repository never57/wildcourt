using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 钓鱼游动动画构建工具：程序化创建 AC_Fish 控制器与两个摆尾 Clip（关键帧用代码写入）。
    /// </summary>
    public static class FishingAnimationBuilder
    {
        private const string AnimDir = "Assets/_Game/Art/Animation";

        [MenuItem("Tools/Build Fish Animation")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(AnimDir))
                AssetDatabase.CreateFolder("Assets/_Game/Art", "Animation");

            // 两个摆尾 Clip
            var idleClip = CreateSwayClip("Fish_Sway_Idle", 1f, 3f);
            var fastClip = CreateSwayClip("Fish_Sway_Fast", 0.4f, 6f);

            // 控制器
            string controllerPath = AnimDir + "/AC_Fish.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm = controller.layers[0].stateMachine;

            // 参数 Speed (float)
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            // 状态
            var idleState = sm.AddState("Swim_Idle");
            idleState.motion = idleClip;
            var fastState = sm.AddState("Swim_Fast");
            fastState.motion = fastClip;
            sm.defaultState = idleState;

            // 双向过渡，时长 0.15s
            var toFast = idleState.AddTransition(fastState);
            toFast.hasExitTime = false;
            toFast.duration = 0.15f;
            toFast.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");

            var toIdle = fastState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Anim] 生成 AC_Fish.controller + {idleClip.name}/{fastClip.name} 到 {AnimDir}");
        }

        private static AnimationClip CreateSwayClip(string name, float duration, float degrees)
        {
            string path = AnimDir + "/" + name + ".anim";
            AssetDatabase.DeleteAsset(path);

            var clip = new AnimationClip();
            clip.name = name;
            clip.frameRate = 30;

            // rotation.z 正弦摆动：±degrees
            var curve = new AnimationCurve();
            const int keys = 32;
            for (int i = 0; i <= keys; i++)
            {
                float t = duration * i / keys;
                float v = degrees * Mathf.Sin(t / duration * Mathf.PI * 2f);
                curve.AddKey(t, v);
            }
            for (int i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }
            clip.SetCurve("", typeof(RectTransform), "localRotation.z", curve);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
    }
}
