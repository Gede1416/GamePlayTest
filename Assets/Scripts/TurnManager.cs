using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 回合管理器：每回合按先攻（speed 大的先动）依次让每个角色行动一次，跑满给定的回合数就结束。
/// 角色的"行动" = 轮到它时跑一次它身上的 AutoPilot 管线，并等它走完再轮下一个。
/// </summary>
public class TurnManager : MonoBehaviour
{
    [System.Serializable]
    public class Actor
    {
        [Tooltip("角色物体")]
        public GameObject go;

        [Tooltip("先攻：数值大的先动；相同则按列表顺序")]
        public int speed = 10;
    }

    [Tooltip("参战角色")]
    public List<Actor> actors = new List<Actor>();

    [Tooltip("给定回合数：跑满这个回合数就结束")]
    public int totalRounds = 10;

    [Tooltip("角色行动完等多久（秒）")]
    public float turnDelay = 0.2f;

    [Tooltip("进入播放就开打")]
    public bool autoStart = true;

    /// <summary>当前第几回合（从 1 开始；没开打是 0）</summary>
    public int CurrentRound { get; private set; }

    /// <summary>当前轮到谁</summary>
    public Actor CurrentActor { get; private set; }

    /// <summary>回合数跑满了</summary>
    public bool IsFinished { get; private set; }

    Coroutine routine;

    void Start()
    {
        if (autoStart) StartBattle();
    }

    /// <summary>开打（会先停掉正在跑的那局，回合数从头数）</summary>
    public void StartBattle()
    {
        StopBattle();
        IsFinished = false;
        routine = StartCoroutine(Run());
    }

    /// <summary>停手；CurrentRound / IsFinished 保持原样便于查看</summary>
    public void StopBattle()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        CurrentActor = null;
    }

    /// <summary>本回合的行动顺序：先攻大的在前，相同则保持列表顺序</summary>
    public List<Actor> Order()
    {
        return actors.Where(a => a != null).OrderByDescending(a => a.speed).ToList();
    }

    IEnumerator Run()
    {
        for (CurrentRound = 1; CurrentRound <= totalRounds; CurrentRound++)
        {
            Debug.Log($"[TurnManager] 第 {CurrentRound}/{totalRounds} 回合开始");

            foreach (var actor in Order())
            {
                if (actor.go == null || !actor.go.activeInHierarchy) continue;   // 死了/被禁用就跳过这次行动
                CurrentActor = actor;
                Debug.Log($"[TurnManager] 第 {CurrentRound} 回合，{actor.go.name} 行动（先攻 {actor.speed}）");

                var pilot = actor.go.GetComponent<AutoPilot>();
                if (pilot != null)
                {
                    pilot.RunPipeline();                 // 轮到它，自己按管线找目标走
                    while (pilot.IsFollowing) yield return null;   // 等它走完再轮下一个
                }

                if (turnDelay > 0f) yield return new WaitForSeconds(turnDelay);
            }
        }

        CurrentActor = null;
        routine = null;
        IsFinished = true;
        Debug.Log($"[TurnManager] {totalRounds} 回合跑完，结束");
    }
}
