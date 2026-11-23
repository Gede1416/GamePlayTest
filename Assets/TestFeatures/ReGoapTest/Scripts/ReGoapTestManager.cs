using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ReGoap.Core;
using UnityEngine;

/// <summary>独立 GOAP 测试场入口：造场、显式建立模型、执行规划结果和展示状态。</summary>
public class ReGoapTestManager : MonoBehaviour
{
    [SerializeField, Tooltip("测试物体共用的基础材质，运行时复制并染色")]
    Material baseMaterial;

    readonly List<Material> materials = new List<Material>();
    ReGoapTestAgent agent;
    Transform field;
    Transform actor;
    Transform weapon;
    Transform enemy;
    string status;
    string planText;
    float planCost;

    void Awake() => RunCase(true);

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) RunCase(true);
        else if (Input.GetKeyDown(KeyCode.B)) RunCase(false);
    }

    void OnDisable()
    {
        StopAllCoroutines();
        status = "Stopped - restart to run again";
    }

    void OnDestroy() => Clear();

    void OnGUI()
    {
        if (agent == null) return;
        GUILayout.BeginArea(new Rect(14, 14, 440, 310), GUI.skin.box);
        GUILayout.Label("ReGoap - Defeat enemy");
        GUILayout.Label("Cyan: agent   Gold: weapon   Red: enemy");
        GUILayout.Label("Goal: enemyDead = true");
        GUILayout.Label("Status: " + status);
        GUILayout.Label("Plan: " + planText);
        GUILayout.Label("Planning cost: " + planCost + " (not travel time)");
        GUILayout.Label("Sword attack cost = 1; unarmed attack cost = 10");
        GUILayout.Space(6);
        foreach (var fact in agent.World.GetValues())
            GUILayout.Label(fact.Key + " = " + fact.Value);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("R: With weapon")) RunCase(true);
        if (GUILayout.Button("B: No weapon")) RunCase(false);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void RunCase(bool weaponAvailable)
    {
        Clear();
        if (baseMaterial == null)
        {
            Debug.LogError("ReGoap 测试场：基础材质未配置", this);
            return;
        }
        field = new GameObject("ReGoap test objects").transform;
        field.SetParent(transform, false);
        CreateObject("Ground", PrimitiveType.Cube, new Vector3(0, -0.15f, 0),
            new Vector3(12, 0.3f, 10), new Color(0.22f, 0.25f, 0.3f));
        actor = CreateObject("Agent", PrimitiveType.Capsule, new Vector3(-4, 0.75f, -2),
            new Vector3(0.75f, 0.75f, 0.75f), Color.cyan);
        weapon = CreateObject("Weapon", PrimitiveType.Cube, new Vector3(-4, 0.35f, 3),
            new Vector3(0.3f, 0.3f, 1.2f), new Color(1, 0.75f, 0.1f));
        enemy = CreateObject("Enemy", PrimitiveType.Capsule, new Vector3(4, 0.75f, 1),
            new Vector3(0.75f, 0.75f, 0.75f), new Color(0.9f, 0.2f, 0.2f));
        weapon.gameObject.SetActive(weaponAvailable);
        agent = new ReGoapTestAgent(weaponAvailable);
        var goal = agent.Plan();
        if (goal == null)
        {
            status = "No plan";
            planText = "(none)";
            return;
        }
        var plan = goal.GetPlan();
        planText = string.Join(" -> ", plan.Select(step => step.Action.GetName()));
        planCost = 0;
        foreach (var step in plan)
            planCost += step.Action.GetCost(Context(step, goal));
        Debug.Log("ReGoap 测试场计划：" + planText + "；成本 " + planCost, this);
        StartCoroutine(Execute(plan, goal));
    }

    void Clear()
    {
        StopAllCoroutines();
        agent?.Dispose();
        agent = null;
        if (field != null)
        {
            field.gameObject.SetActive(false);
            Destroy(field.gameObject);
            field = null;
        }
        foreach (var material in materials) Destroy(material);
        materials.Clear();
    }

    Transform CreateObject(string objectName, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        var instance = GameObject.CreatePrimitive(type);
        instance.name = objectName;
        instance.transform.SetParent(field, false);
        instance.transform.localPosition = position;
        instance.transform.localScale = scale;
        var material = new Material(baseMaterial) { color = color };
        materials.Add(material);
        instance.GetComponent<Renderer>().sharedMaterial = material;
        return instance.transform;
    }

    GoapActionStackData<string, object> Context(ReGoapActionState<string, object> step, IReGoapGoal<string, object> goal)
    {
        return new GoapActionStackData<string, object>
        {
            agent = agent,
            currentState = agent.World,
            goalState = goal.GetGoalState(),
            settings = step.Settings
        };
    }

    IEnumerator Execute(Queue<ReGoapActionState<string, object>> plan, IReGoapGoal<string, object> goal)
    {
        foreach (var step in plan)
        {
            if (step.Action.GetPreconditions(Context(step, goal)).MissingDifference(agent.World, 1) != 0)
            {
                status = "Precondition failed - restart to replan";
                yield break;
            }
            status = step.Action.GetName();
            switch (status)
            {
                case "Take weapon":
                    yield return MoveTo(weapon.position);
                    weapon.SetParent(actor, true);
                    weapon.localPosition = new Vector3(0.8f, 0, 0);
                    agent.World.Set("weaponAvailable", false);
                    agent.World.Set("hasWeapon", true);
                    break;
                case "Approach enemy":
                    yield return MoveTo(enemy.position + Vector3.left * 1.3f);
                    break;
                case "Sword attack":
                case "Unarmed attack":
                    yield return new WaitForSeconds(0.6f);
                    enemy.gameObject.SetActive(false);
                    agent.World.Set("enemyDead", true);
                    break;
            }
            // 从实际位置回写距离；规划器的效果不会自动替游戏更新世界。
            var distance = actor.position - enemy.position;
            distance.y = 0;
            agent.World.Set("nearEnemy", distance.sqrMagnitude <= 1.5f * 1.5f);
        }
        status = goal.GetGoalState().MissingDifference(agent.World, 1) == 0 ? "Goal reached" : "Goal failed";
        Debug.Log("ReGoap 测试场：" + status, this);
    }

    IEnumerator MoveTo(Vector3 destination)
    {
        destination.y = actor.position.y;
        while ((actor.position - destination).sqrMagnitude > 0.001f)
        {
            actor.position = Vector3.MoveTowards(actor.position, destination, 3f * Time.deltaTime);
            yield return null;
        }
        actor.position = destination;
    }
}
