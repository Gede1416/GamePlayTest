using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>回合状态</summary>
public enum TurnState
{
    /// <summary>没开打</summary>
    Idle = 0,

    /// <summary>进行中</summary>
    Running = 1,

    /// <summary>跑满给定回合数，已结束</summary>
    Finished = 2,
}

/// <summary>
/// 回合管理器。数据结构：
/// 战斗实体列表 actors / 行动栈 ActionStack（每回合按先攻重建）/ 回合状态 State。
/// 每回合把实体按先攻排进行动栈，逐个出栈行动（yield return Entity.TakeTurnRoutine()），跑满给定回合数结束。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class TurnManager : MonoBehaviour
{
    #region 属性

    [Header("数据")][Tooltip("参战实体列表")]
    public List<Entity> actors = new List<Entity>();

    [Tooltip("给定回合数：跑满这个回合数就结束")]
    public int totalRounds = 10;

    [Tooltip("角色行动完等多久（秒）")]
    public float turnDelay = 0.2f;

    [Tooltip("进场就开打（由 BattleManager 在初始化完之后读它决定）")]
    public bool autoStart = true;

    /// <summary>行动栈：本回合的行动顺序（按先攻从高到低，每回合开始时重建）</summary>
    public readonly List<Entity> ActionStack = new();

    int cursor;             // 行动栈里下一位
    Coroutine routine;

    /// <summary>当前第几回合（从 1 开始；没开打是 0）</summary>
    public int CurrentRound { get; private set; }

    /// <summary>当前正在行动的实体</summary>
    public Entity CurrentActor { get; private set; }

    /// <summary>回合状态</summary>
    public TurnState State { get; private set; } = TurnState.Idle;

    /// <summary>跑满回合数了</summary>
    public bool IsFinished => State == TurnState.Finished;

    /// <summary>行动栈里还剩几个没行动（含已被跳过但要到出栈时才判断的）</summary>
    public int StackLeft => Mathf.Max(0, ActionStack.Count - cursor);

    #endregion

    #region 公开方法

    /// <summary>
    /// 初始化：由 BattleManager 调用（回合管理器自己不用 Awake / Start）。
    /// 不传 data 就保持现状；传了 data 就用它覆盖参战列表、回合数与间隔。
    /// </summary>
    public void Init(TurnInitData data = null)
    {
        if (data == null) return;

        actors.Clear();
        if (data.actors != null) actors.AddRange(data.actors);
        totalRounds = data.totalRounds;
        turnDelay = data.turnDelay;
    }

    /// <summary>开始回合：只管开——要重开先让 BattleManager 清一次（避免协程叠着跑）</summary>
    public void StartBattle()
    {
        if (routine != null) return;
        State = TurnState.Running;
        routine = StartCoroutine(Run());
    }

    /// <summary>清理：停协程 + 清空行动栈与参战列表 + 状态归零（由 BattleManager.ClearBattle 调，本组件自己不负责停）</summary>
    public void Clear()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;

        CurrentActor = null;
        CurrentRound = 0;
        cursor = 0;
        ActionStack.Clear();
        actors.Clear();
        State = TurnState.Idle;
    }

    /// <summary>重建行动栈：先攻大的在前，相同则保持参战列表顺序（每回合开始时内部调）</summary>
    void BuildActionStack()
    {
        ActionStack.Clear();
        cursor = 0;
        ActionStack.AddRange(actors.Where(a => a != null).OrderByDescending(a => a.initiative));
    }

    /// <summary>出栈：下一个该行动的实体（已阵亡/被禁用的直接跳过），没有了返回 null</summary>
    Entity PopNext()
    {
        while (cursor < ActionStack.Count)
        {
            var actor = ActionStack[cursor++];
            if (actor != null && actor.gameObject.activeInHierarchy) return actor;
        }
        return null;
    }

    #endregion

    #region 私有方法

    IEnumerator Run()
    {
        for (CurrentRound = 1; CurrentRound <= totalRounds; CurrentRound++)
        {
            BuildActionStack();
            Debug.Log($"[TurnManager] 第 {CurrentRound}/{totalRounds} 回合开始，行动顺序：{string.Join(" > ", ActionStack.ConvertAll(a => a != null ? a.name : "空"))}");

            while (true)
            {
                var actor = PopNext();
                if (actor == null) break;             // 本回合行动栈空了

                CurrentActor = actor;
                Debug.Log($"[TurnManager] 第 {CurrentRound} 回合，{actor.name} 行动（先攻 {actor.initiative}）");

                yield return actor.TakeTurnRoutine();   // 攻击 -> 移动 -> 攻击（内部会等移动走完）

                if (turnDelay > 0f) yield return new WaitForSeconds(turnDelay);
            }
        }

        CurrentActor = null;
        routine = null;
        State = TurnState.Finished;
        Debug.Log($"[TurnManager] {totalRounds} 回合跑完，结束");
    }

    #endregion

}

/// <summary>TurnManager.Init 需要的数据：参战列表 / 回合数 / 行动间隔</summary>
[System.Serializable]
public class TurnInitData
{
    [Tooltip("参战实体列表")]
    public List<Entity> actors = new();

    [Tooltip("给定回合数")]
    public int totalRounds = 10;

    [Tooltip("角色行动完等多久（秒）")]
    public float turnDelay = 0.2f;
}
