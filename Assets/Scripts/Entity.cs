using System.Collections;
using UnityEngine;

/// <summary>
/// 实体：一个角色身上组件的统一入口，并按枚举工厂装配寻路管线的三段。
/// 需要 ObjectMover + AutoPilot（没有会自动补），Health 可选（有就拿来定阵营）。
/// </summary>
[RequireComponent(typeof(ObjectMover))]
[RequireComponent(typeof(AutoPilot))]
public class Entity : MonoBehaviour
{
    [Tooltip("地图管理器；留空则取场景里的第一个")]
    public MapManager map;

    [Tooltip("先攻（回合管理器排序用）")]
    public int speed = 10;

    [Tooltip("每回合最多走几格；0 = 不限")]
    public int moveSteps;

    [Tooltip("阶段一（获得目标点）用哪种实现")]
    [SerializeField] TargetSourceType targetSourceType = TargetSourceType.ApproachNearestEnemy;

    /// <summary>自动寻路组件（管线的装配对象）</summary>
    public AutoPilot Pilot { get; private set; }

    /// <summary>移动组件</summary>
    public ObjectMover Mover { get; private set; }

    /// <summary>生命值组件，可能没有</summary>
    public Health Health { get; private set; }

    /// <summary>攻击管线组件，可能没有（没有就只移动不攻击）</summary>
    public Attacker Attacker { get; private set; }

    /// <summary>自己的阵营；没有 Health 就当 0</summary>
    public int Team => Health != null ? Health.team : 0;

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
        Mover = GetComponent<ObjectMover>();
        Pilot = GetComponent<AutoPilot>();
        Health = GetComponent<Health>();
        Attacker = GetComponent<Attacker>();
        if (map == null) map = FindObjectOfType<MapManager>();

        Wire();
        ApplySteps();
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
