// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// 玩家编排：串联输入 → 节奏判定 → 连招门控 → 挥击表现，并持有生命值。移动不量化（自由），
// 用 Time.deltaTime（scaled）驱动——EC15 只要求挥击/相机/HUD 时钟保持 unscaled，
// 移动允许随 Perfect 冻帧一起定住，呼应打击顿帧的整体节奏感。
using System.Collections;
using UnityEngine;

namespace JadeRabbit.Slice
{
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private SliceInput _input;
        [SerializeField] private RhythmJudge _judge;
        [SerializeField] private ComboController _combo;
        [SerializeField] private MiaodaoSwing _swing;
        [SerializeField] private SliceMetronome _metronome;
        [SerializeField] private SliceRunManager _runManager;

        [Header("移动")]
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _turnLerp = 10f;

        [Header("生命")]
        [SerializeField] private float _maxHp = 100f;

        public float MaxHp => _maxHp;
        public float Hp { get; private set; }

        private const float FlashDuration = 0.15f;

        private Renderer _renderer;
        private Color _baseColor;
        private Coroutine _flashRoutine;
        private bool _dead;

        private void Awake()
        {
            if (_input == null) _input = FindFirstObjectByType<SliceInput>();
            if (_judge == null) _judge = FindFirstObjectByType<RhythmJudge>();
            if (_combo == null) _combo = FindFirstObjectByType<ComboController>();
            if (_swing == null) _swing = FindFirstObjectByType<MiaodaoSwing>();
            if (_metronome == null) _metronome = FindFirstObjectByType<SliceMetronome>();
            if (_runManager == null) _runManager = FindFirstObjectByType<SliceRunManager>();

            Hp = _maxHp;
            _renderer = GetComponent<Renderer>();
            _baseColor = _renderer != null ? _renderer.material.color : Color.white;
        }

        private void OnEnable()
        {
            if (_input != null) _input.OnAttack += HandleAttack;
        }

        private void OnDisable()
        {
            if (_input != null) _input.OnAttack -= HandleAttack;
        }

        private void Update()
        {
            Vector2 mv = _input != null ? _input.MoveValue : Vector2.zero;
            Vector3 delta = new Vector3(mv.x, 0f, mv.y) * _moveSpeed * Time.deltaTime;
            if (delta.sqrMagnitude > 0.0000001f)
            {
                transform.position += delta;
                Quaternion targetRot = Quaternion.LookRotation(new Vector3(mv.x, 0f, mv.y), Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _turnLerp * Time.deltaTime);
            }
        }

        private void HandleAttack(AttackType type, double dsp)
        {
            if (_judge == null || _swing == null || _metronome == null) return;
            RhythmJudgment j = _judge.Judge(type, dsp);
            _combo?.Submit(j);
            _swing.Play(type, j, _metronome.Bpm);
        }

        public void TakeDamage(float dmg)
        {
            if (_dead) return;

            Hp -= dmg;
            FlashRed();

            if (Hp <= 0f && !_dead)
            {
                _dead = true;
                if (_runManager != null) _runManager.EndRun(RunOutcome.Death);
            }
        }

        private void FlashRed()
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
                t += Time.unscaledDeltaTime;
                _renderer.material.color = Color.Lerp(Color.red, _baseColor, t / FlashDuration);
                yield return null;
            }
            _renderer.material.color = _baseColor;
        }
    }
}
