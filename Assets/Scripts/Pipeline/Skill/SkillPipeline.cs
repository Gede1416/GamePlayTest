using System.Collections.Generic;
using UnityEngine;

// ============ 技能管线三段接口 ============
// 阶段一：技能释放判断 -> 阶段二：目标获取 -> 阶段三：技能释放
// 三段都**不依赖任何技能对象**、也不存外部依赖：范围 / 目标数 / 伤害 / 挂哪种 buff 由各自的实现自己带（构造函数注入），
// 地图由 SkillManager 每次调阶段二时传进来。
// 一条技能 = 三段各拿几个实现拼起来（见 Combination/）。
// 额度（冷却 / 场次）是阶段一里的检查自己管：判断拿不到"放成没放成"，靠 SkillManager 放成后发的
// SkillCastEvent（施法者 uuid + 技能名）——检查内部缓存自己的施法者 id 与技能名，收到消息比对上了才记自己的账。

/// <summary>
/// 一条技能：**三段拼起来的组合**。
/// 判断之间是"与"（都过才放）、目标获取之间是"或"（谁先找到算谁）、释放之间是"与"（都成功才算放成）。
/// </summary>
public interface ISkill : ICastCheck, ITargetFinder, ISkillCaster
{
    /// <summary>技能名（自己的身份：释放判断拿它去施法者身上查使用次数）</summary>
    SkillType Type { get; }

    /// <summary>加 / 减伤害（buff 用）：只有伤害类技能认这个加成，其它技能空转</summary>
    void AddDamage(float delta);
}

/// <summary>阶段一：技能释放判断——这次能不能放（自己判断，不看别人）</summary>
public interface ICastCheck
{
    bool CanCast(Entity caster);
}

/// <summary>阶段二：目标获取——挑出这次要作用的目标（地图由调用方给，零件自己不存）</summary>
public interface ITargetFinder
{
    List<Entity> TryFindTargets(Entity caster, MapManager map);
}

/// <summary>阶段三：技能释放——对目标附加效果（扣血 / 挂 buff）</summary>
public interface ISkillCaster
{
    bool Cast(Entity caster, List<Entity> targets);
}

// ============ 阶段二：目标获取 ============

/// <summary>两个攻击目标获取共用的挑选逻辑：范围内按曼哈顿距离由近到远取前 targetCount 个非己方（没挑到就返回空列表）</summary>
public static class TargetPicker
{
    public static List<Entity> Pick(MapManager map, Entity caster, int range, int targetCount)
    {
        var targets = new List<Entity>();
        if (map == null || caster == null || targetCount <= 0) return targets;

        var self = map.WorldToCell(caster.transform.position);

        // 单位从地图的实体列表里找（map 就是单位注册表），不扫全场景
        foreach (var go in map.entities)
        {
            if (go == null) continue;

            var health = go.GetComponent<Health>();
            if (health == null || health.team == caster.Team || health.IsDead) continue;   // 只认非己方且活着的

            var entity = go.GetComponent<Entity>();
            if (entity == null) continue;

            int d = MapManager.Manhattan(self, map.WorldToCell(go.transform.position));
            if (d > range) continue;                                                       // 技能范围外

            targets.Add(entity);
        }

        if (targets.Count == 0) return targets;

        targets.Sort((a, b) => Dist(map, self, a).CompareTo(Dist(map, self, b)));          // 近的优先
        if (targets.Count > targetCount)
            targets.RemoveRange(targetCount, targets.Count - targetCount);                 // 目标数量上限

        return targets;
    }

    #region 私有方法

    static int Dist(MapManager map, Vector2Int from, Entity e)
    {
        return MapManager.Manhattan(from, map.WorldToCell(e.transform.position));
    }

    #endregion

}
