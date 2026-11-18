using UnityEngine;

/// <summary>独立场景入口，每一步执行一次行为树决策。</summary>
public sealed class CombatTreeDemo : MonoBehaviour
{
    [SerializeField, HideInInspector] CombatTree tree = new CombatTree();
    [SerializeField] Transform actor, enemy, healPad, restPad, boostPad;
    const float MoveSpeed = 4f;
    const float AttackRange = 1.4f;
    Vector3 spawn;
    CombatBranch movingTo;
    Camera sceneCamera;
    CombatTrial trial;
    bool automatic = true;
    bool loop = true;
    int battleNumber;
    int lastDamage;
    float showDamageUntil;
    float nextTick;
    string status;

    void Awake()
    {
        spawn = actor.position;
        sceneCamera = Camera.main;
        Restart();
    }

    void Update()
    {
        if (movingTo != CombatBranch.Empty)
        {
            Vector3 target = Destination(movingTo);
            actor.position = Vector3.MoveTowards(actor.position, target, MoveSpeed * Time.deltaTime);
            if (actor.position == target) Resolve(movingTo);
        }
        else if (automatic && Time.time >= nextTick)
        {
            if (!trial.Finished) Step();
            else if (loop) Restart();
            else automatic = false;
        }
    }

    void OnGUI()
    {
        DrawLabel(healPad, "回血");
        DrawLabel(restPad, "回体力");
        DrawLabel(boostPad, "强化");
        DrawEnemyHealth();
        GUILayout.BeginArea(new Rect(10, 10, Mathf.Min(440, Screen.width - 20), 220), GUI.skin.box);
        GUILayout.Label($"行为树战斗测试　　第 {battleNumber} 场");
        GUILayout.Label("根节点   " + string.Join("  →  ", System.Array.ConvertAll(tree.children, CombatTree.Label)));
        GUILayout.Space(6);
        GUILayout.Label($"己方 HP  {trial.Hp}/100      体力  {trial.Stamina}/3      敌方 HP  {trial.EnemyHp}/{CombatTrial.MaxEnemyHp}");
        GUILayout.Label($"攻击力  {(trial.BoostTurns > 0 ? 20 : 10)}      强化剩余  {trial.BoostTurns} 回合      攻击冷却  {trial.Cooldown} 回合");
        bool moving = movingTo != CombatBranch.Empty;
        GUILayout.Label($"第 {(moving ? trial.Turn + 1 : trial.Turn)} 步      根节点选择   {CombatTree.Label(moving ? movingTo : trial.Branch)}");
        GUILayout.Label("战斗技能计划   " + (moving || string.IsNullOrEmpty(trial.Plan) ? "—" : trial.Plan));
        GUILayout.Label(status);
        GUILayout.BeginHorizontal();
        GUI.enabled = !trial.Finished && !moving;
        if (GUILayout.Button("下一步")) { automatic = false; Step(); }
        GUI.enabled = true;
        if (GUILayout.Button(automatic ? "本步后暂停" : "自动播放"))
        {
            automatic = !automatic;
            if (automatic && trial.Finished) Restart();
        }
        loop = GUILayout.Toggle(loop, "自动循环");
        if (GUILayout.Button("重置"))
        {
            automatic = false;
            battleNumber = 0;
            Restart();
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void DrawLabel(Transform point, string label)
    {
        var screen = sceneCamera.WorldToScreenPoint(point.position + Vector3.up * 0.25f);
        GUI.Box(new Rect(screen.x - 26, Screen.height - screen.y - 18, 52, 20), label);
    }

    void DrawEnemyHealth()
    {
        var screen = sceneCamera.WorldToScreenPoint(enemy.position + Vector3.up);
        var label = new Rect(screen.x - 65, Screen.height - screen.y - 26, 130, 22);
        GUI.Box(label, $"敌人  {trial.EnemyHp}/{CombatTrial.MaxEnemyHp}");
        var bar = new Rect(label.x, label.yMax, label.width, 5);
        var original = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.28f, 0.22f);
        bar.width *= (float)trial.EnemyHp / CombatTrial.MaxEnemyHp;
        if (bar.width > 0) GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = original;
        if (Time.time < showDamageUntil)
            GUI.Box(new Rect(screen.x - 24, label.y - 23, 48, 22), $"-{lastDamage}");
    }

    void Restart()
    {
        battleNumber++;
        nextTick = Time.time + 0.5f;
        showDamageUntil = 0;
        movingTo = CombatBranch.Empty;
        actor.position = spawn;
        enemy.gameObject.SetActive(true);
        trial = new CombatTrial(20, 0, 0);
        status = "从低血量、零体力开始";
    }

    void Step()
    {
        if (trial.Finished || movingTo != CombatBranch.Empty) return;
        movingTo = tree.Select(trial);
        if (movingTo == CombatBranch.Empty) Resolve(movingTo);
        else status = "移动中";
    }

    Vector3 Destination(CombatBranch branch)
    {
        Vector3 target;
        switch (branch)
        {
            case CombatBranch.Heal: target = healPad.position; break;
            case CombatBranch.Rest: target = restPad.position; break;
            case CombatBranch.Boost: target = boostPad.position; break;
            default:
                target = enemy.position;
                target.y = actor.position.y;
                return Vector3.MoveTowards(target, actor.position, AttackRange);
        }
        target.y = actor.position.y;
        return target;
    }

    void Resolve(CombatBranch branch)
    {
        movingTo = CombatBranch.Empty;
        int hpBefore = trial.EnemyHp;
        trial.Resolve(branch);
        if (trial.EnemyHp < hpBefore)
        {
            lastDamage = hpBefore - trial.EnemyHp;
            showDamageUntil = Time.time + 0.9f;
        }
        if (trial.EnemyHp == 0) enemy.gameObject.SetActive(false);
        nextTick = Time.time + (trial.Finished ? 2f : 1f);
        if (trial.Finished) status = "战斗结束";
        else if (trial.Branch == CombatBranch.Empty) status = "没有可执行的已连接分支";
        else status = "";
        if ((trial.Finished && !loop) || trial.Branch == CombatBranch.Empty) automatic = false;
    }
}
