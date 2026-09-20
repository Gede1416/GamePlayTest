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
/// 这里只做三件事——按名单造出来并装配（Build / Init）、每回合挨个跑三段（RunPipeline）、转发伤害加成（AddDamage）。
/// 额度（冷却 / 每回合一次 / 一场一次）不在这里：归各技能的释放判断自己管，数据靠消息送
/// （放成后这里发 SkillCastEvent、回合开头 TurnManager 发 TurnChangedEvent）。
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

    /// <summary>最近一次目标获取选出来的目标（各技能共用这块缓冲区）</summary>
    public readonly List<Entity> targets = new();

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

    /// <summary>按技能名单重建技能列表（判断 / 冷却都是新实例，等于额度归零）</summary>
    public void Build()
    {
        skills.Clear();

        foreach (var type in skillTypes)
        {
            var skill = CreateSkill(type);
            if (skill == null) continue;

            skill.Init(map);            // 地图发给各零件
            skills.Add(skill);
        }
    }

    /// <summary>
    /// 挨个跑技能：**判断 → 目标 → 释放**，三段都过才算放成（顺序固定）。
    /// 放成后发一条 SkillCastEvent（谁放的、哪条技能）——冷却与场次额度都是各检查收到这条消息自己记的，
    /// 所以放成没放成只需要看返回值，管理器不用管额度。
    /// </summary>
    public bool RunPipeline()
    {
        bool casted = false;

        foreach (var skill in skills)
        {
            if (!skill.CanCast(self)) continue;                 // 阶段一
            if (!skill.TryFindTargets(self, targets)) continue;  // 阶段二
            if (!skill.Cast(self, targets)) continue;            // 阶段三

            EventPipeline.Send(new SkillCastEvent(self, skill));
            casted = true;
        }

        return casted;
    }

    /// <summary>加 / 减攻击力（buff 用）：只有伤害类技能吃这个加成（转发给阶段三的 DamageCaster）</summary>
    public void AddDamage(float delta)
    {
        foreach (var skill in skills) skill.AddDamage(delta);
    }

    /// <summary>清理：清空技能与目标（额度 / 冷却跟着实例一起扔，下场按名单重建）（由 Entity.Clear 调）</summary>
    public void Clear()
    {
        skills.Clear();
        targets.Clear();
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
            ? $"[{name}] 放成了技能，目标 {targets.Count} 个"
            : $"[{name}] 没有技能放出来（释放判断没过 / 范围内没目标）");
    }

    [ContextMenu("打印技能")]
    void PrintSkills()
    {
        foreach (var skill in skills)
            Debug.Log($"[{name}] {skill.GetType().Name}：现在能放 {skill.CanCast(self)}", this);
    }

    #endregion

}
