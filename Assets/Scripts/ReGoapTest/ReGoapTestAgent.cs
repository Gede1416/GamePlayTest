using System;
using System.Collections.Generic;
using ReGoap.Core;
using ReGoap.Planner;

/// <summary>测试场的纯 C# 规划模型；实际移动与攻击由场景执行，不在规划时改变世界。</summary>
public sealed class ReGoapTestAgent : IReGoapAgent<string, object>, IReGoapMemory<string, object>, IDisposable
{
    readonly ReGoapPlanner<string, object> planner = new ReGoapPlanner<string, object>();
    readonly List<IReGoapAction<string, object>> actions = new List<IReGoapAction<string, object>>();
    readonly List<IReGoapGoal<string, object>> goals = new List<IReGoapGoal<string, object>>();
    readonly List<TestAction> ownedActions = new List<TestAction>();
    readonly Dictionary<string, object> planValues = new Dictionary<string, object>();
    readonly TestGoal goal = new TestGoal();
    IReGoapGoal<string, object> currentGoal;

    public ReGoapState<string, object> World { get; } = ReGoapState<string, object>.Instantiate();

    public ReGoapTestAgent(bool weaponAvailable)
    {
        World.Set("weaponAvailable", weaponAvailable);
        World.Set("hasWeapon", false);
        World.Set("nearEnemy", false);
        World.Set("enemyDead", false);
        // 故意把昂贵动作放在前面，验证规划器按成本挑计划，而非按列表顺序执行。
        AddAction("Unarmed attack", 10, new[] { "nearEnemy" }, "enemyDead");
        AddAction("Sword attack", 1, new[] { "nearEnemy", "hasWeapon" }, "enemyDead");
        AddAction("Approach enemy", 1, new string[0], "nearEnemy");
        AddAction("Take weapon", 1, new[] { "weaponAvailable" }, "hasWeapon");
        goals.Add(goal);
    }

    public IReGoapGoal<string, object> Plan()
    {
        currentGoal = planner.Plan(this);
        return currentGoal;
    }

    public void Dispose()
    {
        World.Recycle();
        goal.State.Recycle();
        foreach (var action in ownedActions) action.Dispose();
    }

    public IReGoapMemory<string, object> GetMemory() => this;
    public ReGoapState<string, object> GetWorldState() => World;
    public IReGoapGoal<string, object> GetCurrentGoal() => currentGoal;
    public void WarnPossibleGoal(IReGoapGoal<string, object> candidate) { }
    public bool IsActive() => true;
    public List<ReGoapActionState<string, object>> GetStartingPlan() => null;
    public object GetPlanValue(string key) => planValues[key];
    public void SetPlanValue(string key, object value) => planValues[key] = value;
    public bool HasPlanValue(string key) => planValues.ContainsKey(key);
    public List<IReGoapGoal<string, object>> GetGoalsSet() => goals;
    public List<IReGoapAction<string, object>> GetActionsSet() => actions;
    public ReGoapState<string, object> InstantiateNewState() => ReGoapState<string, object>.Instantiate();

    void AddAction(string name, float cost, string[] required, string effect)
    {
        var action = new TestAction(name, cost, required, effect);
        actions.Add(action);
        ownedActions.Add(action);
    }

    sealed class TestGoal : IReGoapGoal<string, object>
    {
        Queue<ReGoapActionState<string, object>> plan;
        public readonly ReGoapState<string, object> State = ReGoapState<string, object>.Instantiate();

        public TestGoal() { State.Set("enemyDead", true); }
        public void Run(Action<IReGoapGoal<string, object>> callback) { }
        public Queue<ReGoapActionState<string, object>> GetPlan() => plan;
        public string GetName() => "Defeat enemy";
        public void Precalculations(IGoapPlanner<string, object> goapPlanner) { }
        public bool IsGoalPossible() => true;
        public ReGoapState<string, object> GetGoalState() => State;
        public float GetPriority() => 1;
        public void SetPlan(Queue<ReGoapActionState<string, object>> path) => plan = path;
        public float GetErrorDelay() => 0;
    }

    sealed class TestAction : IReGoapAction<string, object>, IDisposable
    {
        readonly string name;
        readonly float cost;
        readonly ReGoapState<string, object> preconditions = ReGoapState<string, object>.Instantiate();
        readonly ReGoapState<string, object> effects = ReGoapState<string, object>.Instantiate();
        readonly ReGoapState<string, object> settings = ReGoapState<string, object>.Instantiate();
        readonly List<ReGoapState<string, object>> candidates;

        public TestAction(string name, float cost, string[] required, string effect)
        {
            this.name = name;
            this.cost = cost;
            foreach (var key in required) preconditions.Set(key, true);
            effects.Set(effect, true);
            if (effect == "hasWeapon")
            {
                // 拿武器会离开敌人，必须重新接近，不能沿用旧的距离事实。
                effects.Set("nearEnemy", false);
                effects.Set("weaponAvailable", false);
            }
            candidates = new List<ReGoapState<string, object>> { settings };
        }

        public void Dispose() { preconditions.Recycle(); effects.Recycle(); settings.Recycle(); }
        public List<ReGoapState<string, object>> GetSettings(GoapActionStackData<string, object> data) => candidates;
        // 本例由协程驱动执行；接口回调只表示这个动作的实际操作已由调用方完成。
        public void Run(IReGoapAction<string, object> previous, IReGoapAction<string, object> next,
            ReGoapState<string, object> settings, ReGoapState<string, object> goalState,
            Action<IReGoapAction<string, object>> done, Action<IReGoapAction<string, object>> fail) => done(this);
        public void PlanEnter(IReGoapAction<string, object> previous, IReGoapAction<string, object> next,
            ReGoapState<string, object> settings, ReGoapState<string, object> goalState) { }
        public void PlanExit(IReGoapAction<string, object> previous, IReGoapAction<string, object> next,
            ReGoapState<string, object> settings, ReGoapState<string, object> goalState) { }
        public void Exit(IReGoapAction<string, object> next) { }
        public string GetName() => name;
        public bool IsActive() => false;
        public bool IsInterruptable() => true;
        public void AskForInterruption() { }
        public ReGoapState<string, object> GetPreconditions(GoapActionStackData<string, object> data) => preconditions;
        public ReGoapState<string, object> GetEffects(GoapActionStackData<string, object> data) => effects;
        public bool CheckProceduralCondition(GoapActionStackData<string, object> data) => true;
        public float GetCost(GoapActionStackData<string, object> data) => cost;
        public void Precalculations(GoapActionStackData<string, object> data) { }
        public string ToString(GoapActionStackData<string, object> data) => name;
    }
}
