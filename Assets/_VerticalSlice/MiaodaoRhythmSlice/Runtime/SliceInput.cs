// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// F3 输入替身：代码内建 InputAction（不依赖 .inputactions 资产）。攻击回调使用
// InputAction.performed 的精确时间戳（ctx.time）经 SliceMetronome.InputTimeToDsp 转 dsp 域——
// 对齐「禁止 Update() 轮询做节拍判定」「用 InputAction.performed 提供的精确时间戳」两条硬约束。
using UnityEngine;
using UnityEngine.InputSystem;

namespace JadeRabbit.Slice
{
    [DisallowMultipleComponent]
    public class SliceInput : MonoBehaviour
    {
        [SerializeField] private SliceMetronome _metronome;

        public Vector2 MoveValue { get; private set; }
        public bool AttacksEnabled { get; set; } = true;

        /// <summary>攻击触发事件：类型 + 输入时刻（dsp 域，已由 SliceMetronome.InputTimeToDsp 转换）。</summary>
        public event System.Action<AttackType, double> OnAttack;

        private InputAction _moveAction;
        private InputAction _lightAction;
        private InputAction _heavyAction;
        private InputAction _dashAction;

        private void Awake()
        {
            if (_metronome == null) _metronome = FindFirstObjectByType<SliceMetronome>();

            _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.AddBinding("<Gamepad>/leftStick");

            _lightAction = new InputAction("Light", InputActionType.Button, "<Keyboard>/j");
            _lightAction.AddBinding("<Gamepad>/buttonWest");

            _heavyAction = new InputAction("Heavy", InputActionType.Button, "<Keyboard>/k");
            _heavyAction.AddBinding("<Gamepad>/buttonNorth");

            _dashAction = new InputAction("Dash", InputActionType.Button, "<Keyboard>/l");
            _dashAction.AddBinding("<Keyboard>/space");
            _dashAction.AddBinding("<Gamepad>/buttonEast");

            _lightAction.performed += ctx => HandleAttack(AttackType.Light, ctx);
            _heavyAction.performed += ctx => HandleAttack(AttackType.Heavy, ctx);
            _dashAction.performed += ctx => HandleAttack(AttackType.Dash, ctx);
        }

        private void Update()
        {
            MoveValue = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        }

        private void HandleAttack(AttackType type, InputAction.CallbackContext ctx)
        {
            if (!AttacksEnabled) return;
            double dsp = _metronome != null ? _metronome.InputTimeToDsp(ctx.time) : AudioSettings.dspTime;
            OnAttack?.Invoke(type, dsp);
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
            _lightAction?.Enable();
            _heavyAction?.Enable();
            _dashAction?.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _lightAction?.Disable();
            _heavyAction?.Disable();
            _dashAction?.Disable();

            _moveAction?.Dispose();
            _lightAction?.Dispose();
            _heavyAction?.Dispose();
            _dashAction?.Dispose();

            _moveAction = null;
            _lightAction = null;
            _heavyAction = null;
            _dashAction = null;
        }
    }
}
