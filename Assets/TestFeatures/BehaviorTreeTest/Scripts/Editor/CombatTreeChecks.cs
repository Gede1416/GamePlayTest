using System;

public static class CombatTreeChecks
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/行为树/验证独立测试场")]
#endif
    public static void Run()
    {
        var tree = new CombatTree();
        var fight = new CombatTrial(20, 0, 0);
        tree.Tick(fight);
        Check(fight.Branch == CombatBranch.Heal && fight.Hp == 60, "低血量优先回血");
        tree.Tick(fight);
        Check(fight.Branch == CombatBranch.Rest && fight.Stamina == 3, "休息恢复体力");
        tree.Tick(fight);
        Check(fight.Branch == CombatBranch.Boost && fight.BoostTurns > 0, "满体力强化");
        tree.Tick(fight);
        Check(fight.Branch == CombatBranch.Fight && fight.EnemyHp == 40 && fight.Plan == "攻击", "强化攻击");
        tree.Tick(fight);
        Check(fight.Plan == "冷却缩减 → 攻击" && fight.EnemyHp == 20 && fight.BoostTurns == 0, "冷却与临时加成到期");
        var ready = new CombatTrial(100, 2, 0);
        tree.Tick(ready);
        Check(ready.Plan == "攻击" && ready.EnemyHp == 50, "无需缩减时直接攻击");
        var hurt = new CombatTrial(30, 2, 0);
        tree.Tick(hurt);
        Check(hurt.Plan == "恢复 → 攻击" && hurt.Hp > 30, "战斗规划恢复后攻击");
        for (int i = 0; i < 50 && !fight.Finished; i++) tree.Tick(fight);
        Check(fight.EnemyHp == 0, "能够结束战斗");
        tree.children = new[] { CombatBranch.Rest, CombatBranch.Heal, CombatBranch.Boost, CombatBranch.Fight };
        var reordered = new CombatTrial(20, 0, 0);
        tree.Tick(reordered);
        Check(reordered.Branch == CombatBranch.Rest && reordered.Hp == 20, "交换根端口应改变选择结果");
        tree.skillPlanner = false;
        var disconnected = new CombatTrial(10000, 2, 0);
        tree.Tick(disconnected);
        Check(disconnected.Branch == CombatBranch.Empty && disconnected.EnemyHp == 60, "断开的规划节点不能执行");
#if UNITY_EDITOR
        UnityEngine.Debug.Log("行为树测试通过：四个分支、技能规划、临时加成、连线优先级和战斗结束。");
#else
        Console.WriteLine("PASS: combat behavior tree and skill planning");
#endif
    }

    static void Check(bool passed, string message)
    {
        if (!passed) throw new InvalidOperationException(message);
    }
}
