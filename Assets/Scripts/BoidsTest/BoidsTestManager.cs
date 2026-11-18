using UnityEngine;

/// <summary>俯视群集测试场。每一步先读取全群状态计算转向，再统一移动。</summary>
public class BoidsTestManager : MonoBehaviour
{
    [SerializeField] Camera viewCamera;
    [SerializeField] Material boidMaterial;
    [SerializeField, Range(0, 6), Tooltip("与贴近的邻居拉开距离")] float separationWeight = 3f;
    [SerializeField, Range(0, 6), Tooltip("逐渐匹配邻居的平均速度")] float alignmentWeight = 1f;
    [SerializeField, Range(0, 6), Tooltip("靠近邻居的中心")] float cohesionWeight = 0.8f;
    [SerializeField, Range(0, 6), Tooltip("向鼠标在地面上的投影转向，设为零可关闭")] float mouseWeight = 1.6f;

    const int Count = 64;
    const float MaxSpeed = 4f;
    const float MaxAcceleration = 8f;
    const float NeighborRadius = 2.6f;
    const float SeparationRadius = 0.9f;

    readonly Vector3[] positions = new Vector3[Count];
    readonly Vector3[] velocities = new Vector3[Count];
    readonly Vector3[] accelerations = new Vector3[Count];
    readonly Transform[] agents = new Transform[Count];
    readonly Plane ground = new Plane(Vector3.up, Vector3.zero);
    Mesh arrow;

    void Awake()
    {
        arrow = new Mesh { name = "Boid" };
        arrow.vertices = new[] { new Vector3(0, 0, 0.36f), new Vector3(0.17f, 0, -0.22f), new Vector3(-0.17f, 0, -0.22f) };
        arrow.triangles = new[] { 0, 1, 2 };
        arrow.RecalculateNormals();
        float spawnRadius = viewCamera.orthographicSize * Mathf.Min(1f, viewCamera.aspect) * 0.72f;
        for (int i = 0; i < Count; i++)
        {
            var boid = new GameObject("Boid " + i);
            boid.transform.SetParent(transform, false);
            boid.AddComponent<MeshFilter>().sharedMesh = arrow;
            boid.AddComponent<MeshRenderer>().sharedMaterial = boidMaterial;
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            positions[i] = new Vector3(offset.x, 0, offset.y);
            Quaternion heading = Quaternion.Euler(0, Random.value * 360f, 0);
            velocities[i] = heading * Vector3.forward * MaxSpeed;
            agents[i] = boid.transform;
            agents[i].SetPositionAndRotation(positions[i], heading);
        }
    }

    void FixedUpdate()
    {
        Vector3? target = MouseTarget();
        for (int i = 0; i < Count; i++)
            accelerations[i] = Steering(i, target);

        float halfHeight = viewCamera.orthographicSize;
        float halfWidth = halfHeight * viewCamera.aspect;
        for (int i = 0; i < Count; i++)
        {
            Vector3 velocity = Vector3.ClampMagnitude(velocities[i] + accelerations[i] * Time.fixedDeltaTime, MaxSpeed);
            Vector3 position = positions[i] + velocity * Time.fixedDeltaTime;
            // 在画面边缘折返，即使鼠标移出也能继续观察群集。
            ReflectAtEdge(ref position.x, ref velocity.x, halfWidth);
            ReflectAtEdge(ref position.z, ref velocity.z, halfHeight);
            positions[i] = position;
            velocities[i] = velocity;
            agents[i].position = position;
            if (velocity.sqrMagnitude > 0.2f)
                agents[i].rotation = Quaternion.LookRotation(velocity);
        }
    }

    void OnDestroy()
    {
        if (arrow != null) Destroy(arrow);
    }

    Vector3? MouseTarget()
    {
        Vector3 pointer = Input.mousePosition;
        if (!Application.isFocused || !viewCamera.pixelRect.Contains(pointer)) return null;
        Ray ray = viewCamera.ScreenPointToRay(pointer);
        if (ground.Raycast(ray, out float distance)) return ray.GetPoint(distance);
        return null;
    }

    Vector3 Steering(int index, Vector3? target)
    {
        Vector3 separation = Vector3.zero; // 分离量
        Vector3 velocitySum = Vector3.zero; // 速度量
        Vector3 offsetSum = Vector3.zero; // 接近量
        int neighbors = 0;

        for (int j = 0; j < Count; j++)
        {
            if (j == index) continue;
            Vector3 offset = positions[j] - positions[index];
            float distanceSq = offset.sqrMagnitude;
            if (distanceSq > NeighborRadius * NeighborRadius) continue;
            velocitySum += velocities[j];
            offsetSum += offset;
            neighbors++;
            if (distanceSq < SeparationRadius * SeparationRadius)
                separation -= offset / Mathf.Max(distanceSq, 0.0001f);
        }

        Vector3 force = separation * separationWeight;
        if (neighbors > 0)
        {
            force += (velocitySum / neighbors - velocities[index]) * alignmentWeight;
            force += offsetSum / neighbors * cohesionWeight;
        }
        if (target.HasValue)
        {
            Vector3 desiredVelocity = Vector3.ClampMagnitude(target.Value - positions[index], MaxSpeed);
            force += (desiredVelocity - velocities[index]) * mouseWeight;
        }
        return Vector3.ClampMagnitude(force, MaxAcceleration);
    }

    static void ReflectAtEdge(ref float position, ref float velocity, float extent)
    {
        if (position > extent)
        {
            position = extent;
            velocity = -Mathf.Abs(velocity);
        }
        else if (position < -extent)
        {
            position = -extent;
            velocity = Mathf.Abs(velocity);
        }
    }
}
