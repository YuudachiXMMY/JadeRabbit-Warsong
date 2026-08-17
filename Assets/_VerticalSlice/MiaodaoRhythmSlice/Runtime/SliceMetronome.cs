// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// F2 BeatClock 的【切片替身】。生产版用 FMOD RuntimeManager + getDSPClock；切片改用
// Unity 的 AudioSettings.dspTime（样本精确的音频硬件时钟）+ AudioSource.PlayScheduled，
// 程序化生成 click 音——零 FMOD bank / 零音频素材即可跑出紧致节拍。
//
// 关键正确性点（喂 M0 D 组「技术真值」判据）：
//   1. 判定网格 = _dspStart + n × halfBeatInterval，纯计算、样本精确，不受 Update() 抖动影响。
//   2. click 用 PlayScheduled(dsp) 排程 → 玩家听到的拍点样本精确（非 Update 里 PlayOneShot 的帧抖动）。
//   3. 输入时间戳经 InputTimeToDsp() 从 realtime 域回投到 dsp 域（对齐「用 InputAction.performed 精确时间戳」）。
using UnityEngine;

namespace JadeRabbit.Slice
{
    [DisallowMultipleComponent]
    public class SliceMetronome : MonoBehaviour
    {
        [Header("节拍")]
        [SerializeField] private float _bpm = 120f;
        [Tooltip("时钟启动前的静默延迟（秒），给音频引擎排程余量")]
        [SerializeField] private double _startDelay = 0.6;
        [Tooltip("提前排程窗口（秒）")]
        [SerializeField] private double _scheduleLookahead = 0.15;

        [Header("音量")]
        [Range(0f, 1f)] [SerializeField] private float _beatVolume = 0.55f;
        [Range(0f, 1f)] [SerializeField] private float _halfVolume = 0.25f;

        public float Bpm => _bpm;
        public double BeatInterval => 60.0 / _bpm;          // 一整拍秒数
        public double HalfBeatInterval => 30.0 / _bpm;      // 半拍秒数（60/bpm/2）
        public bool IsStartConfirmed { get; private set; }
        public double DspStart => _dspStart;

        // 半拍索引 idx、其 dsp 时间、是否强拍（每小节首拍）
        public event System.Action<long, double, bool> OnHalfBeatScheduled;

        private double _dspStart;
        private long _nextIndex;
        private AudioSource _srcA, _srcB;
        private AudioSource _oneShotSrc;
        private bool _pingpong;
        private AudioClip _accentClip, _beatClip, _halfClip, _chimeClip;

        private void Awake()
        {
            _accentClip = MakeClick("slice_accent", 1174f, 0.06f);   // 强拍 D6
            _beatClip   = MakeClick("slice_beat",   880f,  0.05f);   // 拍 A5
            _halfClip   = MakeClick("slice_half",   587f,  0.035f);  // 半拍 D5
            _chimeClip  = MakeChime("slice_chime");                  // Perfect 编钟共鸣

            _srcA = CreateSource();
            _srcB = CreateSource();
            _oneShotSrc = CreateSource();
        }

        private AudioSource CreateSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.bypassEffects = true;
            s.bypassListenerEffects = true;
            return s;
        }

        private void Start()
        {
            _dspStart = AudioSettings.dspTime + _startDelay;
            _nextIndex = 0;
            IsStartConfirmed = false;
        }

        private void Update()
        {
            double now = AudioSettings.dspTime;
            if (!IsStartConfirmed && now >= _dspStart)
                IsStartConfirmed = true;

            // 排程 lookahead 内的半拍 click
            while (DspTimeOfIndex(_nextIndex) < now + _scheduleLookahead)
            {
                long idx = _nextIndex;
                double t = DspTimeOfIndex(idx);
                if (t >= now) // 不排程过去的拍
                {
                    bool onBeat = (idx % 2 == 0);
                    bool downbeat = (idx % 8 == 0); // 4 拍一小节 = 8 半拍
                    AudioClip clip = onBeat ? (downbeat ? _accentClip : _beatClip) : _halfClip;
                    float vol = onBeat ? _beatVolume : _halfVolume;

                    AudioSource src = _pingpong ? _srcA : _srcB;
                    _pingpong = !_pingpong;
                    src.clip = clip;
                    src.volume = vol;
                    src.PlayScheduled(t);

                    OnHalfBeatScheduled?.Invoke(idx, t, downbeat);
                }
                _nextIndex++;
            }
        }

        public double DspTimeOfIndex(long halfBeatIndex) => _dspStart + halfBeatIndex * HalfBeatInterval;

        /// <summary>当前 dsp 时间。</summary>
        public double DspNow => AudioSettings.dspTime;

        /// <summary>下一个（未来）半拍的 dsp 时间——供 HUD 逼近指示。</summary>
        public double NextHalfBeatDsp(double dsp)
        {
            long n = (long)System.Math.Floor((dsp - _dspStart) / HalfBeatInterval) + 1;
            if (n < 0) n = 0;
            return _dspStart + n * HalfBeatInterval;
        }

        /// <summary>把输入时刻吸附到最近的网格拍（HalfBeat 或 Beat 模式）。</summary>
        public double NearestGridDsp(double dsp, bool beatMode)
        {
            double interval = beatMode ? BeatInterval : HalfBeatInterval;
            long n = (long)System.Math.Round((dsp - _dspStart) / interval);
            return _dspStart + n * interval;
        }

        /// <summary>
        /// 把 InputAction.CallbackContext.time（Time.realtimeSinceStartup 域）回投到 AudioSettings.dspTime 域。
        /// 修正物理输入与回调派发之间的亚帧延迟——这是切片对 D 组「时间戳来源」判据的核心验证点。
        /// </summary>
        public double InputTimeToDsp(double inputContextTime)
        {
            double delta = inputContextTime - Time.realtimeSinceStartupAsDouble; // 通常 ≤ 0（事件略早于回调）
            return AudioSettings.dspTime + delta;
        }

        /// <summary>运行时改 BPM（测试 BPM 变速用）。切片直接以「当下」为新网格锚点重锚，不做适应窗口。</summary>
        public void SetBpm(float newBpm)
        {
            newBpm = Mathf.Clamp(newBpm, 60f, 240f);
            _bpm = newBpm;
            // 重锚：从下一个安全排程点开始新网格，避免与已排程 click 撞车
            double now = AudioSettings.dspTime;
            _dspStart = now + _scheduleLookahead;
            _nextIndex = 0;
            IsStartConfirmed = true;
        }

        // ---- 程序化音效生成（无素材依赖）----

        private static AudioClip MakeClick(string name, float freq, float durationSec)
        {
            // 本工程为 FMOD 禁用了 Unity 内建音频，AudioSettings.outputSampleRate 会报
            // "Audio system is disabled"。click/chime 用固定 44100 生成即可（播放时 Unity 自动重采样）。
            const int sampleRate = 44100;
            int samples = Mathf.Max(1, (int)(durationSec * sampleRate));
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 45f);                 // 快速衰减包络
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = (i < samples * 0.06f) ? Random.Range(-0.4f, 0.4f) : 0f; // 起音瞬态
                data[i] = Mathf.Clamp((tone * 0.8f + noise) * env, -1f, 1f);
            }
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeChime(string name)
        {
            // 本工程为 FMOD 禁用了 Unity 内建音频，AudioSettings.outputSampleRate 会报
            // "Audio system is disabled"。click/chime 用固定 44100 生成即可（播放时 Unity 自动重采样）。
            const int sampleRate = 44100;
            int samples = Mathf.Max(1, (int)(0.5f * sampleRate));
            var data = new float[samples];
            // 编钟：基频 + 泛音，缓慢衰减
            float f0 = 1568f; // G6
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 6f);
                float s = Mathf.Sin(2f * Mathf.PI * f0 * t) * 0.6f
                        + Mathf.Sin(2f * Mathf.PI * f0 * 2.01f * t) * 0.25f
                        + Mathf.Sin(2f * Mathf.PI * f0 * 2.76f * t) * 0.15f;
                data[i] = Mathf.Clamp(s * env, -1f, 1f);
            }
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Perfect 编钟共鸣层——供 PerfectKinetics 触发。</summary>
        public void PlayChime()
        {
            if (_oneShotSrc != null && _chimeClip != null)
                _oneShotSrc.PlayOneShot(_chimeClip, 0.7f);
        }
    }
}
