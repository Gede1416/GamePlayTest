using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能名（和 TargetSourceType 一个套路）：**Inspector 里只选这个**。
/// 技能名对应哪个组合技能由 SkillManager 自己的映射（CreateSkill）决定，技能的内容全在组合类里。
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
/// 技能管理器（挂在实体上，一个实体一个）：手里是**一组技能**（每个 ISkill 都是三段拼起来的一条技能）。
/// **Inspector 上只配技能名**（skillTypes 列表）：名字 → 组合技能由 SkillManager 自己的映射决定，
/// 这里只做四件事——按名单造出来（Build）、每回合挨个跑三段（RunPipeline）、记**使用次数缓存**、转发伤害加成（AddDamage）。
/// 额度（冷却 / 一场一次）不在这里：归各技能的释放判断自己管，判据就是这个使用次数缓存
/// （`skillUseCount`：技能名 → 放成过几次），判断通过施法者（Entity → 这里）取。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
[RequireComponent(typeof(Entity))]
public class SkillManager : MonoBehaviour
{
    #region 属性

    [Tooltip("这个实体有哪几个技能：只选技能名，内容（范围 / 伤害 / 冷却 / buff / 额度）在各自的组合技能里")]
    [SerializeField] List<SkillType> skillTypes = new();

    [Tooltip("地图管理器；留空则取场景里的第一个")]
    public MapManager map;

    /// <summary>使用次数缓存：技能名 → 这条技能这场放成过几次（放成后由 RunPipeline 记一笔，释放判断拿它认额度）</summary>
    public Dictionary<SkillType, int> skillUseCount = new();

    readonly List<ISkill> skills = new();     // 按 skillTypes 造出来的组合技能

    Entity self;

    /// <summary>现在的技能列表（只读；额度 / 冷却状态在各自的释放判断里）</summary>
    public IReadOnlyList<ISkill> Skills => skills;

    #endregion

    #region 公开方法

    /// <summary>初始化：由 Entity.Init 调用（地图由上级发下来），这里找身上的 Entity 并按名单造技能</summary>
    public void Init()
    {
        self = GetComponent<Entity>();
        Build();
    }

    /// <summary>按技能名单重建技能列表（判断 / 冷却都是新实例，等于额度归零；使用次数缓存一起清）</summary>
    public void Build()
    {
        skills.Clear();
        skillUseCount.Clear();

        foreach (var type in skillTypes)
        {
            var skill = CreateSkill(type);
            if (skill == null) continue;

            skills.Add(skill);
        }
    }

    /// <summary>
    /// 挨个跑技能：**判断 → 目标 → 释放**，三段都过才算放成（顺序固定）。
    /// 放成后把这条技能的使用次数记进缓存（<see cref="skillUseCount"/>）——冷却与场次额度都是各检查查这个数自己算的，
    /// 所以管理器不用管额度，只负责记账。
    /// </summary>
    public bool RunPipeline()
    {
        bool casted = false;

        foreach (var skill in skills)
        {
            if (!skill.CanCast(self)) continue;                     // 阶段一
            var found = skill.TryFindTargets(self, map);            // 阶段二（地图从这里给）
            if (found == null || found.Count == 0) continue;
            if (!skill.Cast(self, found)) continue;                 // 阶段三

            skillUseCount[skill.Type] = skill.UsedCount;            // 使用次数缓存：这条技能放过几次
            casted = true;
        }

        return casted;
    }

    /// <summary>某个技能名这场放成过几次（没记过就是 0）——技能管线的释放判断通过施法者问这里</summary>
    public int UsedCount(SkillType type)
    {
        return skillUseCount.TryGetValue(type, out int used) ? used : 0;
    }

    /// <summary>加 / 减攻击力（buff 用）：只有伤害类技能吃这个加成（转发给阶段三的 DamageCaster）</summary>
    public void AddDamage(float delta)
    {
        foreach (var skill in skills) skill.AddDamage(delta);
    }

    /// <summary>清理：清空技能与使用次数缓存（下场按名单重建）（由 Entity.Clear 调）</summary>
    public void Clear()
    {
        skills.Clear();
        skillUseCount.Clear();
        self = null;
    }

    #endregion

    #region 私有方法

    /// <summary>技能名 → 组合技能（加技能 = 加一个枚举 + 这里一个 case，内容写在 Combination 里）</summary>
    ISkill CreateSkill(SkillType type)
    {
        switch (type)
        {
            case SkillType.Melee: return new MeleeSkill();
            case SkillType.Ranged: return new RangedSkill();
            case SkillType.HealBuff: return new HealBuffSkill();
            case SkillType.AttackBuff: return new AttackBuffSkill();
        }

        Debug.LogWarning($"[{name}] {type} 没接实现");
        return null;
    }

    [ContextMenu("跑一次技能管线")]
    void RunMenu()
    {
        bool ok = RunPipeline();
        Debug.Log(ok
            ? $"[{name}] 放成了技能"
            : $"[{name}] 没有技能放出来（释放判断没过 / 范围内没目标）");
    }

    [ContextMenu("打印技能")]
    void PrintSkills()
    {
        foreach (var skill in skills)
            Debug.Log($"[{name}] {skill.Type}：这场放过 {UsedCount(skill.Type)} 次，现在能放 {skill.CanCast(self)}", this);
    }

    #endregion

}
