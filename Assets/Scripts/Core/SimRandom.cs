using System;

namespace Hollow.Core
{
    /// <summary>
    /// The single seedable gameplay RNG (xorshift128+). Its two state words are saved with the game,
    /// so a loaded game continues the exact same random sequence.
    /// </summary>
    [Serializable]
    public sealed class SimRandom
    {
        public ulong S0;
        public ulong S1;

        public SimRandom() : this(1) { }

        public SimRandom(ulong seed)
        {
            Reseed(seed);
        }

        public void Reseed(ulong seed)
        {
            S0 = SplitMix(ref seed);
            S1 = SplitMix(ref seed);
            if (S0 == 0 && S1 == 0) S1 = 1;
        }

        public ulong NextULong()
        {
            ulong x = S0;
            ulong y = S1;
            S0 = y;
            x ^= x << 23;
            S1 = x ^ y ^ (x >> 17) ^ (y >> 26);
            return S1 + y;
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float Value => (NextULong() >> 40) * (1f / (1 << 24));

        public float Range(float minInclusive, float maxExclusive) => minInclusive + (maxExclusive - minInclusive) * Value;

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong span = (ulong)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextULong() % span);
        }

        public SimRandom Clone() => new() { S0 = S0, S1 = S1 };

        static ulong SplitMix(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
