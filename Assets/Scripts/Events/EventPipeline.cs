using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>战斗事件类型</summary>
public enum BattleEventType
{
    /// <summary>有实体阵亡：谁死了看 BattleEvent.entity，死者阵营看 team</summary>
    EntityDied = 0,

    /// <summary>战斗结束：胜方阵营看 team（-1 = 打平 / 全灭）</summary>
    BattleEnded = 1,
}

/// <summary>战斗事件：一个类型 + 这次要带的少量数据（谁、哪个阵营）。</summary>
public class BattleEvent
{
    #region 属性

    public readonly BattleEventType type;

    /// <summary>事件相关的实体（死亡事件里是死者；别的类型可以为 null）</summary>
    public readonly Entity entity;

    /// <summary>死亡事件 = 死者阵营；结束事件 = 胜方阵营（-1 = 打平 / 全灭）</summary>
    public readonly int team;

    #endregion

    #region 构造

    public BattleEvent(BattleEventType type, Entity entity = null, int team = -1)
    {
        this.type = type;
        this.entity = entity;
        this.team = team;
    }

    #endregion

    #region 公开方法

    /// <summary>死亡事件：传死者和它的阵营</summary>
    public static BattleEvent EntityDied(Entity entity, int team) => new BattleEvent(BattleEventType.EntityDied, entity, team);

    /// <summary>结束事件：传胜方阵营（-1 = 打平 / 全灭）</summary>
    public static BattleEvent BattleEnded(int winnerTeam) => new BattleEvent(BattleEventType.BattleEnded, null, winnerTeam);

    #endregion
}

/// <summary>
/// 基础事件管线：只管收发——发送方 Send，订阅方按类型收（每种类型可以订多个，按订阅顺序回调）。
/// 订阅在初始化时订（BattleManager.InitBattle 订死亡消息），清场时 Clear 掉（BattleManager.ClearBattle），
/// 免得旧的订阅留在静态表里对着已经销毁的对象回调。
/// </summary>
public static class EventPipeline
{
    #region 属性

    static readonly Dictionary<BattleEventType, Action<BattleEvent>> handlers = new();

    #endregion

    #region 公开方法

    /// <summary>发消息：这个类型没有订阅者就什么都不做</summary>
    public static void Send(BattleEvent e)
    {
        if (e == null) return;
        if (handlers.TryGetValue(e.type, out var handler)) handler?.Invoke(e);
    }

    /// <summary>订某种消息</summary>
    public static void Subscribe(BattleEventType type, Action<BattleEvent> handler)
    {
        if (handler == null) return;

        handlers.TryGetValue(type, out var existing);
        handlers[type] = existing + handler;
    }

    /// <summary>退订</summary>
    public static void Unsubscribe(BattleEventType type, Action<BattleEvent> handler)
    {
        if (handler == null || !handlers.TryGetValue(type, out var existing)) return;

        var left = existing - handler;
        if (left == null) handlers.Remove(type);
        else handlers[type] = left;
    }

    /// <summary>清掉全部订阅（清场时调）</summary>
    public static void Clear() => handlers.Clear();

    #endregion
}
