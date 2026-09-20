using UnityEngine;

/// <summary>
/// 技能名（枚举跟着工厂走，和 TargetSourceType 一个套路）：
/// **Inspector 里只选这个**，技能的其余内容（范围 / 伤害 / 冷却 / 挂哪种 buff / 额度）都在 SkillFactory 里定死。
/// </summary>
public enum SkillType
{
    /// <summary>近战：范围 1 格、1 个目标、伤害 20、冷却 1 回合</summary>
    Melee = 0,

    /// <summary>远程：范围 3 格、1 个目标、伤害 10、冷却 1 回合</summary>
    Ranged = 1,

    /// <summary>治疗：给自己挂 Heal buff（回 10 × 3 回合），血量没满才放、不限场次</summary>
    HealBuff = 2,

    /// <summary>加攻：给自己挂 AddAttack buff（+5 × 3 回合），一场战斗只放一次</summary>
    AttackBuff = 3,
}

/// <summary>
/// 技能工厂：**技能名 → 一份完整技能定义**（三段 + 数值 + 额度都在这儿定）。
/// 原来散在 Inspector 上的东西都收敛到这里：加技能 = 加一个 SkillType + 这里加一个 case，
/// 改数值 = 改这里的常量；预制体上只留"这个实体有哪几个技能"。
/// 每次 Create 都造**新实例**——冷却剩余、被 buff 加过的伤害都是运行期状态，不能在实体之间共享。
/// 只有一组公开静态方法的工厂类，按约定不分块。
/// </summary>
public static class SkillFactory
{
    // ---------- 数值都在这（技能定义表） ----------
    const float MeleeDamage = 20f;
    const int MeleeCooldown = 1;
    const float RangedDamage = 10f;
    const int RangedCooldown = 1;
    const BuffType HealBuff = BuffType.Heal;
    const BuffType AttackBuff = BuffType.AddAttack;

    /// <summary>按技能名造一份完整定义（新实例；阶段一 / 二 / 三按技能名选实现）</summary>
    public static SkillDefinition Create(SkillType type, MapManager map)
    {
        switch (type)
        {
            case SkillType.Melee:
                return new SkillDefinition(type,
                    new CooldownCastCheck(MeleeCooldown),
                    new MeleeTargetFinder(map),
                    new DamageCaster(MeleeDamage));

            case SkillType.Ranged:
                return new SkillDefinition(type,
                    new CooldownCastCheck(RangedCooldown),
                    new RangedTargetFinder(map),
                    new DamageCaster(RangedDamage));

            case SkillType.HealBuff:
                return new SkillDefinition(type,
                    new WoundedCastCheck(),             // 血量没满才放
                    new SelfTargetFinder(),
                    new BuffCaster(HealBuff));

            case SkillType.AttackBuff:
                return new SkillDefinition(type,
                    new CooldownCastCheck(),
                    new SelfTargetFinder(),
                    new BuffCaster(AttackBuff),
                    oncePerBattle: true);               // 一场战斗只放一次
        }

        Debug.LogWarning($"SkillFactory：{type} 没接实现");
        return null;
    }
}
