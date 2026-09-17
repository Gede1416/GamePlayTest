using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 攻击管线（挂在实体上）：阶段一 技能释放判断 -> 阶段二 目标获取 -> 阶段三 技能释放。
/// 三段各自独立（不依赖技能对象），装配交给 AttackPipelineFactory（按 AttackType 枚举造），
/// 本组件只负责驱动流程。
/// </summary>
[RequireComponent(typeof(Entity))]
public class Attacker : MonoBehaviour
{
    [Tooltip("攻击类型：近战 范围 1 / 远程 范围 3，都是 1 个目标")]
    [SerializeField] AttackType attackType = AttackType.Melee;

    [Tooltip("每次命中的伤害（给阶段三）")]
    public float damage = 10f;

    [Tooltip("冷却回合数（给阶段一）；0 = 无冷却")]
    public int cooldown;

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

    /// <summary>向外暴露的攻击类型：外部改它就会按新类型重建三段</summary>
    public AttackType Type
    {
        get => attackType;
        set
        {
            attackType = value;
            Build();
        }
    }

    /// <summary>按当前攻击类型装配三段（工厂造接口，这里只负责装上；也可以外部塞别的实现进来）</summary>
    public void Build()
    {
        AttackPipelineFactory.Wire(this, attackType, map, cooldown, damage);
    }

    /// <summary>攻击管线：技能释放判断 -> 目标获取 -> 技能释放</summary>
    public bool RunPipeline()
    {
        if (CastCheck == null || TargetFinder == null || Caster == null) return false;

        if (!CastCheck.CanCast(self)) return false;                     // 阶段一
        if (!TargetFinder.TryFindTargets(self, targets)) return false;  // 阶段二
        if (!Caster.Cast(self, targets)) return false;                  // 阶段三

        (CastCheck as ICooldown)?.StartCooldown();                      // 放成了才进冷却
        return true;
    }

    /// <summary>回合推进：冷却减一（想让技能有冷却就在每回合开始调它）</summary>
    public void TickTurn()
    {
        (CastCheck as ICooldown)?.TickTurn();
    }

    // ---------- 私有 ----------

    Entity self;

    void Awake()
    {
        self = GetComponent<Entity>();
        if (map == null) map = FindObjectOfType<MapManager>();

        Build();
    }

    [ContextMenu("跑一次攻击管线")]
    void RunMenu()
    {
        bool ok = RunPipeline();
        Debug.Log(ok
            ? $"[{name}] {attackType} 命中 {targets.Count} 个目标"
            : $"[{name}] {attackType} 没放出来（前置条件不满足 / 范围内没目标）");
    }
}
