using System;
using JadeRabbit.CombatCore.Interfaces;

namespace JadeRabbit.CombatCore.RNG
{
    /// <summary>
    /// <see cref="IRandomSource"/> 生产实现，封装 <see cref="System.Random"/>（ADR-D8 §4）。
    /// 纯 C#、无引擎依赖——可在纯 NUnit EditMode 下直接构造。
    /// </summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        private Random _rng;
        private int _seed;

        public SystemRandomSource(int seed)
        {
            _seed = seed;
            _rng = new Random(seed);
        }

        public int Next(int maxExclusive) => _rng.Next(maxExclusive);

        public double NextDouble() => _rng.NextDouble();

        public void Reseed(int seed)
        {
            _seed = seed;
            _rng = new Random(seed);
        }

        public int Seed => _seed;
    }
}
