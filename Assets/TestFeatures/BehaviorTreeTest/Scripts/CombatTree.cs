using System;
using System.Collections.Generic;

public enum CombatBranch { Empty, Heal, Rest, Boost, Fight }

/// <summary>根选择节点按端口顺序尝试子节点，首个成功的行为结束本次决策。</summary>
[Serializable]
public sealed class CombatTree
{
    public CombatBranch[] children = { CombatBranch.Heal, CombatBranch.Rest, CombatBranch.Boost, CombatBranch.Fight };
    public bool skillPlanner = true;

    public void Tick(CombatTrial trial)
    {
        if (!trial.Finished) trial.Resolve(Select(trial));
    }

    internal CombatBranch Select(CombatTrial trial)
    {
        foreach (var child in children)
        {
            switch (child)
            {
                case CombatBranch.Heal when trial.Hp <= 25:
                case CombatBranch.Rest when trial.Stamina == 0:
                case CombatBranch.Boost when trial.Stamina == 3 && trial.BoostTurns == 0 && trial.EnemyHp > 10:
                case CombatBranch.Fight when skillPlanner && trial.Stamina > 0:
                    return child;
            }
        }
        return CombatBranch.Empty;
    }

    public static string Label(CombatBranch branch)
    {
        switch (branch)
        {
            case CombatBranch.Heal: return "回血";
            case CombatBranch.Rest: return "回体力";
            case CombatBranch.Boost: return "强化自己";
            case CombatBranch.Fight: return "战斗";
            default: return "无";
        }
    }
}

public sealed class CombatTrial
{
    public const int MaxEnemyHp = 100000;
    const string Attack = "攻击", Strengthen = "临时强化", ReduceCooldown = "冷却缩减", Restore = "恢复";

    public int Hp { get; private set; }
    public int Stamina { get; private set; }
    public int EnemyHp { get; private set; } = MaxEnemyHp;
    public int BoostTurns { get; private set; }
    public int Cooldown { get; private set; }
    public int Turn { get; private set; }
    public CombatBranch Branch { get; private set; }
    public string Plan { get; private set; }
    public bool Finished => EnemyHp == 0 || Hp == 0;

    public CombatTrial(int hp, int stamina, int cooldown)
    {
        Hp = hp;
        Stamina = stamina;
        Cooldown = cooldown;
    }

    internal void Resolve(CombatBranch branch)
    {
        Branch = CombatBranch.Empty;
        Plan = "";
        Turn++;
        switch (branch)
        {
            case CombatBranch.Heal:
                Cast(Restore);
                break;
            case CombatBranch.Rest:
                Stamina = 3;
                break;
            case CombatBranch.Boost:
                Cast(Strengthen);
                break;
            case CombatBranch.Fight:
                if (!ExecuteCombatPlan()) return;
                break;
            default:
                return;
        }
        Branch = branch;
        if (BoostTurns > 0) BoostTurns--;
        if (branch != CombatBranch.Fight && Cooldown > 0) Cooldown--;
    }

    bool ExecuteCombatPlan()
    {
        // 默认目标是命中；能用强化一击收尾时，把足够的伤害也列入前置条件。
        using (var agent = new ReGoapTestAgent("完成攻击", "hit"))
        {
            agent.World.Set("hit", false);
            agent.World.Set("ready", Cooldown == 0);
            agent.World.Set("safe", Hp > 35);
            bool needsBoost = BoostTurns == 0 && EnemyHp > 10 && EnemyHp <= 20;
            agent.World.Set("damageEnough", !needsBoost);
            agent.AddAction(Attack, 1, new[] { "ready", "safe", "damageEnough" }, "hit");
            if (needsBoost)
                agent.AddAction(Strengthen, 1, Array.Empty<string>(), "damageEnough");
            if (Cooldown > 0)
                agent.AddAction(ReduceCooldown, 1, Array.Empty<string>(), "ready");
            if (Hp <= 35)
                agent.AddAction(Restore, 1, Array.Empty<string>(), "safe");
            var goal = agent.Plan();
            if (goal == null) return false;
            var names = new List<string>();
            foreach (var step in goal.GetPlan())
            {
                var name = step.Action.GetName();
                Cast(name);
                names.Add(name);
            }
            Plan = string.Join(" → ", names);
            return true;
        }
    }

    void Cast(string skill)
    {
        switch (skill)
        {
            case Restore: Hp = Math.Min(100, Hp + 40); break;
            case Strengthen: BoostTurns = 3; break;
            case ReduceCooldown: Cooldown = 0; break;
            case Attack:
                EnemyHp = Math.Max(0, EnemyHp - (BoostTurns > 0 ? 20 : 10));
                Stamina--;
                Cooldown = 1;
                if (EnemyHp > 0) Hp -= 12;
                break;
        }
    }
}
