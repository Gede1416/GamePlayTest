using System.Collections;
using UnityEngine;

/// <summary>
/// 3D 俯视角格子移动（y 为高度，在 XZ 平面移动）。
/// 只负责：收移动指令 -> 问 MapManager 要合法性与落点 -> 用协程把物体挪到落点。
/// 自己不改任何地图数据（占用、格子都由 MapManager 管）。
/// 指令来源：外部赋值 moveInput（优先），否则读取 WASD；外部脚本也可直接调 Move。
/// </summary>
public class ObjectMover : MonoBehaviour
{
    [Tooltip("地图管理器；留空则取场景里的第一个")]
    public MapManager map;

    [Tooltip("移动速度（世界单位/秒）；走一格用时 = 地图格子边长 / 速度")]
    public float speed = 5f;

    [Tooltip("外部输入（移动指令）：每帧由其他脚本赋值即可覆盖键盘输入；留空/零则使用 WASD")]
    public Vector3 moveInput;

    [Tooltip("每回合最多走几格；0 = 不限（由实体类 Entity.moveSteps 配置）")]
    public int stepLimit;

    [Tooltip("本回合剩余步数，每回合开始时由 Entity 补满（stepLimit 为 0 时无意义）")]
    public int stepsLeft;

    Vector2Int from, to;     // 本次行走的起点格与落点格（落点由地图裁决给出）
    bool walking;

    /// <summary>是否正在走（动画中，输入被阻断）</summary>
    public bool IsMoving => walking;

    /// <summary>本回合还剩步数（stepLimit 为 0 表示不限）</summary>
    public bool HasSteps => stepLimit <= 0 || stepsLeft > 0;

    /// <summary>把剩余步数补满——每回合开始时调</summary>
    public void ResetSteps() => stepsLeft = stepLimit;

    void Awake()
    {
        if (map == null) map = FindObjectOfType<MapManager>();
        if (map == null) Debug.LogWarning($"{name}: 没找到 MapManager，移动不会生效", this);
        if (stepLimit > 0 && stepsLeft <= 0) ResetSteps();   // 没挂 Entity 时自己补一次，免得一上来就不能走
    }

    void Update()
    {
        Vector3 input = moveInput.sqrMagnitude > 0f ? moveInput : ReadWASD();
        Move(ToStep(new Vector2(input.x, input.z)));
    }

    /// <summary>
    /// 移动指令入口：沿 step 走一格。动画中 / 步数用完 / 地图说不合法 / 没有指令都返回 false。
    /// </summary>
    public bool Move(Vector2Int step)
    {
        if (walking || map == null || speed <= 0f || step == Vector2Int.zero) return false;   // 动画中阻断输入
        if (!HasSteps) return false;                                                          // 本回合步数用完了

        from = map.WorldToCell(transform.position);
        if (!map.TryMove(from, step, out to)) return false;   // 合法性与落点都问地图

        if (stepLimit > 0) stepsLeft--;                       // 确定了要扣：走成了才扣
        walking = true;
        StartCoroutine(Walk());
        return true;
    }

    void OnDisable()
    {
        if (walking && map != null) map.CancelMove(from, to);   // 半路被禁用：让地图把占用数据退回起点
        walking = false;
    }

    /// <summary>协程动画：从当前坐标插值到地图给的落点</summary>
    IEnumerator Walk()
    {
        Vector3 start = transform.position;
        Vector3 target = map.CellToWorld(to.x, to.y);
        target.y = start.y;                                  // 高度不变，只走平面
        float dur = map.cellSize / speed;

        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(start, target, t / dur);   // 想缓动改 Vector3.SmoothStep
            yield return null;
        }

        transform.position = target;
        walking = false;
    }

    /// <summary>平面输入 -> 一格方向（只走四方向，取分量大的轴；无输入返回零）</summary>
    public static Vector2Int ToStep(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f) return Vector2Int.zero;
        if (Mathf.Abs(input.x) >= Mathf.Abs(input.y)) return new Vector2Int(input.x > 0f ? 1 : -1, 0);
        return new Vector2Int(0, input.y > 0f ? 1 : -1);
    }

    /// <summary>WASD（含方向键）-> Vector3，值为 -1 / 0 / 1；z 分量对应世界 z</summary>
    public static Vector3 ReadWASD()
    {
        return new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
    }
}
