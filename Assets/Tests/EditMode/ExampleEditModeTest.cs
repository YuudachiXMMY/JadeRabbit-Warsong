using NUnit.Framework;

namespace JadeRabbit.Tests.EditMode
{
    // 示例 EditMode 单元测试 — 证明测试框架已就绪，可运行、可断言。
    // 命名约定: 文件 [System]_[Feature]_Test.cs; 方法 test_[scenario]_[expected]。
    // CombatCore 程序集落地后，在 EditModeTests.asmdef 的 references 中加入
    // "CombatCore" 并在 CombatCore 侧 [assembly: InternalsVisibleTo("EditModeTests")]
    // 以完成 ADR-D8 的纯 C# NUnit 缝隙测试 spike。
    public class ExampleEditModeTest
    {
        [Test]
        public void test_framework_ready_returns_true()
        {
            Assert.IsTrue(true, "EditMode 测试框架未正确装配");
        }

        [TestCase(2, 3, 5)]
        [TestCase(-1, 1, 0)]
        public void test_deterministic_addition_matches_expected(int a, int b, int expected)
        {
            Assert.AreEqual(expected, a + b);
        }
    }
}
