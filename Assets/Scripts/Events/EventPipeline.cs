using System;
using System.Collections.Generic;

/// <summary>
/// 基础事件管线（泛型）：按**事件类型**收发——订阅方 <c>Subscribe&lt;T&gt;(处理函数)</c>，发送方 <c>Send(事件对象)</c>。
/// 加新事件只要写一个新的数据类型，这个类一行都不用改；同一类型可以订多个，按订阅顺序回调。
/// 订阅在各自的 Init 里订、Clear 里退订（BattleManager.ClearBattle 末尾还会 Clear 兜一道）。
/// </summary>
public static class EventPipeline
{
    #region 属性

    // 事件类型 -> 该类型的处理链（multicast delegate）
    static readonly Dictionary<Type, Delegate> handlers = new();

    #endregion

    #region 公开方法

    /// <summary>发消息：这个类型没有订阅者就什么都不做</summary>
    public static void Send<T>(T e)
    {
        if (handlers.TryGetValue(typeof(T), out var chain)) (chain as Action<T>)?.Invoke(e);
    }

    /// <summary>订某种消息</summary>
    public static void Subscribe<T>(Action<T> handler)
    {
        if (handler == null) return;

        handlers.TryGetValue(typeof(T), out var chain);
        handlers[typeof(T)] = (chain as Action<T>) + handler;
    }

    /// <summary>退订</summary>
    public static void Unsubscribe<T>(Action<T> handler)
    {
        if (handler == null || !handlers.TryGetValue(typeof(T), out var chain)) return;

        var left = (chain as Action<T>) - handler;
        if (left == null) handlers.Remove(typeof(T));
        else handlers[typeof(T)] = left;
    }

    /// <summary>清掉全部订阅（清场时调）</summary>
    public static void Clear() => handlers.Clear();

    #endregion
}
