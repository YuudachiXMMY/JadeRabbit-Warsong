// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// C1 节奏量化判定的【切片实现】。忠实实现：
//   C1-F2 偏移公式：beat_offset_ms = (judgment_time − latency − nearest_beat_dsp) × 1000
//   C1-F3 评级钳位级联：GoodEff = min(GoodAuthored, grid/2−1)；PerfectEff = clamp(...)；坍缩护栏
//   规则 9 后果表：Perfect 1.0×/+2/Advanced；Good 0.75×/+1/Standard；Miss 0.5×/+0(不清零)/Closed
//   规则 1/10 Off 合约：恒 Perfect 但 CancelTier 封顶 Standard
// 切片简化：用「网格最近邻」直接判定（等价于生产的 look-ahead 预缓冲，避开输入锚定过去拍的 bug），
//           不复刻单槽预缓冲/LateWindow 状态机——判定数学等价，手感一致。
using UnityEngine;

namespace JadeRabbit.Slice
{
    [DisallowMultipleComponent]
    public class RhythmJudge : MonoBehaviour
    {
        [SerializeField] private SliceMetronome _metronome;
        [SerializeField] private QuantizationMode _mode = QuantizationMode.HalfBeat;

        [Header("判定窗（authored, ms @ 120BPM）")]
        [SerializeField] private float _perfectAuthoredMs = 50f;   // C1 authored Perfect 半窗
        [SerializeField] private float _goodAuthoredMs = 120f;     // C1 authored Good 半窗
        [Tooltip("平台最小 Perfect 半窗：PC 16.7 / iOS 39 / Android 80")]
        [SerializeField] private float _platformMinPerfectMs = 16.7f;

        [Header("延迟校准（秒；由 LatencyCalibration 写入）")]
        [SerializeField] private float _calibrationOffsetSeconds = 0f;

        public const int ComboMax = 999;
        public int CurrentCombo { get; private set; }
        public QuantizationMode Mode { get => _mode; set => _mode = value; }
        public float CalibrationOffsetSeconds { get => _calibrationOffsetSeconds; set => _calibrationOffsetSeconds = value; }
        public SliceMetronome Metronome { get => _metronome; set => _metronome = value; }

        public event System.Action<RhythmJudgment> OnJudgment;

        /// <summary>
        /// 判定一次攻击输入。inputDsp = 输入时刻（dsp 域，已由 SliceMetronome.InputTimeToDsp 转换）。
        /// </summary>
        public RhythmJudgment Judge(AttackType attack, double inputDsp)
        {
            // Off 模式无障碍合约（规则 1/7/10）：立即恒 Perfect、offset=0、不依赖时钟就绪、CancelTier 封顶 Standard
            if (_mode == QuantizationMode.Off)
                return Emit(attack, Rating.Perfect, 0f, offMode: true);

            if (_metronome == null)
                return Emit(attack, Rating.Good, 0f, offMode: false); // 缺时钟兜底，不阻断试玩

            bool beatMode = _mode == QuantizationMode.Beat;
            double nearest = _metronome.NearestGridDsp(inputDsp, beatMode);

            // C1-F2：判定锚点加延迟补偿后与拍点比较
            double perceived = inputDsp - _calibrationOffsetSeconds;
            float offsetMs = (float)((perceived - nearest) * 1000.0);

            // C1-F3 步骤 1：运行时钳位级联
            double gridMs = (beatMode ? _metronome.BeatInterval : _metronome.HalfBeatInterval) * 1000.0;
            float goodEff = Mathf.Min(_goodAuthoredMs, (float)(gridMs / 2.0 - 1.0));
            float perfectEff = Mathf.Min(_perfectAuthoredMs, goodEff);
            perfectEff = Mathf.Max(perfectEff, _platformMinPerfectMs);
            if (perfectEff > goodEff) goodEff = perfectEff; // E7 坍缩护栏

            // C1-F3 步骤 2：评级
            float abs = Mathf.Abs(offsetMs);
            Rating rating = abs <= perfectEff ? Rating.Perfect
                          : abs <= goodEff ? Rating.Good
                          : Rating.Miss;

            return Emit(attack, rating, offsetMs, offMode: false);
        }

        private RhythmJudgment Emit(AttackType attack, Rating rating, float offsetMs, bool offMode)
        {
            int before = CurrentCombo;

            // C1-F4 连击更新：Perfect +2 / Good +1 / Miss +0（不清零）
            int delta = rating == Rating.Perfect ? 2 : (rating == Rating.Good ? 1 : 0);
            CurrentCombo = Mathf.Clamp(CurrentCombo + delta, 0, ComboMax);

            // 规则 10 CancelTier：Miss→Closed；Good→Standard；Perfect→Advanced（Off 覆盖为 Standard）
            CancelTier tier = rating == Rating.Miss ? CancelTier.Closed
                            : rating == Rating.Good ? CancelTier.Standard
                            : (offMode ? CancelTier.Standard : CancelTier.Advanced);

            // 规则 9 伤害倍率
            float dmgMult = rating == Rating.Perfect ? 1.0f : (rating == Rating.Good ? 0.75f : 0.5f);

            var judgment = new RhythmJudgment(attack, rating, offsetMs, tier, before, CurrentCombo, dmgMult, offMode);
            OnJudgment?.Invoke(judgment);
            return judgment;
        }

        /// <summary>run 结束边界（对齐 C1 SC-2）：清连击。切片在 SliceRunManager.EndRun 调用。</summary>
        public void ResetRun()
        {
            CurrentCombo = 0;
        }
    }
}
