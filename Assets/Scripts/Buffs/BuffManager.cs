using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff 类型（和 SkillType / TargetSourceType 一个套路）：**按名字挂 buff**（技能里挂哪种 buff 也选这个）。
/// 名字对应哪个组合 buff 由 BuffManager 自己的映射（CreateBuff）决定，数值与持续回合在各组合类里。
/// </summary>
public enum BuffType
{
    /// <summary>治疗：每次结算回 20 血 × 3 回合（挂上时也先回一次，不产生常驻加成）</summary>
    Heal = 0,

    /// <summary>增加移动步数：挂上 +2 步、移除时减回去，持续 3 回合</summary>
    AddMoveSteps = 1,

    /// <summary>增加攻击力：挂上 +5 伤害、移除时减回去，持续 3 回合</summary>
    AddAttack = 2,
}

/// <summary>
/// Buff 管理器（挂在实体上，一个实体一个）：按**队列**存正在生效的 buff（每个 IBuff 都是效果零件拼起来的一条），
/// 每次结算轮转一圈——出队 → <c>Trigger()</c> → 过期的 <c>Remove()</c> 且**不再入队**，没过期的重新入队。
/// **Inspector / 代码里只给类型名**（`Add(BuffType)`）：名字 → 组合 buff 由自己的映射（CreateBuff）决定，
/// 这里只管队列、生命周期与广播。
/// 结算时机：<c>Entity.TakeTurnRoutine()</c> 开头调 <c>TickTurn()</c>，所以"1 回合"= 自己行动一次
/// （技能冷却不在这里推：那是各技能的释放判断收 <c>TurnChangedEvent</c> 自己减的，按大回合走）。
/// 由 Entity.Init / Clear 调（组件自己不写 Awake / Start）。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
[RequireComponent(typeof(Entity))]
public class BuffManager : MonoBehaviour
{
    #region 属性

    readonly Queue<IBuff> buffQueue = new Queue<IBuff>();   // 运行期状态，不序列化

    Entity owner;

    /// <summary>正在生效的 buff 条数</summary>
    public int Count => buffQueue.Count;

    /// <summary>正在生效的 buff（只读给外面看，例如 buff 条刷新时遍历）</summary>
    public IEnumerable<IBuff> Active => buffQueue;

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
        if (buffQueue.Count == 0) return;

        foreach (var buff in buffQueue) buff.Remove();
        buffQueue.Clear();
        SendChanged();
    }

    /// <summary>挂一条 buff：按类型造一条组合 buff，立刻生效并入队；targets 留空就是挂给自己</summary>
    public void Add(BuffType type, List<Entity> targets = null)
    {
        var buff = CreateBuff(type, targets ?? new List<Entity> { owner });
        if (buff == null) return;

        buff.Add();         // 常驻加成当场生效（治疗这类要等结算）
        buffQueue.Enqueue(buff);
        SendChanged();
    }

    /// <summary>结算一次（自己的回合开始调）：队列轮转一圈，结束的那条移除后不再入队</summary>
    public void TickTurn()
    {
        bool expired = false;
        int count = buffQueue.Count;                    // 先记下来：这一圈只处理现在已有的
        for (int i = 0; i < count; i++)
        {
            var buff = buffQueue.Dequeue();
            buff.Trigger();

            if (buff.IsOver)
            {
                buff.Remove();                          // 结束的：撤掉加成，不入队
                expired = true;
            }
            else buffQueue.Enqueue(buff);               // 没结束的：转回队尾
        }

        if (expired) SendChanged();                     // 有到期的才广播一次（界面刷新）
    }

    #endregion

    #region 私有方法

    /// <summary>buff 名 → 组合 buff（加 buff = 加一个枚举 + 这里一个 case，数值与持续回合写在 Combination 里）</summary>
    IBuff CreateBuff(BuffType type, List<Entity> targets)
    {
        switch (type)
        {
            case BuffType.Heal: return new HealBuff(targets);
            case BuffType.AddMoveSteps: return new AddMoveStepsBuff(targets);
            case BuffType.AddAttack: return new AddAttackBuff(targets);
        }

        Debug.LogWarning($"[{name}] {type} 没接实现");
        return null;
    }

    /// <summary>广播"buff 变了"（挂上 / 到期移除 / 清场都走这里，界面只认这条消息）</summary>
    void SendChanged() => EventPipeline.Send(new BuffChangedEvent(owner));

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
            Debug.Log($"  {buff.Type}：{(buff.Left < 0 ? "永久" : $"还剩 {buff.Left} 次结算")}，{(buff.IsOver ? "该结束了" : "还在")}", this);
        }
    }

    #endregion
}
