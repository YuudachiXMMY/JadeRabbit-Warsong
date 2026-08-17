// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// FE3 延迟校准雏形：Calibration 阶段收集 TapCount 次任意攻击键敲击的 raw 偏差
// （dsp − 最近半拍网格），取均值并 clamp 到 ±120ms，写入 RhythmJudge.CalibrationOffsetSeconds，
// 随后进入 Combat 阶段。C1-F2 延迟安全约束要求校准偏移 < GoodHalfWindow，故此处硬 clamp。
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
        private const float MaxOffsetSeconds = 0.12f;

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

            double grid = _metronome.NearestGridDsp(dsp, false);
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
