using UnityEngine;

/// <summary>
/// 测试用：让 pilot 走到 anchor（默认 entity (1)）上下左右最近的空格。
/// 挂在 Canvas 上，四个按钮的 onClick 分别指到 GoUp / GoDown / GoLeft / GoRight。
/// </summary>
public class NavTest : MonoBehaviour
{
    [Tooltip("地图管理器")]
    public MapManager map;

    [Tooltip("参照物：走到它四周最近的格子")]
    public Transform anchor;

    [Tooltip("要移动的寻路组件（entity 身上的 AutoPilot）")]
    public AutoPilot pilot;

    [Header("手动目标（测试用）")]
    [Tooltip("手动填一个目标格：运行时在 Inspector 里改一下就会自动出发")]
    public Vector2Int manualTarget;

    Vector2Int lastManual;

    void OnValidate()
    {
        // 只在运行时、且值真的变了才出发；进播放模式/脚本重载触发的回调不会误触发
        if (!Application.isPlaying || manualTarget == lastManual) return;
        lastManual = manualTarget;
        GoToManual();
    }

    [ContextMenu("移动到手动目标")]
    public void GoToManual() => GoTo(manualTarget);

    public void GoUp() => GoNearest(Vector2Int.up);
    public void GoDown() => GoNearest(Vector2Int.down);
    public void GoLeft() => GoNearest(Vector2Int.left);
    public void GoRight() => GoNearest(Vector2Int.right);

    /// <summary>从 anchor 所在格子沿 dir 往外找最近的可进入格子，然后让 pilot 走过去</summary>
    public bool GoNearest(Vector2Int dir)
    {
        if (map == null || anchor == null || pilot == null)
        {
            Debug.LogWarning("[NavTest] map / anchor / pilot 还没接好", this);
            return false;
        }

        var cell = map.WorldToCell(anchor.position);
        for (int i = 0; i < map.Cols + map.Rows; i++)
        {
            cell += dir;
            if (!map.InBounds(cell)) break;              // 出界了，这个方向没有格子
            if (map.CanEnter(cell)) return GoTo(cell);   // 最近的一个空格
        }

        Debug.Log($"[NavTest] {dir} 方向没有可进入的格子");
        return false;
    }

    /// <summary>走到指定格子</summary>
    public bool GoTo(Vector2Int cell)
    {
        if (pilot == null) return false;

        bool ok = pilot.MoveTo(cell);
        Debug.Log(ok ? $"[NavTest] 出发 -> 格 {cell}" : $"[NavTest] 走不到格 {cell}");
        return ok;
    }
}
