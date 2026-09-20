using System.Collections.Generic;

/// <summary>远程目标获取：范围 3 格、1 个目标。地图由组合技能在装配时发下来</summary>
public class RangedTargetFinder : ITargetFinder, ISkillPart
{
    #region 属性

    /// <summary>技能范围（格）</summary>
    public const int Range = 3;

    /// <summary>最多选几个目标</summary>
    public const int TargetCount = 1;

    MapManager map;

    #endregion

    #region 公开方法

    /// <summary>装配：地图由组合技能发下来</summary>
    public void Init(ISkill owner, MapManager map)
    {
        this.map = map;
    }

    /// <summary>按范围挑目标：Range 格内的 1 个最近的非己方</summary>
    public bool TryFindTargets(Entity caster, List<Entity> targets)
    {
        return TargetPicker.Pick(map, caster, Range, TargetCount, targets);
    }

    #endregion
}
