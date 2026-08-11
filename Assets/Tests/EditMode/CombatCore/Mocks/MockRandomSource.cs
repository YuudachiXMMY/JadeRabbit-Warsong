using System.Collections.Generic;
using JadeRabbit.CombatCore.Interfaces;

namespace JadeRabbit.Tests.EditMode.CombatCore.Mocks
{
    /// <summary>
    /// ADR-D8 Mock 契约：<see cref="IRandomSource"/> 的可编排测试替身（纯 C#，禁 using FMOD/TDE/Feel，
    /// 保持 CombatCore 边界隔离）。按注入的返回序列产出 NextDouble()/Next()，使加权抽取结果完全确定，
    /// 支撑 "永不落 Legendary 带无保底" 断言（AC-C3-14 范式）。序列耗尽后返回最后一个值（或 0）。
    /// </summary>
    public sealed class MockRandomSource : IRandomSource
    {
        private readonly Queue<double> _doubles;
        private readonly Queue<int> _ints;
        private double _lastDouble;
        private int _lastInt;

        public MockRandomSource(IEnumerable<double> doubles = null, IEnumerable<int> ints = null)
        {
            _doubles = doubles != null ? new Queue<double>(doubles) : new Queue<double>();
            _ints = ints != null ? new Queue<int>(ints) : new Queue<int>();
            Seed = 0;
        }

        public int Next(int maxExclusive)
        {
            if (_ints.Count > 0) _lastInt = _ints.Dequeue();
            // clamp 到 [0, maxExclusive) 保证契约（编排值越界时取模，仍确定）
            int v = _lastInt % maxExclusive;
            return v < 0 ? v + maxExclusive : v;
        }

        public double NextDouble()
        {
            if (_doubles.Count > 0) _lastDouble = _doubles.Dequeue();
            return _lastDouble;
        }

        public void Reseed(int seed) => Seed = seed;

        public int Seed { get; private set; }
    }
}
