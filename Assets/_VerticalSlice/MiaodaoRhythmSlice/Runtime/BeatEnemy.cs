// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// 落拍敌人替身：在小节强拍（downbeat）攻击，提前 1 拍变亮/放大做前摇预告（telegraph），
// 让玩家能「听音乐」预判躲避。调度纯事件驱动（订阅 SliceMetronome.OnHalfBeatScheduled 并记下
// 下次攻击的半拍索引），不在 Update() 里轮询判拍。敌人逻辑用 scaled time——
// Perfect 冻帧（Time.timeScale=0）顺带定住敌人 = 打击顿帧感。
using System.Collections;
using UnityEngine;

namespace JadeRabbit.Slice
{
    [DisallowMultipleComponent]
    public class BeatEnemy : MonoBehaviour
    {
        [SerializeField] private SliceMetronome _metronome;
        [SerializeField] private PlayerController _player;
        [SerializeField] private SliceRunManager _runManager;

        [Header("数值")]
        [SerializeField] private float _maxHp = 120f;
        [SerializeField] private float _attackDamage = 15f;
        [SerializeField] private float _attackRange = 2.5f;
        [SerializeField] private float _baseHitstun = 0.15f;

        private const float TelegraphScale = 1.3f;
        private static readonly Color TelegraphColor = new Color(1f, 0.9f, 0.3f);
        private const float FlashDuration = 0.12f;

        public float MaxHp => _maxHp;
        public float Hp { get; private set; }
        public bool IsAlive { get; private set; } = true;

        private Renderer _renderer;
        private Color _baseColor;
        private Vector3 _baseScale;

        private long _pendingAttackIdx = -1;
        private float _stunUntil;
        private bool _ended;
        private Coroutine _flashRoutine;

        private void Awake()
        {
            if (_metronome == null) _metronome = FindFirstObjectByType<SliceMetronome>();
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_runManager == null) _runManager = FindFirstObjectByType<SliceRunManager>();

            Hp = _maxHp;
            _renderer = GetComponent<Renderer>();
            _baseColor = _renderer != null ? _renderer.material.color : Color.white;
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (_metronome != null) _metronome.OnHalfBeatScheduled += HandleHalfBeat;
        }

        private void OnDisable()
        {
            if (_metronome != null) _metronome.OnHalfBeatScheduled -= HandleHalfBeat;
        }

        private void HandleHalfBeat(long idx, double dsp, bool downbeat)
        {
            if (!IsAlive) return;

            // 命中本轮预告目标（强拍）→ 执行攻击
            if (downbeat && idx == _pendingAttackIdx)
            {
                _pendingAttackIdx = -1;
                ExecuteAttack();
            }

            // 提前 1 拍（2 个半拍）预告下一次强拍攻击：idx%8==6 → 下一强拍 idx+2 恰好 1 拍之后
            if (idx % 8 == 6 && _pendingAttackIdx < 0)
            {
                _pendingAttackIdx = idx + 2;
                BeginTelegraph();
            }
        }

        private void BeginTelegraph()
        {
            if (!IsAlive || Time.time < _stunUntil) return; // 硬直期间不预告新攻击

            transform.localScale = _baseScale * TelegraphScale;
            if (_renderer != null) _renderer.material.color = TelegraphColor;
        }

        private void ExecuteAttack()
        {
            transform.localScale = _baseScale;
            if (_renderer != null) _renderer.material.color = _baseColor;

            if (!IsAlive || Time.time < _stunUntil) return; // 硬直期间攻击被打断
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.transform.position);
            if (dist <= _attackRange)
                _player.TakeDamage(_attackDamage);
        }

        public void TakeDamage(float dmg, bool isPerfect)
        {
            if (!IsAlive) return;

            Hp -= dmg;
            FlashWhite();
            Knockback();
            ApplyHitstun(isPerfect ? _baseHitstun * 1.3f : _baseHitstun);

            if (Hp <= 0f && !_ended)
            {
                _ended = true;
                IsAlive = false;
                if (_runManager != null) _runManager.EndRun(RunOutcome.Victory);
            }
        }

        public void ApplyHitstun(float seconds)
        {
            float until = Time.time + seconds;
            if (until > _stunUntil) _stunUntil = until;
        }

        private void Knockback()
        {
            if (_player == null) return;
            Vector3 away = transform.position - _player.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = Vector3.forward;
            transform.position += away.normalized * 0.15f;
        }

        private void FlashWhite()
        {
            if (_renderer == null) return;
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            float t = 0f;
            while (t < FlashDuration)
            {
                t += Time.deltaTime;
                _renderer.material.color = Color.Lerp(Color.white, _baseColor, t / FlashDuration);
                yield return null;
            }
            _renderer.material.color = _baseColor;
        }
    }
}
