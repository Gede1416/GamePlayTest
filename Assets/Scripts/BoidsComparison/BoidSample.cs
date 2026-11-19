using UnityEngine;

namespace BoidsComparison
{
    internal struct BoidSample
    {
        internal Vector2 position;
        internal Vector2 velocity;

        internal static BoidSample[] Create(int count)
        {
            var samples = new BoidSample[count];
            uint seed = 42;
            for (int i = 0; i < count; i++)
            {
                float x = NextFloat(ref seed) * 16f - 8f;
                float z = NextFloat(ref seed) * 16f - 8f;
                float angle = NextFloat(ref seed) * Mathf.PI * 2f;
                samples[i].position = new Vector2(x, z);
                samples[i].velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.5f;
            }
            return samples;
        }

        // 与 DOTS 场景的 Mathematics.Random(42) 使用相同序列。
        static float NextFloat(ref uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return (seed >> 9) * (1f / 8388608f);
        }
    }
}
