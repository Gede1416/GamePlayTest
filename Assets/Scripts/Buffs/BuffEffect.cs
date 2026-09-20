/// <summary>
/// Buff 效果接口：一条 buff 生命周期里的三个动作。
/// 数值与持续回合由 BuffCfg 持有、**通过参数传进来**，所以实现不用存状态，
/// 一个类型一个实例就够（BuffFactory 里 new 出来直接复用）。
/// 注意「增加移动步数 / 增加攻击力」与「移除增加的移动步数 / 增加攻击力」**不是两个类型**：
/// 它们是同一个实现的两头——Apply 加上去、Revert 减回来；拆成两个类型就得各记一份数值，
/// 迟早和实际的加成对不上。
/// </summary>
public interface IBuffEffect
{
    /// <summary>挂上：常驻加成在这里生效</summary>
    void Apply(Entity target, BuffCfg cfg);

    /// <summary>每次结算：一次性的效果（治疗）在这里做</summary>
    void Tick(Entity target, BuffCfg cfg);

    /// <summary>移除：把 Apply 加上去的东西撤回来</summary>
    void Revert(Entity target, BuffCfg cfg);
}

/// <summary>治疗：自己没有常驻加成，每次结算给目标回 value 点血</summary>
public class HealEffect : IBuffEffect
{
    public void Apply(Entity target, BuffCfg cfg) { }

    public void Tick(Entity target, BuffCfg cfg) => target.Heal(cfg.value);

    public void Revert(Entity target, BuffCfg cfg) { }
}

/// <summary>增加移动步数：挂上时把步数加给 Entity，移除时减回去（本轮预算也跟着同步）</summary>
public class MoveStepsEffect : IBuffEffect
{
    public void Apply(Entity target, BuffCfg cfg) => target.AddMoveSteps((int)cfg.value);

    public void Tick(Entity target, BuffCfg cfg) { }

    public void Revert(Entity target, BuffCfg cfg) => target.AddMoveSteps(-(int)cfg.value);
}

/// <summary>增加攻击力：挂上时加给 Attacker 的伤害，移除时减回去</summary>
public class AttackEffect : IBuffEffect
{
    public void Apply(Entity target, BuffCfg cfg) => target.AddDamage(cfg.value);

    public void Tick(Entity target, BuffCfg cfg) { }

    public void Revert(Entity target, BuffCfg cfg) => target.AddDamage(-cfg.value);
}
