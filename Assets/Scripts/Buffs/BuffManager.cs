using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff 管理器（挂在实体上，一个实体一个）：按**队列**存正在生效的 buff，每次结算轮转一圈——
/// 出队 → <c>Trigger()</c> → 过期的 <c>Remove()</c> 且**不再入队**，没过期的重新入队。
/// 数值 / 持续回合由 BuffFactory 决定，这里只管队列与生命周期。
/// 结算时机：<c>Entity.TakeTurnRoutine()</c> 开头调 <c>TickTurn()</c>（和 Attacker.TickTurn 推进冷却同一个位置，
/// 所以"1 回合"= 自己行动一次；要改成"每大回合"就挪到 TurnChangedEvent 那边）。
/// 由 Entity.Init / Clear 调（组件自己不写 Awake / Start）。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
[RequireComponent(typeof(Entity))]
public class BuffManager : MonoBehaviour
{
    #region 属性

    readonly Queue<Buff> buffQueue = new Queue<Buff>();   // 运行期状态，不序列化

    Entity owner;

    /// <summary>正在生效的 buff 条数</summary>
    public int Count => buffQueue.Count;

    #endregion

    #region 公开方法

    /// <summary>初始化：记住主人 + 清空队列（由 Entity.Init 调）</summary>
    public void Init()
    {
        owner = GetComponent<Entity>();
        buffQueue.Clear();
    }

    /// <summary>清理：先把每条 buff 移除（加成撤回去，别留在实体上）再清空队列（由 Entity.Clear 调）</summary>
    public void Clear()
    {
        foreach (var buff in buffQueue) buff.Remove();
        buffQueue.Clear();
    }

    /// <summary>挂一条 buff：按类型造配置与效果，立刻生效并入队；targets 留空就是挂给自己</summary>
    public void Add(BuffType type, List<Entity> targets = null)
    {
        var buff = BuffFactory.Create(type, targets ?? new List<Entity> { owner });
        if (buff == null) return;

        buff.Add();         // 常驻加成当场生效（治疗这类要等结算）
        buffQueue.Enqueue(buff);
    }

    /// <summary>结算一次（自己的回合开始调）：队列轮转一圈，结束的那条移除后不再入队</summary>
    public void TickTurn()
    {
        int count = buffQueue.Count;                    // 先记下来：这一圈只处理现在已有的
        for (int i = 0; i < count; i++)
        {
            var buff = buffQueue.Dequeue();
            buff.Trigger();

            if (buff.IsOver) buff.Remove();             // 结束的：撤掉加成，不入队
            else buffQueue.Enqueue(buff);               // 没结束的：转回队尾
        }
    }

    #endregion

    #region 私有方法

    [ContextMenu("测试：挂一个治疗 buff")]
    void TestHeal() => Add(BuffType.Heal);

    [ContextMenu("测试：挂一个增加移动步数 buff")]
    void TestMoveSteps() => Add(BuffType.AddMoveSteps);

    [ContextMenu("测试：挂一个增加攻击力 buff")]
    void TestAttack() => Add(BuffType.AddAttack);

    [ContextMenu("打印正在生效的 buff")]
    void PrintBuffs()
    {
        Debug.Log($"[{name}] 正在生效 {buffQueue.Count} 条", this);
        foreach (var buff in buffQueue)
        {
            Debug.Log($"  {buff.Cfg.type}：数值 {buff.Cfg.value}，持续 {buff.Cfg.duration} 回合，{(buff.IsOver ? "该结束了" : "还在")}", this);
        }
    }

    #endregion
}
