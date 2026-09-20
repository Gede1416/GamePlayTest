using System.Collections.Generic;
using UnityEngine;

// ============ 技能管线三段接口 ============
// 阶段一：技能释放判断 -> 阶段二：目标获取 -> 阶段三：技能释放
// 三段都**不依赖任何技能对象**：范围、目标数、伤害、挂哪种 buff 这些数据由各自的实现自己带（构造函数注入），
// 地图等外部依赖同样走构造函数注入。额度（每回合一次 / 每场一次）不在这里，归 Skill 管。

/// <summary>阶段一：技能释放判断——这次能不能放（自己判断，不看别人）</summary>
public interface ICastCheck
{
    bool CanCast(Entity caster);
}

/// <summary>阶段二：目标获取——选出这次要作用的目标</summary>
public interface ITargetFinder
{
    bool TryFindTargets(Entity caster, List<Entity> targets);
}

/// <summary>阶段三：技能释放——对目标附加效果（扣血 / 挂 buff）</summary>
public interface ISkillCaster
{
    bool Cast(Entity caster, List<Entity> targets);
}

/// <summary>带冷却的阶段一实现再实现它，Skill 释放成功后会调 StartCooldown、每回合调 TickTurn</summary>
public interface ICooldown
{
    int CooldownLeft { get; }
    void StartCooldown();
    void TickTurn();
}

// ============ 阶段一：释放判断 ============

/// <summary>默认的释放判断：施法者活着 + 冷却好了（冷却自己数）</summary>
public class CooldownCastCheck : ICastCheck, ICooldown
{
    readonly int cooldown;

    public int CooldownLeft { get; private set; }

    public CooldownCastCheck(int cooldown = 0)
    {
        this.cooldown = cooldown;
    }

    /// <summary>进冷却：放成一次技能后由 Skill 调</summary>
    public void StartCooldown() => CooldownLeft = cooldown;

    /// <summary>回合推进：冷却减一</summary>
    public void TickTurn()
    {
        if (CooldownLeft > 0) CooldownLeft--;
    }

    /// <summary>能不能放：施法者活着 且 冷却已经好了</summary>
    public bool CanCast(Entity caster)
    {
        if (caster == null || caster.Health == null || caster.Health.IsDead) return false;
        return CooldownLeft <= 0;
    }
}

// ============ 阶段二：目标获取 ============

/// <summary>两个攻击目标获取共用的挑选逻辑：范围内按曼哈顿距离由近到远取前 targetCount 个非己方</summary>
public static class TargetPicker
{
    public static bool Pick(MapManager map, Entity caster, int range, int targetCount, List<Entity> targets)
    {
        targets.Clear();
        if (map == null || caster == null || targetCount <= 0) return false;

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

        if (targets.Count == 0) return false;

        targets.Sort((a, b) => Dist(map, self, a).CompareTo(Dist(map, self, b)));          // 近的优先
        if (targets.Count > targetCount)
            targets.RemoveRange(targetCount, targets.Count - targetCount);                 // 目标数量上限

        return true;
    }

    #region 私有方法

    static int Dist(MapManager map, Vector2Int from, Entity e)
    {
        return MapManager.Manhattan(from, map.WorldToCell(e.transform.position));
    }

    #endregion

}

/// <summary>近战目标获取：范围 1 格、1 个目标</summary>
public class MeleeTargetFinder : ITargetFinder
{
    public const int Range = 1;
    public const int TargetCount = 1;

    readonly MapManager map;

    public MeleeTargetFinder(MapManager map)
    {
        this.map = map;
    }

    /// <summary>按范围挑目标：Range 格内的 1 个最近的非己方</summary>
    public bool TryFindTargets(Entity caster, List<Entity> targets)
    {
        return TargetPicker.Pick(map, caster, Range, TargetCount, targets);
    }
}

/// <summary>远程目标获取：范围 3 格、1 个目标</summary>
public class RangedTargetFinder : ITargetFinder
{
    public const int Range = 3;
    public const int TargetCount = 1;

    readonly MapManager map;

    public RangedTargetFinder(MapManager map)
    {
        this.map = map;
    }

    /// <summary>按范围挑目标：Range 格内的 1 个最近的非己方</summary>
    public bool TryFindTargets(Entity caster, List<Entity> targets)
    {
        return TargetPicker.Pick(map, caster, Range, TargetCount, targets);
    }
}

/// <summary>给自己上 buff 这类技能用：目标就是自己，永远找得到（活着才放得出去由阶段一管）</summary>
public class SelfTargetFinder : ITargetFinder
{
    /// <summary>把施法者自己当成唯一目标</summary>
    public bool TryFindTargets(Entity caster, List<Entity> targets)
    {
        targets.Clear();
        if (caster == null) return false;

        targets.Add(caster);
        return true;
    }
}

// ============ 阶段三：技能释放 ============

/// <summary>伤害类技能的释放：对每个目标扣 damage 血（伤害自己带）</summary>
public class DamageCaster : ISkillCaster
{
    public float damage;

    public DamageCaster(float damage = 10f)
    {
        this.damage = damage;
    }

    /// <summary>加 / 减伤害（buff 用；delta 为负就是减回去）</summary>
    public void AddDamage(float delta) => damage += delta;

    /// <summary>对每个目标扣 damage 血；没有目标就返回 false</summary>
    public bool Cast(Entity caster, List<Entity> targets)
    {
        if (caster == null || targets == null || targets.Count == 0) return false;

        // 伤害只从 Entity 这个门面进去（Entity.TakeDamage 再转给 Health）
        foreach (var t in targets)
            if (t != null) t.TakeDamage(damage);

        Debug.Log($"[DamageCaster] {caster.name} 命中 {targets.Count} 个目标，每个 {damage} 伤害");
        return true;
    }
}

/// <summary>Buff 类技能的释放：给每个目标挂一条 buff（挂哪种由构造函数带）</summary>
public class BuffCaster : ISkillCaster
{
    readonly BuffType buff;

    public BuffCaster(BuffType buff)
    {
        this.buff = buff;
    }

    /// <summary>对每个目标挂 buff（走 Entity.AddBuff 门面，目标自己的 BuffManager 收下来）</summary>
    public bool Cast(Entity caster, List<Entity> targets)
    {
        if (caster == null || targets == null || targets.Count == 0) return false;

        foreach (var t in targets)
            if (t != null) t.AddBuff(buff);

        Debug.Log($"[BuffCaster] {caster.name} 放下 {buff}");
        return true;
    }
}
