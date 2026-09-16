using System.Collections;
using UnityEngine;

/// <summary>
/// 实体：一个角色身上组件的统一入口，也是对外唯一门面（回合 / 地图 / UI / 技能都只认 Entity）。
/// 数据所有权：身份(uuid)、先攻、回合步数、管线引用由 Entity 自己持有；
/// 生命值与阵营仍由 Health 持有（Entity 只转发，保证一份真相）；"技能"完全由管线组件承担
/// （移动管线 AutoPilot + 攻击管线 Attacker），Entity 不另存技能数据。
/// </summary>
[RequireComponent(typeof(ObjectMover))]
[RequireComponent(typeof(AutoPilot))]
public class Entity : MonoBehaviour
{
    [Header("数据")]
    [Tooltip("实体唯一 id：地图的 格子→uuid 二维图 与 uuid→位置 索引都用它（<= 0 会警告）")]
    [SerializeField] int uuid = 1;

    [Tooltip("先攻（回合排序用，大的先动）")]
    public int initiative = 10;

    [Tooltip("每回合最多走几格；0 = 不限")]
    public int moveSteps;

    [Tooltip("移动管线类型（阶段一）：靠近 / 远离")]
    [SerializeField] TargetSourceType targetSourceType = TargetSourceType.ApproachNearestEnemy;

    [Header("管线")]
    [Tooltip("地图管理器；留空则取场景里的第一个")]
    public MapManager map;

    /// <summary>自动寻路组件（移动管线的装配对象）</summary>
    public AutoPilot Pilot { get; private set; }

    /// <summary>移动组件</summary>
    public ObjectMover Mover { get; private set; }

    /// <summary>生命值组件，可能没有</summary>
    public Health Health { get; private set; }

    /// <summary>攻击管线组件，可能没有（没有就只移动不攻击）</summary>
    public Attacker Attacker { get; private set; }

    // ---------- 对外数据（借 Entity 报价，所有者见注释） ----------

    /// <summary>实体 id（Entity 自己持有）</summary>
    public int Uuid => uuid;

    /// <summary>当前生命值（Health 持有，这里转发）</summary>
    public float Hp => Health != null ? Health.Current : 0f;

    /// <summary>生命值上限（Health 持有，这里转发）</summary>
    public float MaxHp => Health != null ? Health.maxHealth : 0f;

    /// <summary>是否已阵亡（Health 持有，这里转发）</summary>
    public bool IsDead => Health != null && Health.IsDead;

    /// <summary>自己的阵营；没有 Health 就当 0</summary>
    public int Team => Health != null ? Health.team : 0;

    /// <summary>受到伤害：对外的唯一伤害入口，转发给生命值</summary>
    public void TakeDamage(float amount)
    {
        if (Health != null) Health.TakeDamage(amount);
    }

    /// <summary>向外暴露的枚举属性：外部改它就会立刻按新枚举重建阶段一</summary>
    public TargetSourceType SourceType
    {
        get => targetSourceType;
        set
        {
            targetSourceType = value;
            if (Pilot != null) Pilot.TargetSource = PathPipelineFactory.CreateSource(value, map, transform, Team, Mover);
        }
    }

    void Awake()
    {
        CacheComponents();
        if (map == null) map = FindObjectOfType<MapManager>();
        if (uuid <= 0) Debug.LogWarning($"{name}: uuid 没配（<= 0），地图索引会用不了", this);

        Init();     // 用场景里配好的数据装配
    }

    /// <summary>
    /// 初始化。不传 data：用场景里配好的（Inspector 字段）装配；
    /// 传了 data：先用 data 覆盖，再装配（管线按新类型重建、步数补满）。
    /// </summary>
    public void Init(EntityInitData data = null)
    {
        CacheComponents();

        if (data != null)
        {
            uuid = data.uuid;
            initiative = data.initiative;
            moveSteps = data.moveSteps;
            if (data.map != null) map = data.map;
            targetSourceType = data.moveSource;
            if (Attacker != null) Attacker.Type = data.attackType;
        }

        Wire();
        ApplySteps();
    }

    void CacheComponents()
    {
        if (Mover == null) Mover = GetComponent<ObjectMover>();
        if (Pilot == null) Pilot = GetComponent<AutoPilot>();
        if (Health == null) Health = GetComponent<Health>();
        if (Attacker == null) Attacker = GetComponent<Attacker>();
    }

    /// <summary>把每回合步数上限写给移动组件，并把剩余步数补满（改 moveSteps 后调它）</summary>
    public void ApplySteps()
    {
        if (Mover == null) return;
        Mover.stepLimit = moveSteps;
        Mover.ResetSteps();
    }

    /// <summary>按当前枚举装配管线三段（工厂造接口，这里塞进 AutoPilot）</summary>
    public void Wire()
    {
        PathPipelineFactory.Wire(Pilot, targetSourceType, map, transform, Team, Mover);
    }

    /// <summary>
    /// 轮到它行动的简单回合操作：**攻击 -> 移动 -> 攻击**。
    /// 移动是协程动画，所以这里是协程：等它走完再补第二次攻击（TurnManager 直接 yield 它）。
    /// </summary>
    public IEnumerator TakeTurnRoutine()
    {
        ApplySteps();                                        // 步数补满
        Attacker?.TickTurn();                                // 技能冷却推进

        AttackOnce();                                        // 攻击 1
        if (Pilot != null) Pilot.RunPipeline();              // 移动（找目标走过去）
        while (Pilot != null && Pilot.IsFollowing) yield return null;   // 等移动动画走完
        AttackOnce();                                        // 攻击 2
    }

    /// <summary>打一次：没有攻击组件 / 前置条件不满足 / 范围内没目标都会返回 false</summary>
    public bool AttackOnce()
    {
        return Attacker != null && Attacker.RunPipeline();
    }
}

/// <summary>
/// Entity.Init 需要的数据：只装 Entity 自己持有的那部分（身份 / 先攻 / 回合步数 / 管线类型 / 地图）。
/// 生命值与阵营归 Health 持有，所以不在这里。
/// </summary>
[System.Serializable]
public class EntityInitData
{
    [Tooltip("实体唯一 id")]
    public int uuid = 1;

    [Tooltip("先攻（大的先动）")]
    public int initiative = 10;

    [Tooltip("每回合最多走几格；0 = 不限")]
    public int moveSteps;

    [Tooltip("地图管理器；不填就用 Entity 场景里配的 / 自己找")]
    public MapManager map;

    [Tooltip("移动管线类型（阶段一）：靠近 / 远离")]
    public TargetSourceType moveSource = TargetSourceType.ApproachNearestEnemy;

    [Tooltip("攻击管线类型：近战 / 远程")]
    public Attacker.AttackType attackType = Attacker.AttackType.Melee;
}
