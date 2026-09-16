using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 攻击管线（挂在实体上）：阶段一 技能释放判断 -> 阶段二 目标获取 -> 阶段三 技能释放。
/// 三段都可替换（外部 new 好塞进来）；默认就是 AttackPipeline.cs 里的三个实现。
/// </summary>
[RequireComponent(typeof(Entity))]
public class Attacker : MonoBehaviour
{
    [Tooltip("要放的技能：攻击范围 / 目标数量 / 前置条件 / 效果 都在它身上（Skill 普通类，可继承扩展）")]
    public Skill skill = new Skill();

    [Tooltip("地图管理器；留空则取场景里的第一个")]
    public MapManager map;

    /// <summary>阶段一：技能释放判断</summary>
    public ICastCheck CastCheck { get; set; }

    /// <summary>阶段二：目标获取</summary>
    public ITargetFinder TargetFinder { get; set; }

    /// <summary>阶段三：技能释放</summary>
    public ISkillCaster Caster { get; set; }

    /// <summary>最近一次目标获取选出来的目标</summary>
    public readonly List<Entity> targets = new();

    Entity self;

    void Awake()
    {
        self = GetComponent<Entity>();
        if (map == null) map = FindObjectOfType<MapManager>();

        CastCheck ??= new SkillReadyCheck();
        TargetFinder ??= new RangeTargetFinder(map);
        Caster ??= new DefaultSkillCaster();
    }

    /// <summary>攻击管线：技能释放判断 -> 目标获取 -> 技能释放</summary>
    public bool RunPipeline()
    {
        if (CastCheck == null || TargetFinder == null || Caster == null) return false;

        if (!CastCheck.CanCast(skill, self)) return false;                     // 阶段一
        if (!TargetFinder.TryFindTargets(skill, self, targets)) return false;  // 阶段二
        return Caster.Cast(skill, self, targets);                              // 阶段三
    }

    /// <summary>回合推进：技能冷却减一（想让技能有冷却就在每回合开始调它）</summary>
    public void TickTurn()
    {
        skill?.TickTurn();
    }

    [ContextMenu("跑一次攻击管线")]
    void RunMenu()
    {
        bool ok = RunPipeline();
        Debug.Log(ok
            ? $"[{name}] {skill.skillName} 命中 {targets.Count} 个目标"
            : $"[{name}] {skill.skillName} 没放出来（前置条件不满足 / 范围内没目标）");
    }
}
