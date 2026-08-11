using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JadeRabbit.Tests.PlayMode
{
    // 示例 PlayMode 集成测试 — 证明 Play 模式测试可进入运行时、跨帧断言。
    // 用于多系统交互、物理、协程等需真实场景的测试（见 tests/README.md 证据矩阵）。
    public class ExamplePlayModeTest
    {
        [Test]
        public void test_playmode_scene_bootstraps_without_error()
        {
            var go = new GameObject("SmokeProbe");
            Assert.IsNotNull(go);
            Object.DestroyImmediate(go);
        }

        [UnityTest]
        public IEnumerator test_frame_advances_across_yield()
        {
            int startFrame = Time.frameCount;
            yield return null;
            Assert.Greater(Time.frameCount, startFrame, "PlayMode 未跨帧推进");
        }
    }
}
