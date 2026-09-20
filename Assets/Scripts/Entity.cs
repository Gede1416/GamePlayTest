using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 实体：组件的统一入口，也是对外唯一门面（回合 / 地图 / UI 只认 Entity）。
/// 数据所有权：身份(uuid)、先攻、回合步数、管线引用由 Entity 自己持有；
/// 生命值与阵营由 Health 持有（这里只转发）；"技能"由管线组件承担
/// （移动管线 AutoPilot + 技能管线 SkillManager），技能内容在各条组合技能里。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
[RequireComponent(typeof(AutoPilot))]
public class Entity : MonoBehaviour
{
    #region 属性

    [Header("数据")][Tooltip("实体唯一 id：地图的 格子→uuid 二维图 与 uuid→位置 索引都用它（<= 0 会警告）")]
    [SerializeField] int uuid = 1;

    [Tooltip("先攻（回合排序用，大的先动）")]
    public int initiative = 10;

    [Tooltip("每回合最多走几格；0 = 不限（由 AutoPilot.ApplySteps 记下来，只有\"远离\"阶段一挑落点时读它）")]
    public int moveSteps;

    [Header("管线")][Tooltip("地图管理器；留空则取场景里的第一个")]
    public MapManager map;

    [Tooltip("UI 点位：血条 / 伤害数字挂在它下面；留空就在 Init 时于头顶自动建一个")]
    public Transform uiPoint;

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

    /// <summary>自动寻路组件（移动管线的装配对象，阶段三自己控制位置）</summary>
    public AutoPilot Pilot { get; private set; }

    /// <summary>生命值组件，可能没有</summary>
    public Health Health { get; private set; }

    /// <summary>技能管线组件（手里是一组技能），可能没有（没有就只移动不放技能）</summary>
    public SkillManager Skills { get; private set; }

    /// <summary>Buff 管理器，可能没有（没有就挂不上 buff）</summary>
    public BuffManager Buffs { get; private set; }

    DeathEffect deathEffect;      // 阵亡表现组件，可能没有（没有就直接停用自己）

    /// <summary>移动管线类型：数据归 AutoPilot，这里只是转发（改它会重建移动管线阶段一）</summary>
    public TargetSourceType SourceType
    {
        get => Pilot != null ? Pilot.SourceType : TargetSourceType.ApproachNearestEnemy;
        set { if (Pilot != null) Pilot.SourceType = value; }
    }

    #endregion

    #region 公开方法

    /// <summary>
    /// 初始化：由上级（BattleManager）调用，自己再把初始化发给身上的组件（Health → DeathEffect → Buffs → AutoPilot → Skills）。
    /// 不传 data：用预制体里配好的（Inspector 字段）装配；
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
            SourceType = data.moveSource;
        }

        if (uuid <= 0) Debug.LogWarning($"{name}: uuid 没配（<= 0），地图索引会用不了", this);
        if (map == null) Debug.LogWarning($"{name}: map 没配，移动 / 技能管线拿不到地图数据", this);

        if (uiPoint == null) uiPoint = CreateUiPoint();      // UI 都挂在它下面

        // 地图由上级发下来，各组件只认自己的 Init（不再依赖 Awake / Start）
        if (Health != null) Health.Init();

        if (deathEffect != null) deathEffect.Init();

        if (Buffs != null) Buffs.Init();

        if (Pilot != null)
        {
            Pilot.map = map;
            Pilot.Init();
            Pilot.ApplySteps(moveSteps);
        }

        if (Skills != null)
        {
            Skills.map = map;
            Skills.Init();
        }
    }

    /// <summary>受到伤害：对外的唯一伤害入口，转发给生命值</summary>
    public void TakeDamage(float amount)
    {
        if (Health != null) Health.TakeDamage(amount);
    }

    /// <summary>治疗：转发给生命值（buff 的"治疗"效果走这里，扣血 / 回血都从 Entity 进）</summary>
    public void Heal(float amount)
    {
        if (Health != null) Health.Heal(amount);
    }

    /// <summary>加 / 减每回合可走步数（buff 用；步数是 Entity 的数据，顺手把本回合预算同步给移动管线）</summary>
    public void AddMoveSteps(int delta)
    {
        moveSteps = Mathf.Max(0, moveSteps + delta);
        if (Pilot != null) Pilot.ApplySteps(moveSteps);
    }

    /// <summary>加 / 减攻击力（buff 用，转发给 SkillManager：伤害归各技能的阶段三持有）</summary>
    public void AddDamage(float delta)
    {
        if (Skills != null) Skills.AddDamage(delta);
    }

    /// <summary>挂一条 buff：targets 留空就挂给自己（转发给 BuffManager；身上没这个组件就什么都不做）</summary>
    public void AddBuff(BuffType type, List<Entity> targets = null)
    {
        if (Buffs != null) Buffs.Add(type, targets);
    }

    /// <summary>
    /// 轮到它行动的简单回合操作：**放技能 -> 移动 -> 再放技能**。
    /// 每个技能的额度（冷却 / 一场一次）由它自己的释放判断记账，这里不用管：
    /// 判断靠 SkillManager 放成后发的 **SkillCastEvent**（施法者 uuid + 技能名）认自己那条技能，冷却每大回合收 `TurnChangedEvent` 自己减，
    /// 所以第二次只会补放"开局够不着、走完才够得着"的那次（放成过的会被判断挡住）；
    /// 移动是协程动画，所以这里是协程：等它走完再补第二次（TurnManager 直接 yield 它）。
    /// </summary>
    public IEnumerator TakeTurnRoutine()
    {
        Buffs?.TickTurn();                                       // 先结算 buff（治疗 / 到期加成），再按最新数值行动
        Pilot?.ApplySteps(moveSteps);                            // 步数补满

        Skills?.RunPipeline();                                   // 放技能 1
        if (Pilot != null) Pilot.RunPipeline();                  // 移动（找目标走过去）
        while (Pilot != null && Pilot.IsFollowing) yield return null;   // 等移动动画走完
        Skills?.RunPipeline();                                   // 放技能 2（放成过的会被各自的释放判断挡住）
    }

    /// <summary>清理：把清理发给身上的组件（Skills → AutoPilot → Buffs → DeathEffect → Health，与初始化相反的顺序），由 BattleManager 统一调</summary>
    public void Clear()
    {
        if (Skills != null) Skills.Clear();
        if (Pilot != null) Pilot.Clear();
        if (Buffs != null) Buffs.Clear();
        if (deathEffect != null) deathEffect.Clear();
        if (Health != null) Health.Clear();
    }

    #endregion

    #region 私有方法

    /// <summary>没配 UI 点位就自动建一个（头顶 +1），血条与伤害数字挂它下面</summary>
    Transform CreateUiPoint()
    {
        var go = new GameObject("UI");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1f, 0f);
        return go.transform;
    }

    void CacheComponents()
    {
        if (Pilot == null) Pilot = GetComponent<AutoPilot>();
        if (Health == null) Health = GetComponent<Health>();
        if (Skills == null) Skills = GetComponent<SkillManager>();
        if (Buffs == null) Buffs = GetComponent<BuffManager>();
        if (deathEffect == null) deathEffect = GetComponent<DeathEffect>();
    }

    #endregion

}

/// <summary>
/// Entity.Init 需要的数据：只装 Entity 自己持有的那部分（身份 / 先攻 / 回合步数 / 移动管线类型 / 地图）。
/// 生命值与阵营归 Health 持有、技能归 SkillManager 的配置持有，所以都不在这里。
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
}
