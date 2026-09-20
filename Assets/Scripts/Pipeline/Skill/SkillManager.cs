using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能管理器（挂在实体上，一个实体一个）：手里是**一组技能**。
/// **Inspector 上只配技能名**（`skillTypes` 列表）——技能的完整内容（三段流水线 + 数值 + 额度）
/// 由 SkillFactory 按技能名给出（SkillDefinition），这里只负责按名单造出来、每回合挨个尝试。
/// 额度归 Skill：每个技能每回合最多放一次（放成了才算），定义里勾了 oncePerBattle 的一整场只放一次。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
[RequireComponent(typeof(Entity))]
public class SkillManager : MonoBehaviour
{
    #region 属性

    [Tooltip("这个实体有哪几个技能：只选技能名，具体内容（范围 / 伤害 / 冷却 / buff / 额度）在 SkillFactory 里定义")]
    [SerializeField] List<SkillType> skillTypes = new();

    [Tooltip("地图管理器；留空则取场景里的第一个")]
    public MapManager map;

    /// <summary>最近一次目标获取选出来的目标（各技能共用这块缓冲区）</summary>
    public readonly List<Entity> targets = new();

    readonly List<Skill> skills = new();     // 按 skillTypes 造出来的运行期技能

    Entity self;

    /// <summary>现在的技能列表（只读；额度状态在各自的 Skill 里）</summary>
    public IReadOnlyList<Skill> Skills => skills;

    #endregion

    #region 公开方法

    /// <summary>初始化：由 Entity.Init 调用（地图由上级发下来），这里找身上的 Entity 并按名单造技能</summary>
    public void Init()
    {
        self = GetComponent<Entity>();
        Build();
    }

    /// <summary>按技能名单重建技能列表（重置所有额度：每回合一次 / 每场一次的计数都归零）</summary>
    public void Build()
    {
        skills.Clear();
        foreach (var type in skillTypes)
        {
            var definition = SkillFactory.Create(type, map);
            if (definition == null) continue;

            skills.Add(new Skill(definition));
        }
    }

    /// <summary>挨个尝试放技能（每个技能每回合最多一次）：返回这次有没有放成过</summary>
    public bool RunPipeline()
    {
        bool casted = false;
        foreach (var skill in skills)
        {
            if (skill.TryCast(self, targets)) casted = true;
        }
        return casted;
    }

    /// <summary>回合推进（自己的回合开始调）：清掉"本回合放过"的额度 + 推各技能的冷却</summary>
    public void TickTurn()
    {
        foreach (var skill in skills) skill.TickTurn();
    }

    /// <summary>加 / 减攻击力（buff 用）：只有伤害类技能吃这个加成（走阶段三的 DamageCaster）</summary>
    public void AddDamage(float delta)
    {
        foreach (var skill in skills) skill.AddDamage(delta);
    }

    /// <summary>清理：清空技能与目标（额度也跟着没了，下次 Init 按名单重建）（由 Entity.Clear 调）</summary>
    public void Clear()
    {
        skills.Clear();
        targets.Clear();
        self = null;
    }

    #endregion

    #region 私有方法

    [ContextMenu("跑一次技能管线")]
    void RunMenu()
    {
        bool ok = RunPipeline();
        Debug.Log(ok
            ? $"[{name}] 放成了技能，目标 {targets.Count} 个"
            : $"[{name}] 没有技能放出来（额度用完 / 前置条件不满足 / 范围内没目标）");
    }

    [ContextMenu("打印技能与额度")]
    void PrintSkills()
    {
        foreach (var skill in skills)
        {
            var definition = skill.Definition;
            Debug.Log($"[{name}] {skill.Type}：伤害 {definition.Damage}，"
                      + $"每场一次 {definition.oncePerBattle}，本场放过 {skill.UsedThisBattle}，现在能放 {skill.CanUse}", this);
        }
    }

    #endregion

}
