using UnityEngine;
using UnityEngine.Serialization;

/// <summary>俯视群集测试场入口，负责造场、鼠标输入和驱动群集。</summary>
public class BoidsTestManager : MonoBehaviour
{
    [SerializeField] Camera viewCamera;
    [SerializeField] Material boidMaterial;
    [SerializeField, Range(0, 6), Tooltip("与贴近的邻居拉开距离")] float separationWeight = 3f;
    [SerializeField, Range(0, 6), Tooltip("逐渐匹配邻居的平均速度")] float alignmentWeight = 1f;
    [SerializeField, Range(0, 6), Tooltip("靠近邻居的中心")] float cohesionWeight = 0.8f;
    [FormerlySerializedAs("mouseWeight")]
    [SerializeField, Range(0, 6), Tooltip("匹配阵型移动速度的强度，设为零可关闭主动追随")] float formationFollowWeight = 1.6f;
    [SerializeField, Tooltip("阵型边界：AABB 沿世界轴，OBB 随阵型朝向旋转")] FormationBoundsType formationBoundsType = FormationBoundsType.Obb;
    [SerializeField, Tooltip("阵型的宽度和深度，OBB 模式下沿阵型的左右和前后方向")] Vector2 formationSize = new Vector2(8, 5);
    [SerializeField, Min(0), Tooltip("阵型中心移向鼠标左键点击位置的速度，世界单位/秒")] float formationSpeed = 3f;
    [Header("阵型偏转")]
    [SerializeField, Min(0), Tooltip("阵型每秒最多旋转的角度，设为零可停止旋转")] float formationTurnSpeed = 120f;
    [SerializeField, Range(0, 180), Tooltip("阵型朝向差小于此角度时不转向")] float formationRotationDeadZone = 3f;
    [SerializeField, Min(0), Tooltip("阵型速度低于此值时保持朝向，世界单位/秒")] float formationMinHeadingSpeed = 0.2f;
    [Header("单位偏转")]
    [SerializeField, Min(0), Tooltip("单位每秒最多旋转的角度，设为零可停止旋转")] float agentTurnSpeed = 180f;
    [SerializeField, Range(0, 180), Tooltip("单位朝向差小于此角度时不转向")] float agentRotationDeadZone = 3f;
    [SerializeField, Min(0), Tooltip("单位速度低于此值时使用阵型朝向，世界单位/秒")] float agentMinHeadingSpeed = 0.2f;
    [SerializeField, Range(0, 1), Tooltip("零为单位运动方向，一为阵型朝向；低速时使用阵型朝向")] float formationFacingBias = 0.65f;

    const int Count = 64;
    readonly Plane ground = new Plane(Vector3.up, Vector3.zero);
    Mesh arrow;
    BoidFlock flock;
    BoidFormation formation;
    LineRenderer formationOutline;
    Transform formationHeading;
    Vector3? moveTarget;

    void Awake()
    {
        arrow = new Mesh { name = "Boid" };
        arrow.vertices = new[] { new Vector3(0, 0, 0.36f), new Vector3(0.17f, 0, -0.22f), new Vector3(-0.17f, 0, -0.22f) };
        arrow.triangles = new[] { 0, 1, 2 };
        arrow.RecalculateNormals();
        formation = new BoidFormation(Vector3.zero, formationSize) { BoundsType = formationBoundsType };
        var outline = new GameObject("Formation Bounds");
        outline.transform.SetParent(transform, false);
        formationOutline = outline.AddComponent<LineRenderer>();
        formationOutline.sharedMaterial = boidMaterial;
        formationOutline.positionCount = 4;
        formationOutline.loop = true;
        formationOutline.widthMultiplier = 0.04f;
        formationOutline.useWorldSpace = true;
        var headingView = new GameObject("Formation Heading");
        headingView.transform.SetParent(transform, false);
        headingView.AddComponent<MeshFilter>().sharedMesh = arrow;
        headingView.AddComponent<MeshRenderer>().sharedMaterial = boidMaterial;
        formationHeading = headingView.transform;
        formationHeading.localScale = Vector3.one * 2;
        UpdateFormationView();
        var agents = new BoidAgent[Count];
        for (int i = 0; i < Count; i++)
        {
            var boid = new GameObject("Boid " + i);
            boid.transform.SetParent(transform, false);
            boid.AddComponent<MeshFilter>().sharedMesh = arrow;
            boid.AddComponent<MeshRenderer>().sharedMaterial = boidMaterial;
            Vector2 halfSize = formation.Size * 0.5f - Vector2.one * BoidFormation.AgentRadius;
            Vector3 position = formation.ToWorld(new Vector3(
                Random.Range(-halfSize.x, halfSize.x), 0, Random.Range(-halfSize.y, halfSize.y)));
            Quaternion heading = Quaternion.Euler(0, Random.value * 360f, 0);
            agents[i] = new BoidAgent(boid.transform, position, heading);
        }
        flock = new BoidFlock(agents);
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        Vector3? target = MouseTarget();
        if (target.HasValue) moveTarget = target;
    }

    void FixedUpdate()
    {
        flock.SeparationWeight = separationWeight;
        flock.AlignmentWeight = alignmentWeight;
        flock.CohesionWeight = cohesionWeight;
        flock.FormationFollowWeight = formationFollowWeight;
        flock.SetRotationParameters(agentTurnSpeed, agentRotationDeadZone, agentMinHeadingSpeed, formationFacingBias);
        formation.BoundsType = formationBoundsType;
        formation.SetSize(formationSize);
        formation.SetRotationParameters(formationTurnSpeed, formationRotationDeadZone, formationMinHeadingSpeed);
        formation.Step(moveTarget, formationSpeed, Time.fixedDeltaTime);
        flock.Step(Time.fixedDeltaTime, formation);
        UpdateFormationView();
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

    void UpdateFormationView()
    {
        for (int i = 0; i < 4; i++)
            formationOutline.SetPosition(i, formation.GetCorner(i));
        formationHeading.SetPositionAndRotation(formation.Center + Vector3.up * 0.02f,
            Quaternion.LookRotation(formation.Forward));
    }
}
