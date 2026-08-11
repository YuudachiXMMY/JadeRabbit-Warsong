using System;
using System.Collections.Generic;
using JadeRabbit.CombatCore.Interfaces;

namespace JadeRabbit.CombatCore.RNG
{
    /// <summary>
    /// ADR-D8 §4 / §305 "优先纯 C# 类构造注入" 的最小代表：无 MonoBehaviour 依赖、public 构造直接注入
    /// <see cref="IRandomSource"/>，可在 EditMode NUnit 下确定性测试——不受 InternalsVisibleTo 约束。
    /// 代表 C3 PerkRollService 加权抽取（perk-system.md 公式 4）的可测性形态：给定注入的 RNG 序列，
    /// 选中索引完全确定，可断言"永不落 Legendary 带无保底"（AC-C3-14 范式）。
    /// </summary>
    public sealed class WeightedPicker
    {
        private readonly IRandomSource _rng;

        public WeightedPicker(IRandomSource rng)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        /// <summary>
        /// 按权重比例选一个索引：抽 r = NextDouble()*total，落在第 i 段累积区间即返回 i。
        /// 权重全零或列表空视为非法输入。确定性仅取决于注入的 <see cref="IRandomSource.NextDouble"/> 序列。
        /// </summary>
        public int Pick(IReadOnlyList<float> weights)
        {
            if (weights == null || weights.Count == 0)
                throw new ArgumentException("weights 不可为空", nameof(weights));

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] < 0f)
                    throw new ArgumentException("权重不可为负", nameof(weights));
                total += weights[i];
            }
            if (total <= 0f)
                throw new ArgumentException("权重总和须为正", nameof(weights));

            double r = _rng.NextDouble() * total;
            float acc = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                acc += weights[i];
                if (r < acc)
                    return i;
            }
            return weights.Count - 1; // 浮点边界兜底：r 恰等于 total 时归最后一段
        }
    }
}
