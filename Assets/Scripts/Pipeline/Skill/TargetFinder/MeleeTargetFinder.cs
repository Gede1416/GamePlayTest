using System.Collections.Generic;

/// <summary>近战目标获取：范围 1 格、1 个目标。地图由调用方（组合技能）每次传进来</summary>
public class MeleeTargetFinder : ITargetFinder
{
    #region 属性

    /// <summary>技能范围（格）</summary>
    public const int Range = 1;

    /// <summary>最多选几个目标</summary>
    public const int TargetCount = 1;

    #endregion

    #region 公开方法

    /// <summary>按范围挑目标：Range 格内的 1 个最近的非己方</summary>
    public List<Entity> TryFindTargets(Entity caster, MapManager map)
    {
        return TargetPicker.Pick(map, caster, Range, TargetCount);
    }

    #endregion
}
