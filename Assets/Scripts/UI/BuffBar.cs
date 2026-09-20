using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Buff 条：把实体当前正在生效的 buff 列在头顶（外观全在预制体里：世界空间 Canvas + 一个 TMP 文字）。
/// 刷新**走事件**——BuffManager 挂上 / 到期移除 / 清场时发 BuffChangedEvent，这里收到、只认自己跟的那个实体，
/// 再读一遍 <c>entity.Buffs.Active</c> 拼文字；不轮询、也不去改 buff 数据。
/// 和血条一样：由 BattleUIManager 在 EntitySpawnedEvent 时实例化到实体的 UI 点位下，LateUpdate 贴点位转向相机。
/// 文字只能是英文 / 数字（默认字体资源没有中文字形），所以显示的是零件类名与剩余结算次数，形如 HealEffect(3) MoveStepsEffect(2)。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class BuffBar : MonoBehaviour
{
    #region 属性

    [Tooltip("显示 buff 的文字（预制体里接好）")]
    [SerializeField] TMP_Text label;

    Entity target;          // 跟着谁（读它的 Buffs / 按它过滤消息）
    Transform anchor;       // 挂在哪（实体的 UI 点位）

    readonly StringBuilder builder = new StringBuilder();   // 拼文字用，避免每次刷新都产生垃圾

    #endregion

    #region 生命周期

    // 每帧贴住 UI 点位并转向相机（和血条一个做法）
    void LateUpdate()
    {
        if (anchor != null) transform.position = anchor.position;

        var cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }

    #endregion

    #region 公开方法

    /// <summary>初始化：记下跟谁、订 buff 变化消息、按当前状态先刷一次（由 BattleUIManager 实例化后调）</summary>
    public void Init(Entity entity)
    {
        target = entity;
        anchor = entity != null ? entity.uiPoint : null;

        EventPipeline.Subscribe<BuffChangedEvent>(OnBuffChanged);
        Refresh();
    }

    /// <summary>清理：退订（条子本身由 BattleUIManager 销毁）</summary>
    public void Clear() => EventPipeline.Unsubscribe<BuffChangedEvent>(OnBuffChanged);

    #endregion

    #region 私有方法

    /// <summary>收到 buff 变化：只认自己跟的那个实体</summary>
    void OnBuffChanged(BuffChangedEvent e)
    {
        if (e.entity != target) return;
        Refresh();
    }

    /// <summary>按当前生效的 buff 重拼文字（零件类名 + 剩余结算次数；永久 buff 不显示次数，没 buff 就清空）</summary>
    void Refresh()
    {
        if (label == null) return;

        var buffs = target != null ? target.Buffs : null;
        builder.Length = 0;

        if (buffs != null)
        {
            foreach (var buff in buffs.Active)
            {
                if (builder.Length > 0) builder.Append(' ');
                builder.Append(buff.GetType().Name);
                if (buff.Left > 0) builder.Append('(').Append(buff.Left).Append(')');
            }
        }

        label.text = builder.ToString();
    }

    #endregion
}
