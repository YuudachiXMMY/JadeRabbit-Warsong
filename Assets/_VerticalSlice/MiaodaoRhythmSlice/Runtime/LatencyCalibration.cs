// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// FE3 延迟校准雏形：Calibration 阶段收集 TapCount 次任意攻击键敲击的 raw 偏差
// （dsp − 最近【整拍】网格），取均值并 clamp 到 ±200ms，写入 RhythmJudge.CalibrationOffsetSeconds，
// 随后进入 Combat 阶段。
//
// 为何用整拍网格而非半拍：网格最近邻测量的无歧义量程 = grid/2。半拍网格（120BPM 下 250ms）
// 只能测 ±125ms，而无线音频（蓝牙）典型延迟 100–250ms 会被 Math.Round 折到相邻网格，
// 测出【符号与量级都错】的偏移（真实 +180ms → 测得 −70ms）。整拍网格（500ms）可测 ±250ms，
// 覆盖绝大多数无线设备。≥250ms 的设备仍会混叠——已知局限，记录在 REPORT。
// 这直接服务 M0 C 组「无线音频延迟容忍度」判据。
//
// ⚠ 规格层待裁决项（ADR-D15）：生产 C1-F2 存在【双重延迟补偿】。
// F2 已把拍点时间戳搬入玩家感知域（audio-beat-clock.md:300 `adjusted = raw + offset/1000`，
// :309 明示「按键时刻 ≈ raw + offset，参考点 = raw + offset，误差归零」），
// 而 C1-F2（rhythm-core.md:230）又给输入加一次 offset，使完美踩拍的 beat_offset_ms
// 恒等于 latency_offset 而非 0 —— 校准越准，偏差越大。
// rhythm-core.md:259 的「延迟安全约束 |latency_offset| < GoodHalfWindow(120ms)」
// 正是该缺陷的症状压制（把必然 Miss 压回 Good 窗内），并非独立规格；它与
// :238 表列的 [-0.2, 0.2] 及 audio-beat-clock.md:306 的 [-200, 200] 三处互不一致。
//
// 本切片【不复刻】该缺陷：网格取 raw 调度域（SliceMetronome._dspStart + n×interval，不含补偿），
// 判定只做一次 `perceived = inputDsp − bias`，完美踩拍 offset == 0。
// 故本切片可安全使用 ±200ms 量程，且可作为 ADR-D15 的正确参考实现。
using UnityEngine;
using UnityEngine.InputSystem;

namespace JadeRabbit.Slice
{
    [DisallowMultipleComponent]
    public class LatencyCalibration : MonoBehaviour
    {
        [SerializeField] private SliceInput _input;
        [SerializeField] private SliceMetronome _metronome;
        [SerializeField] private RhythmJudge _judge;
        [SerializeField] private SliceRunManager _runManager;

        private const int TapCount = 6;
        // 对齐 rhythm-core.md:238 的 latency_offset_s ∈ [-0.2, 0.2]。
        // 原值 0.12f 会把蓝牙量级的实测偏移截断，使校准对无线设备无效。
        private const float MaxOffsetSeconds = 0.2f;

        public int TapsCollected { get; private set; }
        public int TapsNeeded => TapCount;
        public bool Done { get; private set; }

        private double _sumDelta;

        private void Awake()
        {
            if (_input == null) _input = FindFirstObjectByType<SliceInput>();
            if (_metronome == null) _metronome = FindFirstObjectByType<SliceMetronome>();
            if (_judge == null) _judge = FindFirstObjectByType<RhythmJudge>();
            if (_runManager == null) _runManager = FindFirstObjectByType<SliceRunManager>();
        }

        private void OnEnable()
        {
            if (_input != null) _input.OnAttack += HandleTap;
        }

        private void OnDisable()
        {
            if (_input != null) _input.OnAttack -= HandleTap;
        }

        private void Update()
        {
            if (Done) return;
            if (_runManager != null && _runManager.CurrentPhase != SliceRunManager.Phase.Calibration) return;

            var kb = Keyboard.current;
            if (kb != null && kb.enterKey.wasPressedThisFrame)
                Finish(0f);
        }

        private void HandleTap(AttackType type, double dsp)
        {
            if (Done) return;
            if (_metronome == null || !_metronome.IsStartConfirmed) return; // 等首拍确认后才收 tap
            if (_runManager != null && _runManager.CurrentPhase != SliceRunManager.Phase.Calibration) return;

            // beatMode: true = 整拍网格。必须与 HUD 文案「跟着整拍敲」一致，见文件头量程说明。
            double grid = _metronome.NearestGridDsp(dsp, true);
            _sumDelta += dsp - grid;
            TapsCollected++;

            if (TapsCollected >= TapCount)
            {
                float offset = (float)(_sumDelta / TapsCollected);
                Finish(offset);
            }
        }

        private void Finish(float offsetSeconds)
        {
            offsetSeconds = Mathf.Clamp(offsetSeconds, -MaxOffsetSeconds, MaxOffsetSeconds);
            if (_judge != null) _judge.CalibrationOffsetSeconds = offsetSeconds;
            Done = true;
            if (_runManager != null) _runManager.BeginCombat();
        }
    }
}
