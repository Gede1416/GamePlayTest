using System.Collections.Generic;

/// <summary>
/// 一条 buff 的配置：类型 + 数值 + 持续回合（&lt;= 0 = 永久）。
/// 它是这份数值的**唯一所有者**：效果实现不自己存数值，靠参数拿（见 IBuffEffect）。
/// 先做成普通数据类（BuffFactory 里给常量），以后要配表 / 做成资产再说。
/// </summary>
[System.Serializable]
public class BuffCfg
{
    public BuffType type;
    public float value;
    public int duration;

    public BuffCfg(BuffType type, float value, int duration)
    {
        this.type = type;
        this.value = value;
        this.duration = duration;
    }
}

/// <summary>
/// 一条正在生效的 buff：配置 + 效果实现 + 影响的目标，三步生命周期和模板一致：
/// <c>Add()</c> 挂上 → 每次结算 <c>Trigger()</c> → 过期（或清场）<c>Remove()</c>。
/// 持续回合按**结算次数**算：duration = 3 表示还会结算 3 次，结算满 3 次就该移除；duration &lt;= 0 表示永久。
/// </summary>
public class Buff
{
    #region 属性

    readonly BuffCfg cfg;
    readonly IBuffEffect effect;
    readonly List<Entity> targets;
    int elapsed;                    // 已经结算过几次

    /// <summary>这条 buff 的配置（类型 / 数值 / 持续回合）</summary>
    public BuffCfg Cfg => cfg;

    /// <summary>是否该结束了（永久 buff 永远不结束）</summary>
    public bool IsOver => cfg.duration > 0 && elapsed >= cfg.duration;

    #endregion

    #region 构造

    public Buff(BuffCfg cfg, IBuffEffect effect, List<Entity> targets)
    {
        this.cfg = cfg;
        this.effect = effect;
        this.targets = targets;
    }

    #endregion

    #region 公开方法

    /// <summary>挂上：对每个目标 Apply（治疗这种要等 Trigger 才生效）</summary>
    public void Add()
    {
        foreach (var target in targets)
        {
            if (target != null) effect.Apply(target, cfg);
        }
    }

    /// <summary>结算一次：对每个目标 Tick，然后记一次已结算</summary>
    public void Trigger()
    {
        foreach (var target in targets)
        {
            if (target != null) effect.Tick(target, cfg);
        }
        elapsed++;
    }

    /// <summary>移除：对每个目标 Revert，把加成撤回去（清场时也会调，别把加成留在实体上）</summary>
    public void Remove()
    {
        foreach (var target in targets)
        {
            if (target != null) effect.Revert(target, cfg);
        }
    }

    #endregion
}
