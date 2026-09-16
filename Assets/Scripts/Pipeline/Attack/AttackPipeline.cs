using System.Collections.Generic;
using UnityEngine;

// ============ 攻击管线三段接口 ============
// 阶段一：技能释放判断 -> 阶段二：目标获取 -> 阶段三：技能释放
// 和移动管线一样：依赖（地图等）走构造函数注入，接口只收"这次要处理什么"（技能 / 施法者 / 目标列表）。

/// <summary>阶段一：技能释放判断——这次能不能放</summary>
public interface ICastCheck
{
    bool CanCast(Skill skill, Entity caster);
}

/// <summary>阶段二：目标获取——按技能的 攻击范围 / 目标数量 选出目标</summary>
public interface ITargetFinder
{
    bool TryFindTargets(Skill skill, Entity caster, List<Entity> targets);
}

/// <summary>阶段三：技能释放——把技能打到目标身上</summary>
public interface ISkillCaster
{
    bool Cast(Skill skill, Entity caster, List<Entity> targets);
}


// ============ 三个默认实现 ============

/// <summary>默认的释放判断：直接问技能自己的前置条件</summary>
public class SkillReadyCheck : ICastCheck
{
    public bool CanCast(Skill skill, Entity caster)
    {
        return skill != null && skill.CanCast(caster);
    }
}

/// <summary>
/// 默认的目标获取：以施法者所在格子为圆心，在技能的攻击范围内找非己方单位，
/// 按曼哈顿距离由近到远取前 targetCount 个。
/// </summary>
public class RangeTargetFinder : ITargetFinder
{
    readonly MapManager map;

    public RangeTargetFinder(MapManager map)
    {
        this.map = map;
    }

    public bool TryFindTargets(Skill skill, Entity caster, List<Entity> targets)
    {
        targets.Clear();
        if (map == null || skill == null || caster == null || skill.targetCount <= 0) return false;

        var self = map.WorldToCell(caster.transform.position);

        // 单位从地图的实体列表里找（map 就是单位注册表），不扫全场景
        foreach (var go in map.entities)
        {
            if (go == null) continue;

            var health = go.GetComponent<Health>();
            if (health == null || health.team == caster.Team || health.IsDead) continue;   // 只认非己方且活着的

            if (MapManager.Manhattan(self, map.WorldToCell(go.transform.position)) > skill.range) continue;   // 攻击范围外

            var entity = go.GetComponent<Entity>();
            if (entity != null) targets.Add(entity);
        }

        if (targets.Count == 0) return false;

        targets.Sort((a, b) => Dist(self, a).CompareTo(Dist(self, b)));              // 近的优先
        if (targets.Count > skill.targetCount)
            targets.RemoveRange(skill.targetCount, targets.Count - skill.targetCount);   // 目标数量上限

        return true;
    }

    int Dist(Vector2Int from, Entity e)
    {
        return MapManager.Manhattan(from, map.WorldToCell(e.transform.position));
    }
}

/// <summary>默认的技能释放：调技能自己的 Cast 附加效果，然后进冷却</summary>
public class DefaultSkillCaster : ISkillCaster
{
    public bool Cast(Skill skill, Entity caster, List<Entity> targets)
    {
        if (skill == null || caster == null || targets == null || targets.Count == 0) return false;

        skill.Cast(caster, targets);     // 对目标附加效果（子类实现）
        skill.StartCooldown();
        return true;
    }
}
