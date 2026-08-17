// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// P1 HUD 的【切片替身】：IMGUI/OnGUI 免资产绘制（生产版将是 UI Toolkit）。
// 淡出计时用 Time.unscaledTime（EC15：HUD 效果同相机一样须用 unscaled，Perfect 冻帧期间仍可读）。
using UnityEngine;

namespace JadeRabbit.Slice
{
    [DisallowMultipleComponent]
    public class RhythmHUD : MonoBehaviour
    {
        [SerializeField] private RhythmJudge _judge;
        [SerializeField] private SliceMetronome _metronome;
        [SerializeField] private SliceRunManager _runManager;
        [SerializeField] private LatencyCalibration _calibration;

        private const float PopupDuration = 0.8f;

        private Rating _lastRating;
        private float _lastOffsetMs;
        private float _popupUntilUnscaled = -1f;

        private GUIStyle _bigStyle;
        private GUIStyle _midStyle;
        private GUIStyle _smallStyle;
        private bool _stylesReady;

        private void Awake()
        {
            if (_judge == null) _judge = FindFirstObjectByType<RhythmJudge>();
            if (_metronome == null) _metronome = FindFirstObjectByType<SliceMetronome>();
            if (_runManager == null) _runManager = FindFirstObjectByType<SliceRunManager>();
            if (_calibration == null) _calibration = FindFirstObjectByType<LatencyCalibration>();
        }

        private void OnEnable()
        {
            if (_judge != null) _judge.OnJudgment += HandleJudgment;
        }

        private void OnDisable()
        {
            if (_judge != null) _judge.OnJudgment -= HandleJudgment;
        }

        private void HandleJudgment(RhythmJudgment j)
        {
            _lastRating = j.Rating;
            _lastOffsetMs = j.OffsetMs;
            _popupUntilUnscaled = Time.unscaledTime + PopupDuration;
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _midStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperLeft };
            _stylesReady = true;
        }

        private void OnGUI()
        {
            EnsureStyles();

            DrawBeatApproach();
            DrawComboAndBpm();
            DrawJudgmentPopup();
            DrawModeLabel();
            DrawPhasePanel();
            DrawControlsHint();
        }

        private void DrawBeatApproach()
        {
            if (_metronome == null) return;

            float w = 400f;
            float x = (Screen.width - w) * 0.5f;
            float y = 20f;
            GUI.Box(new Rect(x, y, w, 16f), GUIContent.none);

            double now = _metronome.DspNow;
            double next = _metronome.NextHalfBeatDsp(now);
            double interval = _metronome.HalfBeatInterval;
            double remaining = next - now;
            float progress = interval > 0.0 ? 1f - Mathf.Clamp01((float)(remaining / interval)) : 0f;

            float markerX = x + w * progress - 3f;
            GUI.Box(new Rect(markerX, y - 4f, 6f, 24f), GUIContent.none);
        }

        private void DrawComboAndBpm()
        {
            if (_judge != null)
            {
                float mult = RhythmFormulas.ComboMultiplier(_judge.CurrentCombo);
                var rect = new Rect(0f, 60f, Screen.width, 60f);
                GUI.Label(rect, $"COMBO {_judge.CurrentCombo}  x{mult:F2}", _bigStyle);
            }

            if (_metronome != null)
            {
                var bpmRect = new Rect(Screen.width - 160f, 10f, 150f, 30f);
                GUI.Label(bpmRect, $"BPM {_metronome.Bpm:F0}", _smallStyle);
            }
        }

        private void DrawJudgmentPopup()
        {
            if (Time.unscaledTime > _popupUntilUnscaled) return;

            float alpha = Mathf.Clamp01((_popupUntilUnscaled - Time.unscaledTime) / PopupDuration);
            Color color = _lastRating == Rating.Perfect ? new Color(1f, 0.85f, 0.1f)
                        : _lastRating == Rating.Good ? new Color(0.85f, 0.85f, 0.9f)
                        : new Color(0.6f, 0.6f, 0.6f);
            color.a = alpha;

            Color prevColor = GUI.color;
            GUI.color = color;
            var rect = new Rect(0f, 130f, Screen.width, 50f);
            string sign = _lastOffsetMs >= 0f ? "+" : "";
            GUI.Label(rect, $"{_lastRating}  {sign}{_lastOffsetMs:F0}ms", _midStyle);
            GUI.color = prevColor;
        }

        private void DrawModeLabel()
        {
            if (_judge == null) return;
            string label = _judge.Mode == QuantizationMode.Off ? "无障碍模式 (Accessibility)" : _judge.Mode.ToString();
            GUI.Label(new Rect(10f, 10f, 320f, 24f), $"模式: {label}", _smallStyle);
        }

        private void DrawPhasePanel()
        {
            if (_runManager == null) return;

            var rect = new Rect(10f, Screen.height - 150f, 480f, 130f);
            GUILayout.BeginArea(rect, GUI.skin.box);

            switch (_runManager.CurrentPhase)
            {
                case SliceRunManager.Phase.Calibration:
                    GUILayout.Label("延迟校准中——请随节拍任意攻击键轻敲……", _smallStyle);
                    if (_calibration != null)
                        GUILayout.Label($"进度 {_calibration.TapsCollected}/{_calibration.TapsNeeded}（按 Enter 跳过）", _smallStyle);
                    break;
                case SliceRunManager.Phase.Combat:
                    GUILayout.Label("战斗中", _smallStyle);
                    break;
                case SliceRunManager.Phase.Resolution:
                    GUILayout.Label($"结算：{_runManager.LastOutcome}", _smallStyle);
                    GUILayout.Label($"Perfect {_runManager.PerfectCount} / Good {_runManager.GoodCount} / Miss {_runManager.MissCount}", _smallStyle);
                    GUILayout.Label($"最大连击 {_runManager.MaxCombo}   Perfect% {_runManager.PerfectRatio * 100f:F0}%", _smallStyle);
                    GUILayout.Label("按 R 再来一局", _smallStyle);
                    break;
            }

            GUILayout.EndArea();
        }

        private void DrawControlsHint()
        {
            var rect = new Rect(10f, Screen.height - 24f, Screen.width - 20f, 22f);
            GUI.Label(rect, "J 轻 / K 重 / L 冲刺 / WASD 移动 / Tab 切档 / [ ] 调 BPM / R 重开", _smallStyle);
        }
    }
}
