using System.Collections.Generic;

/// <summary>给自己上 buff 这类技能用：目标就是自己（活着才放得出去由阶段一管）。不需要地图</summary>
public class SelfTargetFinder : ITargetFinder
{
    #region 公开方法

    /// <summary>把施法者自己当成唯一目标（没有施法者就返回 null）</summary>
    public List<Entity> TryFindTargets(Entity caster, MapManager map)
    {
        if (caster == null) return null;

        return new List<Entity> { caster };
    }

    #endregion
}
