using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff 管理器（挂在实体上，一个实体一个）：**只管自己身上这一队 buff**——按队列存正在生效的 buff 零件，
/// 每次结算轮转一圈（出队 → <c>Tick(owner)</c> → 过期的 <c>Revert(owner)</c> 且**不再入队**，没过期的转回队尾）。
/// **不认名字、也不造 buff**：挂哪种 buff 由技能那边配（阶段三的 BuffCaster 决定挂哪个零件），
/// 挂给谁由技能阶段二挑好、逐个挂过来；这里只负责收下、排队、到期撤掉。
/// 队列里存的就是**效果零件本身**（`IBuff`）：数值与持续回合都由零件自己带，这里只把主人当初调它的参数传下去。
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

        foreach (var buff in buffQueue) buff.Revert(owner);
        buffQueue.Clear();
    }

    /// <summary>收下一条已经造好的 buff 零件（作用在谁身上 = 自己）：立刻生效并入队；空的不收</summary>
    public void Add(IBuff buff)
    {
        if (buff == null) return;

        buff.Apply(owner);              // 常驻加成当场生效（治疗这类要等结算）
        buffQueue.Enqueue(buff);
    }

    /// <summary>结算一次（自己的回合开始调）：队列轮转一圈，结束的那条移除后不再入队</summary>
    public void TickTurn()
    {
        int count = buffQueue.Count;                    // 先记下来：这一圈只处理现在已有的
        for (int i = 0; i < count; i++)
        {
            var buff = buffQueue.Dequeue();
            buff.Tick(owner);

            if (buff.IsOver) buff.Revert(owner);        // 结束的：撤掉加成，不入队
            else buffQueue.Enqueue(buff);               // 没结束的：转回队尾
        }
    }

    #endregion

    #region 私有方法

    [ContextMenu("打印正在生效的 buff")]
    void PrintBuffs()
    {
        Debug.Log($"[{name}] 正在生效 {buffQueue.Count} 条", this);
        foreach (var buff in buffQueue)
            Debug.Log($"  {buff.GetType().Name}：{(buff.IsOver ? "该结束了" : "还在")}", this);
    }

    #endregion
}
