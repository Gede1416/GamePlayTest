using UnityEngine;

/// <summary>攻击管线选哪几种实现——工厂按这个枚举造接口，Inspector 里直接选。</summary>
public enum AttackType
{
    /// <summary>近战：攻击范围 1 格、1 个目标</summary>
    Melee = 0,

    /// <summary>远程：攻击范围 3 格、1 个目标</summary>
    Ranged = 1,
}

/// <summary>
/// 攻击管线工厂：按枚举造出管线三段需要的接口实现，调用方（Attacker）只管把造好的东西装起来。
/// 和移动管线的 PathPipelineFactory 一个套路：阶段一/三目前只有一种实现，也走这里，换实现只改这个文件。
/// </summary>
public static class AttackPipelineFactory
{
    /// <summary>阶段一：技能释放判断</summary>
    public static ICastCheck CreateCastCheck(int cooldown = 0)
    {
        return new CooldownCastCheck(cooldown);
    }

    /// <summary>阶段二：目标获取（近战 / 远程的区别就在这一步）</summary>
    public static ITargetFinder CreateTargetFinder(AttackType type, MapManager map)
    {
        switch (type)
        {
            case AttackType.Ranged:
                return new RangedTargetFinder(map);
            default:
                return new MeleeTargetFinder(map);
        }
    }

    /// <summary>阶段三：技能释放</summary>
    public static ISkillCaster CreateCaster(float damage = 10f)
    {
        return new DamageCaster(damage);
    }

    /// <summary>一把装配好三段（按枚举选阶段二）</summary>
    public static void Wire(Attacker attacker, AttackType type, MapManager map, int cooldown, float damage)
    {
        if (attacker == null) return;

        attacker.CastCheck = CreateCastCheck(cooldown);
        attacker.TargetFinder = CreateTargetFinder(type, map);
        attacker.Caster = CreateCaster(damage);
    }
}
