// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// 本工程为 FMOD 禁用了 Unity 内建音频（Project Settings > Audio > "Disable Unity Audio"）。
// 手感切片用 Unity 音频（AudioSettings.dspTime + AudioSource）当 F2 BeatClock 替身，
// 需临时开启 Unity 音频才能出声/走时钟。这两个菜单项开/关该项目设置（可逆）；
// 测完请用 "Restore" 还原为 FMOD 的禁用态。
using UnityEditor;
using UnityEngine;

namespace JadeRabbit.Slice.EditorTools
{
    public static class SliceAudioSettings
    {
        private const string AudioManagerPath = "ProjectSettings/AudioManager.asset";

        [MenuItem("VerticalSlice/Enable Unity Audio (for slice test)", priority = 20)]
        public static void EnableUnityAudio() => SetDisableAudio(false);

        [MenuItem("VerticalSlice/Restore Unity Audio Disabled (FMOD)", priority = 21)]
        public static void RestoreDisabled() => SetDisableAudio(true);

        /// <summary>项目当前是否禁用了 Unity 内建音频。</summary>
        public static bool IsUnityAudioDisabled()
        {
            var so = LoadAudioManager();
            var p = so?.FindProperty("m_DisableAudio");
            return p != null && p.boolValue;
        }

        private static SerializedObject LoadAudioManager()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(AudioManagerPath);
            if (assets == null || assets.Length == 0 || assets[0] == null)
            {
                Debug.LogError($"[Slice] 无法加载 {AudioManagerPath}");
                return null;
            }
            return new SerializedObject(assets[0]);
        }

        private static void SetDisableAudio(bool disable)
        {
            var so = LoadAudioManager();
            if (so == null) return;
            var p = so.FindProperty("m_DisableAudio");
            if (p == null) { Debug.LogError("[Slice] AudioManager 找不到 m_DisableAudio 字段"); return; }
            p.boolValue = disable;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Slice] Unity 内建音频已{(disable ? "禁用（还原为 FMOD 态）" : "启用（用于切片手感测试）")}。" +
                      (disable ? string.Empty : " 若节拍无声/不走拍，重启一次 Unity Editor 使音频子系统生效。"));
        }
    }
}
