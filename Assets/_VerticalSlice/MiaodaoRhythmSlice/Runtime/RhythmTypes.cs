// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
using UnityEngine;

namespace JadeRabbit.Slice
{
    /// <summary>量化档位（对齐 C1 rhythm-core 规则 1）。Off = 无障碍恒 Perfect。</summary>
    public enum QuantizationMode { Off, HalfBeat, Beat }

    /// <summary>攻击类型（苗刀：轻 / 重 / 冲刺）。切片版对齐 F3 AttackType 语义。</summary>
    public enum AttackType { Light, Heavy, Dash }

    /// <summary>节奏评级（C1 rhythm-core 规则 9）。</summary>
    public enum Rating { Perfect, Good, Miss }

    /// <summary>取消权限分级（C1 rhythm-core 规则 10）。Advanced 仅 HalfBeat/Beat 的 Perfect。</summary>
    public enum CancelTier { Advanced, Standard, Closed }

    /// <summary>C1 → 下游广播的判定结果（对齐 RhythmJudgmentEvent 契约的切片子集）。</summary>
    public readonly struct RhythmJudgment
    {
        public readonly AttackType Attack;
        public readonly Rating Rating;
        public readonly float OffsetMs;       // 负=提前拍，正=晚于拍
        public readonly CancelTier CancelTier;
        public readonly int ComboBefore;
        public readonly int ComboAfter;
        public readonly float DamageMultiplier; // 评级倍率 1.0 / 0.75 / 0.5
        public readonly bool OffMode;

        public RhythmJudgment(AttackType attack, Rating rating, float offsetMs, CancelTier cancelTier,
            int comboBefore, int comboAfter, float damageMultiplier, bool offMode)
        {
            Attack = attack;
            Rating = rating;
            OffsetMs = offsetMs;
            CancelTier = cancelTier;
            ComboBefore = comboBefore;
            ComboAfter = comboAfter;
            DamageMultiplier = damageMultiplier;
            OffMode = offMode;
        }
    }

    /// <summary>run 结束出口的结果类型（对齐 ADR-D13 RunOutcome）。</summary>
    public enum RunOutcome { Death, Victory }

    /// <summary>共享数值公式（对齐 C2 combo-system Formulas）。</summary>
    public static class RhythmFormulas
    {
        // combo_multiplier(count) = min(cap, 1 + k·count^p)；k=0.01, p=1.2, cap=5.0（C2 F1 锁定值）
        public const float ComboK = 0.01f;
        public const float ComboP = 1.2f;
        public const float ComboCap = 5.0f;

        public static float ComboMultiplier(int count)
        {
            if (count <= 0) return 1.0f;
            return Mathf.Min(ComboCap, 1.0f + ComboK * Mathf.Pow(count, ComboP));
        }

        // final_damage = base × rating_mult × combo_multiplier × perk_mult（切片 perk_mult=1.0）
        public static float FinalDamage(float baseDamage, float ratingMultiplier, int comboAfter)
        {
            return baseDamage * ratingMultiplier * ComboMultiplier(comboAfter);
        }
    }
}
