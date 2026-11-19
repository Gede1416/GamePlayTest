using UnityEngine;

namespace BoidsComparison
{
    public sealed class CpuBoidsDemo : MonoBehaviour
    {
        [SerializeField, Min(1), Tooltip("重新启用场景组件后生效")] int count = 512;
        [SerializeField] Material boidMaterial;
        [SerializeField, Range(0, 6), Tooltip("远离过近的邻居")] float separationWeight = 3f;
        [SerializeField, Range(0, 6), Tooltip("朝邻居的平均速度转向")] float alignmentWeight = 1f;
        [SerializeField, Range(0, 6), Tooltip("朝附近邻居的中心转向")] float cohesionWeight = 0.8f;

        BoidSample[] current;
        BoidSample[] next;
        BoidDisplay display;

        void OnEnable()
        {
            current = BoidSample.Create(count);
            next = new BoidSample[count];
            display = new BoidDisplay(count, boidMaterial);
            for (int i = 0; i < count; i++)
                display.Set(i, current[i].position, current[i].velocity);
        }

        void FixedUpdate()
        {
            for (int i = 0; i < current.Length; i++)
            {
                next[i] = Step(i, Time.fixedDeltaTime);
                display.Set(i, next[i].position, next[i].velocity);
            }
            (current, next) = (next, current);
        }

        void LateUpdate() => display.Draw();

        void OnDisable() => display.Release();

        BoidSample Step(int index, float dt)
        {
            BoidSample self = current[index];
            Vector2 separation = Vector2.zero, velocitySum = Vector2.zero, offsetSum = Vector2.zero;
            int neighbors = 0;
            for (int i = 0; i < current.Length; i++)
            {
                if (i == index) continue;
                Vector2 offset = current[i].position - self.position;
                // 只需跨一次边界，所有位置都已被折回 [-9, 9)。
                if (offset.x > 9f) offset.x -= 18f;
                if (offset.x < -9f) offset.x += 18f;
                if (offset.y > 9f) offset.y -= 18f;
                if (offset.y < -9f) offset.y += 18f;
                float distanceSq = offset.sqrMagnitude;
                if (distanceSq > 2.5f * 2.5f) continue;
                if (distanceSq < 1f) separation -= offset / Mathf.Max(distanceSq, 0.01f);
                velocitySum += current[i].velocity;
                offsetSum += offset;
                neighbors++;
            }

            if (neighbors > 0)
            {
                Vector2 alignment = velocitySum / neighbors - self.velocity;
                Vector2 cohesion = offsetSum / neighbors;
                Vector2 v = self.velocity + dt * (separation * separationWeight
                    + alignment * alignmentWeight + cohesion * cohesionWeight);
                if (v.sqrMagnitude > 1.17549435e-38f)
                    self.velocity = v * (2.5f / Mathf.Sqrt(v.sqrMagnitude));
            }
            self.position += self.velocity * dt;
            self.position.x -= 18f * Mathf.Floor(self.position.x / 18f + 0.5f);
            self.position.y -= 18f * Mathf.Floor(self.position.y / 18f + 0.5f);
            return self;
        }
    }
}
