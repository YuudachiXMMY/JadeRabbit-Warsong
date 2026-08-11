namespace JadeRabbit.CombatCore.Interfaces
{
    /// <summary>
    /// 可注入的确定性随机源（ADR-D8 §4）。支撑 C3 PerkRollService 加权无放回抽取
    /// （perk-system.md 公式 4）的确定性单测（AC-C3-13/14）。
    /// 生产实现: <see cref="RNG.SystemRandomSource"/>（封装 System.Random）。
    /// 测试实现: MockRandomSource（可编排返回序列，验证"永不越 Legendary 带"无保底）。
    /// </summary>
    /// <remarks>
    /// 最小充分集：刻意不含 Next(min,max)/NextFloat()/NextBool() 等便捷方法，避免接口膨胀；
    /// C3 加权抽取只需 Next(int) + NextDouble()。未来若有新需求，追加成员即可（不破坏既有 mock）。
    /// </remarks>
    public interface IRandomSource
    {
        /// <summary>[0, maxExclusive) 区间的均匀整数。</summary>
        int Next(int maxExclusive);

        /// <summary>[0.0, 1.0) 区间的均匀双精度。</summary>
        double NextDouble();

        /// <summary>重置种子，保证测试可复现。</summary>
        void Reseed(int seed);

        /// <summary>当前种子（可复现性诊断）。</summary>
        int Seed { get; }
    }
}
