using System.Collections.Generic;
using NUnit.Framework;
using JadeRabbit.CombatCore.Interfaces;
using JadeRabbit.CombatCore.RNG;
using JadeRabbit.Tests.EditMode.CombatCore.Mocks;

namespace JadeRabbit.Tests.EditMode.CombatCore
{
    /// <summary>
    /// ADR-D8 "step 1" 缝隙 spike（Migration Plan §6 / Verification Required #2/#3）。
    /// 在纯 C# NUnit EditMode（无 PlayMode、无 FMOD、无场景）下证明四件事，作为 D8 Proposed→Accepted 的前置门：
    ///   1. public 构造注入 IRandomSource → 判定逻辑确定性可测（ADR 推荐路径，不受 InternalsVisibleTo 约束）；
    ///   2. 编排 mock RNG 可断言 "永不落 Legendary 带无保底"（AC-C3-14 范式）；
    ///   3. internal 测试缝隙 setter 经 [assembly: InternalsVisibleTo("EditModeTests")] 跨 asmdef 可见
    ///      （消解 Risk 表 HIGH 项 "CS0122 跨 asmdef 不可见"）——本文件能编译并调用它即为证据；
    ///   4. 生产 SystemRandomSource 在固定种子下可复现（确定性测试规则，coding-standards.md）。
    /// 本套件在 CI（game-ci unity-test-runner, testMode=editmode）跑绿 = "spike 通过"。
    /// </summary>
    public class CombatCore_DiSeamSpike_Test
    {
        // Common / Rare / Legendary 权重带（Legendary 为累积区间 [95,100)）。
        private static IReadOnlyList<float> RarityWeights => new List<float> { 70f, 25f, 5f };
        private const int LegendaryIndex = 2;

        [Test]
        public void test_weighted_pick_construct_injection_is_deterministic()
        {
            // r = 0.5 * 100 = 50 → 落 Common 带 [0,70) → 索引 0
            var common = new WeightedPicker(new MockRandomSource(doubles: new[] { 0.5 }));
            Assert.AreEqual(0, common.Pick(RarityWeights), "r=50 应落 Common(0)");

            // r = 0.99 * 100 = 99 → 落 Legendary 带 [95,100) → 索引 2
            var legendary = new WeightedPicker(new MockRandomSource(doubles: new[] { 0.99 }));
            Assert.AreEqual(LegendaryIndex, legendary.Pick(RarityWeights), "r=99 应落 Legendary(2)");
        }

        [Test]
        public void test_orchestrated_mock_never_hits_legendary_band_no_pity()
        {
            // 编排三次抽取，r 值均 < 95（Legendary 带下界）→ 无论多少次都不该保底给 Legendary
            var mock = new MockRandomSource(doubles: new[] { 0.10, 0.50, 0.949 });
            var picker = new WeightedPicker(mock);

            var picks = new List<int>();
            for (int i = 0; i < 3; i++)
                picks.Add(picker.Pick(RarityWeights));

            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, picks, "确定性序列应为 Common,Common,Rare");
            CollectionAssert.DoesNotContain(picks, LegendaryIndex, "无保底：不应被强行升到 Legendary");
        }

        [Test]
        public void test_internal_test_seam_setter_visible_via_internalsvisibleto()
        {
            var probe = new InternalSeamProbe();
            Assert.AreEqual(-1, probe.RollOnce(), "未注入时返回哨兵 -1");

            // 下一行能编译即证明 InternalsVisibleTo 授予了跨 asmdef 的 internal 访问权（否则 CS0122）。
            probe.SetRandomSourceForTesting(new MockRandomSource(ints: new[] { 42 }));
            Assert.AreEqual(42, probe.RollOnce(), "注入后返回 mock 编排值 42");
        }

        [Test]
        public void test_system_random_source_is_reproducible_under_fixed_seed()
        {
            var rng = new SystemRandomSource(123);
            Assert.AreEqual(123, rng.Seed);

            var first = new List<int> { rng.Next(1000), rng.Next(1000), rng.Next(1000) };
            rng.Reseed(123);
            var second = new List<int> { rng.Next(1000), rng.Next(1000), rng.Next(1000) };

            CollectionAssert.AreEqual(first, second, "同种子重播序列须一致（确定性）");
        }

        [Test]
        public void test_weighted_picker_rejects_empty_weights()
        {
            var picker = new WeightedPicker(new MockRandomSource());
            Assert.Throws<System.ArgumentException>(() => picker.Pick(new List<float>()));
        }
    }
}
