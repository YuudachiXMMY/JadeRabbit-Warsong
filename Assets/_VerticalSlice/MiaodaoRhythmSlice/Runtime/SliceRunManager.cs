// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// 循环脊柱：Calibration → Combat → Resolution 单向流转。EndRun 为单一出口（幂等，
// 对齐 ADR-D13 单 run-end 出口语义的切片版）。全局快捷键：Tab 切量化档位、[ ] 调 BPM、
// Resolution 阶段 R 重开当前场景。
//
// 偏离说明：设计要求属性字面写作 `Phase Phase { get; }`，但 C# 不允许嵌套类型 Phase 与同名
// 成员 Phase 共存于同一类的声明空间（CS0102: "已经包含 Phase 的定义"）。此处将属性改名为
// `CurrentPhase`，枚举类型名仍为 `Phase`，外部语义等价（SliceRunManager.Phase.Calibration /
// _runManager.CurrentPhase）。RhythmHUD 与 LatencyCalibration 已同步使用 CurrentPhase。
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace JadeRabbit.Slice
{
    [DisallowMultipleComponent]
    public class SliceRunManager : MonoBehaviour
    {
        public enum Phase { Calibration, Combat, Resolution }

        [SerializeField] private SliceInput _input;
        [SerializeField] private RhythmJudge _judge;
        [SerializeField] private SliceMetronome _metronome;

        public Phase CurrentPhase { get; private set; } = Phase.Calibration;
        public RunOutcome LastOutcome { get; private set; }

        public int TotalAttacks { get; private set; }
        public int PerfectCount { get; private set; }
        public int GoodCount { get; private set; }
        public int MissCount { get; private set; }
        public int MaxCombo { get; private set; }

        public float PerfectRatio => TotalAttacks > 0 ? (float)PerfectCount / TotalAttacks : 0f;

        private bool _ended;

        private void Awake()
        {
            if (_input == null) _input = FindFirstObjectByType<SliceInput>();
            if (_judge == null) _judge = FindFirstObjectByType<RhythmJudge>();
            if (_metronome == null) _metronome = FindFirstObjectByType<SliceMetronome>();
        }

        private void OnEnable()
        {
            if (_judge != null) _judge.OnJudgment += HandleJudgment;
        }

        private void OnDisable()
        {
            if (_judge != null) _judge.OnJudgment -= HandleJudgment;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.tabKey.wasPressedThisFrame && _judge != null)
                _judge.Mode = NextMode(_judge.Mode);

            if (kb.leftBracketKey.wasPressedThisFrame && _metronome != null)
                _metronome.SetBpm(_metronome.Bpm - 10f);

            if (kb.rightBracketKey.wasPressedThisFrame && _metronome != null)
                _metronome.SetBpm(_metronome.Bpm + 10f);

            if (CurrentPhase == Phase.Resolution && kb.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void HandleJudgment(RhythmJudgment j)
        {
            if (CurrentPhase != Phase.Combat) return;

            TotalAttacks++;
            switch (j.Rating)
            {
                case Rating.Perfect: PerfectCount++; break;
                case Rating.Good: GoodCount++; break;
                case Rating.Miss: MissCount++; break;
            }
            if (j.ComboAfter > MaxCombo) MaxCombo = j.ComboAfter;
        }

        public void BeginCombat()
        {
            if (CurrentPhase == Phase.Resolution) return;
            CurrentPhase = Phase.Combat;
        }

        /// <summary>run 结束单一出口（幂等）。</summary>
        public void EndRun(RunOutcome outcome)
        {
            if (_ended) return;
            _ended = true;
            LastOutcome = outcome;
            CurrentPhase = Phase.Resolution;
            if (_input != null) _input.AttacksEnabled = false;
        }

        private static QuantizationMode NextMode(QuantizationMode mode)
        {
            switch (mode)
            {
                case QuantizationMode.Off: return QuantizationMode.HalfBeat;
                case QuantizationMode.HalfBeat: return QuantizationMode.Beat;
                default: return QuantizationMode.Off;
            }
        }
    }
}
