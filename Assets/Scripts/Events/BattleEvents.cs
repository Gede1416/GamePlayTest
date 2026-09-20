/// <summary>
/// 战斗里收发的各种消息。每个消息就是一个只读数据类（类型本身就是"频道"），
/// 加新消息只要在这里加一个类，EventPipeline 不用改。
/// 现在的收发：TurnChangedEvent（回合刷新 + 推技能冷却）/ DamageEvent（伤害数字）/ HealthChangedEvent（血条）/
/// EntityDiedEvent（放开格子 + 判胜负）/ EntitySpawnedEvent（挂血条）/ BuffChangedEvent（buff 条）/
/// BattleEndedEvent（显示胜方）。
/// </summary>

/// <summary>回合刷新：进入第 round 回合（共 totalRounds 回合）</summary>
public class TurnChangedEvent
{
    #region 属性

    /// <summary>第几回合（从 1 开始）</summary>
    public readonly int round;

    /// <summary>这局一共几回合</summary>
    public readonly int totalRounds;

    #endregion

    #region 构造

    public TurnChangedEvent(int round, int totalRounds)
    {
        this.round = round;
        this.totalRounds = totalRounds;
    }

    #endregion
}

/// <summary>造成伤害：谁被打、扣了多少（UI 用它飘伤害数字）</summary>
public class DamageEvent
{
    #region 属性

    /// <summary>被打的实体</summary>
    public readonly Entity target;

    /// <summary>这次扣了多少血</summary>
    public readonly float amount;

    #endregion

    #region 构造

    public DamageEvent(Entity target, float amount)
    {
        this.target = target;
        this.amount = amount;
    }

    #endregion
}

/// <summary>生命值变了：谁、现在多少、上限多少（血条用它刷新）</summary>
public class HealthChangedEvent
{
    #region 属性

    /// <summary>生命值变化的是谁</summary>
    public readonly Entity entity;

    /// <summary>当前生命值</summary>
    public readonly float current;

    /// <summary>生命值上限</summary>
    public readonly float max;

    #endregion

    #region 构造

    public HealthChangedEvent(Entity entity, float current, float max)
    {
        this.entity = entity;
        this.current = current;
        this.max = max;
    }

    #endregion
}

/// <summary>有实体阵亡：谁死了、哪个阵营（地图放开格子、战斗管理器判胜负）</summary>
public class EntityDiedEvent
{
    #region 属性

    /// <summary>阵亡的实体</summary>
    public readonly Entity entity;

    /// <summary>死者阵营</summary>
    public readonly int team;

    #endregion

    #region 构造

    public EntityDiedEvent(Entity entity, int team)
    {
        this.entity = entity;
        this.team = team;
    }

    #endregion
}

/// <summary>实体就绪：加载 + 初始化完成，UI 可以给它挂血条之类</summary>
public class EntitySpawnedEvent
{
    #region 属性

    /// <summary>就绪的实体</summary>
    public readonly Entity entity;

    #endregion

    #region 构造

    public EntitySpawnedEvent(Entity entity)
    {
        this.entity = entity;
    }

    #endregion
}

/// <summary>某个实体的 buff 有变化：挂上 / 到期移除 / 清场（buff 条收到后重新读一遍它的 buff 列表）</summary>
public class BuffChangedEvent
{
    #region 属性

    /// <summary>buff 变动的实体</summary>
    public readonly Entity entity;

    #endregion

    #region 构造

    public BuffChangedEvent(Entity entity)
    {
        this.entity = entity;
    }

    #endregion
}

/// <summary>战斗结束：胜方阵营（-1 = 打平 / 全灭）</summary>
public class BattleEndedEvent
{
    #region 属性

    /// <summary>胜方阵营；-1 表示没有幸存者</summary>
    public readonly int winnerTeam;

    #endregion

    #region 构造

    public BattleEndedEvent(int winnerTeam)
    {
        this.winnerTeam = winnerTeam;
    }

    #endregion
}

/// <summary>
/// 技能放成了：谁放的（施法者的 uuid）、哪条技能（技能名）。
/// 各释放判断收到后拿自己的施法者 id 与技能名跟它比对，对上了才记自己的账（进冷却 / 记一场一次），
/// 所以"放成没放成"这条判断拿不到的数据是靠消息送过去的。
/// </summary>
public class SkillCastEvent
{
    #region 属性

    /// <summary>施法者的 uuid（跟 Entity.Uuid 对）</summary>
    public readonly int uuid;

    /// <summary>放成的那条技能（技能名）</summary>
    public readonly SkillType skillType;

    #endregion

    #region 构造

    public SkillCastEvent(int uuid, SkillType skillType)
    {
        this.uuid = uuid;
        this.skillType = skillType;
    }

    #endregion
}
