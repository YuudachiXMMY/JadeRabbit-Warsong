// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// PL2 相机微切/顶视跟随的【切片实现】。MicroCut/Shake 的冲击衰减用 Time.unscaledDeltaTime 驱动，
// 使其在 Perfect 冻帧（Time.timeScale=0）期间依然可见（EC15：相机效果同 HUD 一样须用 unscaled）。
using UnityEngine;

namespace JadeRabbit.Slice
{
    [DisallowMultipleComponent]
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 12f, -7f);
        [SerializeField] private float _followLerp = 8f;

        [Header("微切/抖动衰减（unscaled）")]
        [SerializeField] private float _kickDecay = 6f;
        [SerializeField] private float _shakeDecay = 8f;
        [SerializeField] private float _kickPositionAmount = 0.35f;
        [SerializeField] private float _kickPitchDegrees = 3f;

        private Vector3 _kickOffset;
        private Quaternion _kickRotation = Quaternion.identity;
        private float _shakeAmp;

        private void Awake()
        {
            if (_target == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null) _target = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (_target != null)
            {
                Vector3 desired = _target.position + _offset;
                transform.position = Vector3.Lerp(transform.position, desired, _followLerp * Time.deltaTime);

                Vector3 lookDir = _target.position - transform.position;
                if (lookDir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }

            float dt = Time.unscaledDeltaTime;
            _kickOffset = Vector3.Lerp(_kickOffset, Vector3.zero, _kickDecay * dt);
            _kickRotation = Quaternion.Slerp(_kickRotation, Quaternion.identity, _kickDecay * dt);
            _shakeAmp = Mathf.Lerp(_shakeAmp, 0f, _shakeDecay * dt);

            Vector3 shakeOffset = _shakeAmp > 0.0001f
                ? new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * _shakeAmp
                : Vector3.zero;

            transform.position += _kickOffset + shakeOffset;
            transform.rotation *= _kickRotation;
        }

        /// <summary>Perfect 判定时的瞬时镜头冲击（"抽帧"），随后按 unscaled 衰减回归。</summary>
        public void MicroCut(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();

            _kickOffset += dir * _kickPositionAmount;
            _kickRotation = Quaternion.Euler(-_kickPitchDegrees, 0f, 0f) * _kickRotation;
        }

        public void Shake(float amp)
        {
            _shakeAmp = Mathf.Max(_shakeAmp, amp);
        }
    }
}
