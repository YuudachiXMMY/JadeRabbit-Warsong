using JadeRabbit.CombatCore.Interfaces;

namespace JadeRabbit.CombatCore.RNG
{
    /// <summary>
    /// ADR-D8 缝隙 spike 探针：MonoBehaviour 字段注入 setter 模式的**非 MonoBehaviour 缩影**
    /// （<c>rhythm-core.md:846</c> 确立的 "internal SetXForTesting" 模式），用于在纯 C# NUnit 下
    /// 证明 <c>[assembly: InternalsVisibleTo("EditModeTests")]</c> 使跨 asmdef 的 internal setter
    /// 对测试程序集可见（消解 Risk 表 "CS0122 跨 asmdef 不可见 · HIGH"）。
    /// </summary>
    /// <remarks>
    /// 真实 RhythmJudger 会是 MonoBehaviour（无法在 EditMode NUnit 纯构造），其残余 setter 走
    /// 同一 internal + InternalsVisibleTo 机制；此探针把该机制的可见性隔离出来单独验证。
    /// 纯逻辑（如加权抽取）优先走 <see cref="WeightedPicker"/> 的 public 构造注入，不受此约束。
    /// </remarks>
    public sealed class InternalSeamProbe
    {
        private IRandomSource _rng;

        /// <summary>无注入时返回哨兵 -1（模拟 as 静默 null → 首次调用暴露未配置）。</summary>
        public int RollOnce() => _rng != null ? _rng.Next(100) : -1;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 测试缝隙：仅开发/编辑器构建暴露，发布构建无此入口（Verification Required #1）。
        /// internal —— 依赖 CombatCore 的 InternalsVisibleTo 授予 EditModeTests 访问权。
        /// </summary>
        internal void SetRandomSourceForTesting(IRandomSource mock) => _rng = mock;
#endif
    }
}
