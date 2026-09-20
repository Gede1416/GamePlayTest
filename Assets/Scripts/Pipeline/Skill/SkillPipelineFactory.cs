using UnityEngine;

/// <summary>
/// 技能类型（枚举跟着工厂走，和移动管线的 TargetSourceType 一个套路）：
/// 决定阶段二（目标获取）与阶段三（释放）选哪种实现。
/// </summary>
public enum SkillType
{
    /// <summary>近战：范围 1 格、1 个目标、造成伤害</summary>
    Melee = 0,

    /// <summary>远程：范围 3 格、1 个目标、造成伤害</summary>
    Ranged = 1,

    /// <summary>施放 buff：目标是自己、给目标挂一条 <see cref="BuffType"/></summary>
    Buff = 2,
}

/// <summary>
/// 技能管线工厂：按技能配置造出三段需要的接口实现，调用方（SkillManager）只管把造好的东西装到 Skill 上。
/// 和移动管线的 PathPipelineFactory 一个套路：阶段一目前只有一种实现，也走这里，换实现只改这个文件。
/// </summary>
public static class SkillPipelineFactory
{
    /// <summary>阶段一：技能释放判断（活着 + 冷却好了）</summary>
    public static ICastCheck CreateCastCheck(int cooldown = 0)
    {
        return new CooldownCastCheck(cooldown);
    }

    /// <summary>阶段二：目标获取（近战 / 远程 / 给自己上 buff 的区别就在这一步）</summary>
    public static ITargetFinder CreateTargetFinder(SkillType type, MapManager map)
    {
        switch (type)
        {
            case SkillType.Ranged:
                return new RangedTargetFinder(map);
            case SkillType.Buff:
                return new SelfTargetFinder();
            default:
                return new MeleeTargetFinder(map);
        }
    }

    /// <summary>阶段三：技能释放（伤害类扣血 / Buff 类挂 buff）</summary>
    public static ISkillCaster CreateCaster(SkillCfg cfg)
    {
        return cfg.type == SkillType.Buff ? new BuffCaster(cfg.buff) : new DamageCaster(cfg.damage);
    }

    /// <summary>给一条技能装好三段（由 SkillManager.Build 调）</summary>
    public static void Wire(Skill skill, SkillCfg cfg, MapManager map)
    {
        if (skill == null || cfg == null) return;

        skill.CastCheck = CreateCastCheck(cfg.cooldown);
        skill.TargetFinder = CreateTargetFinder(cfg.type, map);
        skill.Caster = CreateCaster(cfg);
    }
}
