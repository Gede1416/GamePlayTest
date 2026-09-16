using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 回合管理器（**普通类**，由 InitManager 构建）：
/// 每回合按先攻（Entity.speed 大的先动）依次让每个角色行动一次，跑满给定的回合数就结束。
/// 角色的"行动" = 调 Entity.TakeTurn() 跑一次寻路管线，并等它走完再轮下一个。
/// 自己不是 MonoBehaviour，所以 Run() 返回 IEnumerator，交给 InitManager 去 StartCoroutine。
/// </summary>
public class TurnManager
{
    /// <summary>参战角色（Init 时给）：行动顺序按各自 Entity.speed 排（大的先动，相同则按列表顺序）</summary>
    public readonly List<Entity> actors = new();

    /// <summary>给定回合数：跑满这个回合数就结束</summary>
    public int totalRounds = 10;

    /// <summary>角色行动完等多久（秒）</summary>
    public float turnDelay = 0.2f;

    /// <summary>当前第几回合（从 1 开始；没开打是 0）</summary>
    public int CurrentRound { get; private set; }

    /// <summary>当前轮到谁</summary>
    public Entity CurrentActor { get; private set; }

    /// <summary>回合数跑满了</summary>
    public bool IsFinished { get; private set; }

    /// <summary>装人：参战角色列表</summary>
    public void Init(List<Entity> actors)
    {
        this.actors.Clear();
        if (actors != null) this.actors.AddRange(actors);
    }

    /// <summary>本回合的行动顺序：先攻大的在前，相同则保持列表顺序</summary>
    public List<Entity> Order()
    {
        return actors.Where(a => a != null).OrderByDescending(a => a.speed).ToList();
    }

    /// <summary>开打：由 MonoBehaviour（InitManager）StartCoroutine 起来</summary>
    public IEnumerator Run()
    {
        IsFinished = false;

        for (CurrentRound = 1; CurrentRound <= totalRounds; CurrentRound++)
        {
            Debug.Log($"[TurnManager] 第 {CurrentRound}/{totalRounds} 回合开始");

            foreach (var actor in Order())
            {
                if (actor == null || !actor.gameObject.activeInHierarchy) continue;   // 死了/被禁用就跳过这次行动
                CurrentActor = actor;
                Debug.Log($"[TurnManager] 第 {CurrentRound} 回合，{actor.name} 行动（先攻 {actor.speed}）");

                if (actor.TakeTurn() && actor.Pilot != null)   // 轮到它，自己按管线找目标走
                    while (actor.Pilot.IsFollowing)
                        yield return null;   // 等它走完再轮下一个

                if (turnDelay > 0f) yield return new WaitForSeconds(turnDelay);
            }
        }

        CurrentActor = null;
        IsFinished = true;
        Debug.Log($"[TurnManager] {totalRounds} 回合跑完，结束");
    }
}
