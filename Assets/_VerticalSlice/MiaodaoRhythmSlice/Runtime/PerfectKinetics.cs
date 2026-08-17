// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// PL2 Perfect 动力学：Perfect（非 Off 模式）判定同帧齐发冻帧 + 编钟共鸣 + 相机微切。
// 敌人硬直 ×1.3 归 BeatEnemy.TakeDamage(isPerfect) 处理，本类不重复调用 ApplyHitstun，
// 避免双重硬直叠加——Kinetics 只负责表现层。
//
// 遵守 registry `freeze_zeroes_player_attack_speed` 禁令：冻帧（Time.timeScale=0）不冻结玩家
// 挥击时钟，MiaodaoSwing 的挥击进度用 Time.unscaledDeltaTime 驱动（EC15）；本类冻帧协程同样
// 用手动 unscaled 计时循环，确保能在 timeScale=0 期间正确计时并恢复。
using System.Collections;
using UnityEngine;

namespace JadeRabbit.Slice
{
    [DisallowMultipleComponent]
    public class PerfectKinetics : MonoBehaviour
    {
        [SerializeField] private RhythmJudge _judge;
        [SerializeField] private SliceMetronome _metronome;
        [SerializeField] private CameraRig _camera;

        [Tooltip("冻帧时长（约 2-3 帧 @60fps）")]
        [SerializeField] private float _freezeDuration = 0.045f;

        [SerializeField] private float _microCutAmp = 1f;

        private Coroutine _freezeRoutine;
        private float _freezeUntilUnscaled;

        private void Awake()
        {
            if (_judge == null) _judge = FindFirstObjectByType<RhythmJudge>();
            if (_metronome == null) _metronome = FindFirstObjectByType<SliceMetronome>();
            if (_camera == null) _camera = FindFirstObjectByType<CameraRig>();
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
            if (j.Rating != Rating.Perfect || j.OffMode) return;

            FreezeFrame();

            if (_metronome != null) _metronome.PlayChime();

            if (_camera != null)
            {
                Vector3 dir = Random.insideUnitSphere;
                _camera.MicroCut(dir * _microCutAmp);
            }
        }

        private void FreezeFrame()
        {
            _freezeUntilUnscaled = Time.unscaledTime + _freezeDuration;

            if (_freezeRoutine != null)
                return; // 已在冻帧中：只刷新时长，不叠加/嵌套协程

            _freezeRoutine = StartCoroutine(FreezeRoutine());
        }

        private IEnumerator FreezeRoutine()
        {
            float previousScale = Time.timeScale;
            Time.timeScale = 0f;

            while (Time.unscaledTime < _freezeUntilUnscaled)
                yield return null;

            Time.timeScale = previousScale > 0f ? previousScale : 1f;
            _freezeRoutine = null;
        }
    }
}
