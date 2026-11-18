using UnityEngine;

/// <summary>俯视群集测试场入口，负责造场、鼠标输入和驱动群集。</summary>
public class BoidsTestManager : MonoBehaviour
{
    [SerializeField] Camera viewCamera;
    [SerializeField] Material boidMaterial;
    [SerializeField, Range(0, 6), Tooltip("与贴近的邻居拉开距离")] float separationWeight = 3f;
    [SerializeField, Range(0, 6), Tooltip("逐渐匹配邻居的平均速度")] float alignmentWeight = 1f;
    [SerializeField, Range(0, 6), Tooltip("靠近邻居的中心")] float cohesionWeight = 0.8f;
    [SerializeField, Range(0, 6), Tooltip("向鼠标在地面上的投影转向，设为零可关闭")] float mouseWeight = 1.6f;

    const int Count = 64;
    readonly Plane ground = new Plane(Vector3.up, Vector3.zero);
    Mesh arrow;
    BoidFlock flock;

    void Awake()
    {
        arrow = new Mesh { name = "Boid" };
        arrow.vertices = new[] { new Vector3(0, 0, 0.36f), new Vector3(0.17f, 0, -0.22f), new Vector3(-0.17f, 0, -0.22f) };
        arrow.triangles = new[] { 0, 1, 2 };
        arrow.RecalculateNormals();
        float spawnRadius = viewCamera.orthographicSize * Mathf.Min(1f, viewCamera.aspect) * 0.72f;
        var agents = new BoidAgent[Count];
        for (int i = 0; i < Count; i++)
        {
            var boid = new GameObject("Boid " + i);
            boid.transform.SetParent(transform, false);
            boid.AddComponent<MeshFilter>().sharedMesh = arrow;
            boid.AddComponent<MeshRenderer>().sharedMaterial = boidMaterial;
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 position = new Vector3(offset.x, 0, offset.y);
            Quaternion heading = Quaternion.Euler(0, Random.value * 360f, 0);
            agents[i] = new BoidAgent(boid.transform, position, heading);
        }
        flock = new BoidFlock(agents);
    }

    void FixedUpdate()
    {
        flock.SeparationWeight = separationWeight;
        flock.AlignmentWeight = alignmentWeight;
        flock.CohesionWeight = cohesionWeight;
        flock.MouseWeight = mouseWeight;
        float halfHeight = viewCamera.orthographicSize;
        float halfWidth = halfHeight * viewCamera.aspect;
        flock.Step(MouseTarget(), Time.fixedDeltaTime, new Vector2(halfWidth, halfHeight));
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
}
