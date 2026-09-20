using System.Collections.Generic;

/// <summary>给自己上 buff 这类技能用：目标就是自己，永远找得到（活着才放得出去由阶段一管）。不需要地图，所以不实现 ISkillPart</summary>
public class SelfTargetFinder : ITargetFinder
{
    #region 公开方法

    /// <summary>把施法者自己当成唯一目标</summary>
    public bool TryFindTargets(Entity caster, List<Entity> targets)
    {
        targets.Clear();
        if (caster == null) return false;

        targets.Add(caster);
        return true;
    }

    #endregion
}
