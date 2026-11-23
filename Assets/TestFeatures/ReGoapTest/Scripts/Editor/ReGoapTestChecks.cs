using System;
using System.Linq;
using ReGoap.Core;

/// <summary>不依赖场景和测试包的规划检查；编辑器菜单和独立 Mono 均可运行。</summary>
public static class ReGoapTestChecks
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/ReGoap/验证测试场景规划")]
#endif
    public static void Run()
    {
        CheckPlan(true, "Take weapon", "Approach enemy", "Sword attack");
        CheckPlan(false, "Approach enemy", "Unarmed attack");
        using (var agent = new ReGoapTestAgent(false))
        {
            agent.GetActionsSet().Clear();
            Require(agent.Plan() == null, "没有动作时应返回无解");
        }
#if UNITY_EDITOR
        UnityEngine.Debug.Log("ReGoap 测试通过：持械 / 徒手计划、前置条件、成本选择、目标达成及无解检查。");
#else
        Console.WriteLine("PASS: armed/unarmed plans, preconditions, cost selection, goal reached, impossible goal.");
#endif
    }

    static void CheckPlan(bool weaponAvailable, params string[] expected)
    {
        using (var agent = new ReGoapTestAgent(weaponAvailable))
        {
            var result = agent.Plan();
            Require(result != null, "应能找到计划");
            var plan = result.GetPlan();
            Require(plan.Select(step => step.Action.GetName()).SequenceEqual(expected), "计划顺序或成本选择错误");
            Require(!agent.World.Get("enemyDead").Equals(true), "规划不能直接执行动作效果");
            foreach (var step in plan)
            {
                var data = new GoapActionStackData<string, object>
                {
                    agent = agent, currentState = agent.World,
                    goalState = result.GetGoalState(), settings = step.Settings
                };
                Require(step.Action.GetPreconditions(data).MissingDifference(agent.World, 1) == 0, "前置条件不成立");
                foreach (var effect in step.Action.GetEffects(data).GetValues())
                    agent.World.Set(effect.Key, effect.Value);
            }
            Require(result.GetGoalState().MissingDifference(agent.World, 1) == 0, "执行完计划应达到目标");
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("ReGoap 测试失败：" + message);
    }
}
