// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// F4 苗刀挥击的【切片实现】：代码驱动的弧形挥砍（非 Animator 状态机），用非绑帧时长模拟
// Animancer AttackState.Speed = currentBpm/authoredBpm 的等价效果——
//   duration = baseDuration × authoredBpm / currentBpm
// 严禁绑死攻击动画帧数（技术偏好文档硬约束）。
//
// EC15：挥击进度用 Time.unscaledDeltaTime 驱动——Perfect 冻帧（Time.timeScale=0）期间玩家挥击
// 动画时钟不得被冻结，否则 registry `freeze_zeroes_player_attack_speed` 判定失败。
using System.Collections;
using UnityEngine;

namespace JadeRabbit.Slice
{
    [DisallowMultipleComponent]
    public class MiaodaoSwing : MonoBehaviour
    {
        [SerializeField] private Transform _blade;
        [SerializeField] private BeatEnemy _enemy;

        [Header("命中判定")]
        [SerializeField] private float _hitRange = 2.2f;
        [SerializeField] private float _hitWindowStart = 0.3f;
        [SerializeField] private float _hitWindowEnd = 0.7f;
        [SerializeField] private float _dashLungeDistance = 1.5f;

        private const float BaseDamage = 25f;

        public bool IsSwinging { get; private set; }

        private Renderer _bladeRenderer;
        private Coroutine _swingRoutine;
        private bool _hasHitThisSwing;

        private void Awake()
        {
            if (_blade == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Blade";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(0.15f, 0.15f, 1.2f);
                go.transform.localPosition = new Vector3(0f, 1f, 0.7f);
                _blade = go.transform;
            }

            _bladeRenderer = _blade.GetComponent<Renderer>();
            if (_enemy == null) _enemy = FindFirstObjectByType<BeatEnemy>();
        }

        /// <summary>播放一次挥击。currentBpm 用于按当前节奏动态缩放挥击时长（禁止绑死帧数）。</summary>
        public void Play(AttackType type, RhythmJudgment j, float currentBpm, float authoredBpm = 120f)
        {
            if (_enemy == null) _enemy = FindFirstObjectByType<BeatEnemy>();
            if (_swingRoutine != null) StopCoroutine(_swingRoutine);
            _swingRoutine = StartCoroutine(SwingRoutine(type, j, currentBpm, authoredBpm));
        }

        private IEnumerator SwingRoutine(AttackType type, RhythmJudgment j, float currentBpm, float authoredBpm)
        {
            IsSwinging = true;
            _hasHitThisSwing = false;

            float baseDuration = GetBaseDuration(type);
            float safeBpm = Mathf.Max(1f, currentBpm);
            float duration = Mathf.Max(0.01f, baseDuration * authoredBpm / safeBpm);

            GetArc(type, out float startAngle, out float endAngle);
            SetBladeColor(j.Rating);

            Vector3 dashStart = transform.position;
            Vector3 dashDelta = type == AttackType.Dash ? transform.forward * _dashLungeDistance : Vector3.zero;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);

                if (_blade != null)
                    _blade.localRotation = Quaternion.Euler(0f, Mathf.Lerp(startAngle, endAngle, p), 0f);

                if (type == AttackType.Dash)
                    transform.position = dashStart + dashDelta * p;

                if (!_hasHitThisSwing && p >= _hitWindowStart && p <= _hitWindowEnd)
                    TryHit(j);

                yield return null;
            }

            if (_blade != null)
                _blade.localRotation = Quaternion.Euler(0f, endAngle, 0f);

            IsSwinging = false;
            _swingRoutine = null;
        }

        private void TryHit(RhythmJudgment j)
        {
            if (_enemy == null || !_enemy.IsAlive) return;

            Vector3 toEnemy = _enemy.transform.position - transform.position;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;
            if (dist > _hitRange) return;
            if (dist > 0.0001f && Vector3.Dot(transform.forward, toEnemy.normalized) < 0f) return;

            float dmg = RhythmFormulas.FinalDamage(BaseDamage, j.DamageMultiplier, j.ComboAfter);
            _enemy.TakeDamage(dmg, j.Rating == Rating.Perfect);
            _hasHitThisSwing = true;
        }

        private void SetBladeColor(Rating rating)
        {
            if (_bladeRenderer == null) return;
            Color c = rating == Rating.Perfect ? new Color(1f, 0.84f, 0.1f)
                    : rating == Rating.Good ? new Color(0.85f, 0.85f, 0.92f)
                    : new Color(0.4f, 0.4f, 0.4f);
            _bladeRenderer.material.color = c;
        }

        private static float GetBaseDuration(AttackType type)
        {
            switch (type)
            {
                case AttackType.Heavy: return 0.45f;
                case AttackType.Dash: return 0.3f;
                default: return 0.25f;
            }
        }

        private static void GetArc(AttackType type, out float startAngle, out float endAngle)
        {
            switch (type)
            {
                case AttackType.Heavy:
                    startAngle = -90f; endAngle = 90f; break;
                case AttackType.Dash:
                    startAngle = -20f; endAngle = 20f; break;
                default:
                    startAngle = -50f; endAngle = 50f; break;
            }
        }
    }
}
