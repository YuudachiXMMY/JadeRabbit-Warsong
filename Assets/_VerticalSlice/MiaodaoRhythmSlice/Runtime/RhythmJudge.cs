// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// C1 节奏量化判定的【切片实现】。忠实实现：
//   C1-F2 偏移公式：beat_offset_ms = (judgment_time − bias − nearest_beat_dsp) × 1000
//     ⚠ 切片与生产【不等价】，勿照抄符号（ADR-D15 待裁决）：
//     本切片网格取 raw 调度域（SliceMetronome._dspStart + n×interval，不含延迟补偿），
//     故只做一次 `− bias` 即落回拍点，完美踩拍 offset == 0。
//     生产 GDD 写作 (judgment_time + latency_offset_s − beat_dsp_time)，但其 beat_dsp_time 已由 F2
//     搬入玩家感知域（audio-beat-clock.md:300/:309），等于补偿了两次——完美踩拍 offset 恒为
//     latency_offset 而非 0。详见 LatencyCalibration.cs 头部与 ADR-D15；
//     移植到生产前必须先确认生产侧时域约定已修正。
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
        // 本轮临时值 100f（GDD authored 为 120f）。原因：120BPM HalfBeat 下网格最近邻的 |offset|
        // 结构性封顶在 grid/2 = 125ms，而 GoodEff = min(120, 124) = 120ms，可达 Miss 区仅 (120,125] = 5ms，
        // 等于 Miss 不可测（第 1 轮试玩实证「全部都为 good」）。改 100f 后 Miss 带 = (100,125] = 25ms。
        // 规格层的根因（Miss 在默认窗口下为空集）另由 ADR 裁决，届时此值回归 authored 来源。
        [SerializeField] private float _goodAuthoredMs = 100f;      // C1 authored Good 半窗（本轮临时 100，见上）
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

        private bool _loggedMissingMetronome;

        private void Awake()
        {
            // 与切片其余 7 个组件保持一致：场景生成器刻意不做手工接线，由组件自解析空引用
            // （契约见 Editor/SliceSceneBuilder.cs 头部）。RhythmJudge 曾是唯一缺此兜底的组件，
            // 导致 _metronome 恒为 null、每次判定走下方兜底分支——第 1 轮试玩失败的直接根因。
            if (_metronome == null) _metronome = FindFirstObjectByType<SliceMetronome>();
        }

        /// <summary>
        /// 判定一次攻击输入。inputDsp = 输入时刻（dsp 域，已由 SliceMetronome.InputTimeToDsp 转换）。
        /// </summary>
        public RhythmJudgment Judge(AttackType attack, double inputDsp)
        {
            // Off 模式无障碍合约（规则 1/7/10）：立即恒 Perfect、offset=0、不依赖时钟就绪、CancelTier 封顶 Standard
            if (_mode == QuantizationMode.Off)
                return Emit(attack, Rating.Perfect, 0f, offMode: true);

            // 响亮失败：绝不把「时钟缺失」伪装成一次成功判定。
            // 原实现返回 Good，使 bug 在玩法层表现为「每一刀都踩准了」，熬过了整轮试玩而未被发现。
            // 返回 Miss 则连击停涨、CancelTier 降为 Closed，手感与日志双重可见。
            if (_metronome == null)
            {
                if (!_loggedMissingMetronome)
                {
                    _loggedMissingMetronome = true; // 节流：Judge 是每次攻击都走的热路径，不做重复栈捕获
                    Debug.LogError(
                        "[RhythmJudge] _metronome 为空（场景未接线，且 Awake 兜底未在场景中找到 SliceMetronome）"
                        + "——节奏判定不可用，后续所有攻击将判为 Miss。", this);
                }
                return Emit(attack, Rating.Miss, 0f, offMode: false);
            }

            bool beatMode = _mode == QuantizationMode.Beat;

            // C1-F2：先把输入时刻折算到「玩家感知域」，再据此找最近网格。
            // 顺序关键：原实现用 raw inputDsp 找网格、却用 perceived 算偏移，两者时基不同；
            // 校准偏移非零时会锚到相邻网格，让本该 Perfect 的输入被判成 Miss
            // （偏移绝对值可达一整个 grid），违反 C1-F3 的判定窗不变量。
            double perceived = inputDsp - _calibrationOffsetSeconds;
            double nearest = _metronome.NearestGridDsp(perceived, beatMode);
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
