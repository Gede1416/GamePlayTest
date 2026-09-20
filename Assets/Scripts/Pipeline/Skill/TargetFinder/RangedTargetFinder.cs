using System.Collections.Generic;

/// <summary>远程目标获取：范围 3 格、1 个目标。地图由调用方（组合技能）每次传进来</summary>
public class RangedTargetFinder : ITargetFinder
{
    /// <summary>技能范围（格）</summary>
    public const int Range = 3;

    /// <summary>最多选几个目标</summary>
    public const int TargetCount = 1;

    /// <summary>按范围挑目标：Range 格内的 1 个最近的非己方</summary>
    public List<Entity> TryFindTargets(Entity caster, MapManager map)
    {
        return TargetPicker.Pick(map, caster, Range, TargetCount);
    }
}
