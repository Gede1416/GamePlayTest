using UnityEngine;

public sealed class TowerDefenseDemo : MonoBehaviour
{
    private const int HalfWidth = 8;
    private const int HalfDepth = 6;
    private const int MaxHealth = 3;
    private const float Range = 4.5f;
    private static readonly Vector2Int[] TowerCells =
    {
        new Vector2Int(-4, -3), new Vector2Int(4, -3),
        new Vector2Int(-4, 3), new Vector2Int(4, 3)
    };
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
    };
    private static readonly Color Armor = new Color(0.16f, 0.34f, 0.39f);
    private static readonly Color Cyan = new Color(0.20f, 0.93f, 0.87f);
    private static readonly Color Coral = new Color(1f, 0.32f, 0.18f);
    private readonly Tower[] towers = new Tower[TowerCells.Length];
    private readonly Enemy[] enemies = new Enemy[12];
    private MaterialPropertyBlock paint;

    private sealed class Tower
    {
        public Transform head;
        public Transform beam;
        public float nextShot;
        public float beamEnd;
    }

    private sealed class Enemy
    {
        public Transform body;
        public Vector2Int goal;
        public int health;
        public float respawnAt;
    }

    private void Awake()
    {
        paint = new MaterialPropertyBlock();
        Part("战场", PrimitiveType.Cube, transform, new Vector3(0f, -0.25f, 0f),
            new Vector3(17.8f, 0.5f, 13.8f), Armor);
        Part("地面", PrimitiveType.Cube, transform, new Vector3(0f, -0.025f, 0f),
            new Vector3(17.4f, 0.06f, 13.4f), new Color(0.12f, 0.21f, 0.25f));

        for (int i = 0; i < towers.Length; i++)
        {
            Vector2Int cell = TowerCells[i];
            Transform pedestal = Part("攻击塔 " + (i + 1), PrimitiveType.Cube, transform,
                new Vector3(cell.x, 0.5f, cell.y), Vector3.one, Armor);
            Transform head = Part("炮台", PrimitiveType.Cylinder, pedestal,
                Vector3.up * 0.62f, new Vector3(0.92f, 0.22f, 0.92f), Cyan);
            Part("炮管", PrimitiveType.Cube, head, Vector3.forward * 0.48f,
                new Vector3(0.32f, 0.9f, 1f), Armor);
            Transform beam = Part("射线", PrimitiveType.Cube, transform, Vector3.zero, Vector3.one, Cyan);
            beam.gameObject.SetActive(false);
            towers[i] = new Tower { head = head, beam = beam };
        }

        for (int i = 0; i < enemies.Length; i++)
        {
            Transform body = Part("敌人 " + (i + 1), PrimitiveType.Capsule, transform,
                Vector3.zero, new Vector3(0.62f, 0.48f, 0.62f), Coral);
            enemies[i] = new Enemy { body = body };
            Spawn(enemies[i]);
        }
    }

    private void Update()
    {
        foreach (Enemy enemy in enemies)
        {
            if (enemy.health == 0)
            {
                if (Time.time >= enemy.respawnAt)
                    Spawn(enemy);
                continue;
            }

            Vector3 goal = WorldPoint(enemy.goal);
            enemy.body.localPosition = Vector3.MoveTowards(enemy.body.localPosition, goal, Time.deltaTime * 2.4f);
            if (enemy.body.localPosition == goal)
                enemy.goal = PickNextCell(enemy.goal);
        }

        foreach (Tower tower in towers)
            UpdateTower(tower);
    }

    private void Spawn(Enemy enemy)
    {
        int side = Random.Range(0, 4);
        Vector2Int cell = side < 2
            ? new Vector2Int(side == 0 ? -HalfWidth : HalfWidth, Random.Range(-HalfDepth, HalfDepth + 1))
            : new Vector2Int(Random.Range(-HalfWidth, HalfWidth + 1), side == 2 ? -HalfDepth : HalfDepth);
        enemy.body.localPosition = WorldPoint(cell);
        enemy.goal = PickNextCell(cell);
        enemy.health = MaxHealth;
        enemy.body.gameObject.SetActive(true);
    }

    private static Vector3 WorldPoint(Vector2Int cell)
    {
        return new Vector3(cell.x, 0.49f, cell.y);
    }

    private static Vector2Int PickNextCell(Vector2Int cell)
    {
        // 从合法邻格中等概率选一个，不穿过炮塔或越出边界。
        Vector2Int next = cell;
        int choices = 0;
        foreach (Vector2Int direction in Directions)
        {
            Vector2Int candidate = cell + direction;
            if (Mathf.Abs(candidate.x) > HalfWidth || Mathf.Abs(candidate.y) > HalfDepth ||
                System.Array.IndexOf(TowerCells, candidate) >= 0)
                continue;
            if (Random.Range(0, ++choices) == 0)
                next = candidate;
        }
        return next;
    }

    private void UpdateTower(Tower tower)
    {
        if (tower.beam.gameObject.activeSelf && Time.time >= tower.beamEnd)
            tower.beam.gameObject.SetActive(false);
        if (Time.time < tower.nextShot)
            return;

        Enemy target = null;
        float nearest = Range * Range;
        foreach (Enemy enemy in enemies)
        {
            Vector3 delta = enemy.body.position - tower.head.position;
            float distance = delta.x * delta.x + delta.z * delta.z;
            if (enemy.health > 0 && distance <= nearest)
            {
                target = enemy;
                nearest = distance;
            }
        }
        if (target == null)
            return;

        Vector3 end = target.body.position;
        Vector3 aim = end - tower.head.position;
        tower.head.rotation = Quaternion.LookRotation(new Vector3(aim.x, 0f, aim.z));
        Vector3 muzzle = tower.head.position + tower.head.forward * 0.88f;
        tower.beam.SetPositionAndRotation((muzzle + end) * 0.5f, Quaternion.LookRotation(end - muzzle));
        tower.beam.localScale = new Vector3(0.07f, 0.07f, Vector3.Distance(muzzle, end));
        tower.beam.gameObject.SetActive(true);
        tower.beamEnd = Time.time + 0.14f;
        tower.nextShot = Time.time + 0.65f;
        if (--target.health == 0)
        {
            target.body.gameObject.SetActive(false);
            target.respawnAt = Time.time + 2f;
        }
    }

    private Transform Part(string partName, PrimitiveType shape, Transform parent,
        Vector3 position, Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Destroy(part.GetComponent<Collider>());
        paint.SetColor("_Color", color);
        part.GetComponent<Renderer>().SetPropertyBlock(paint);
        return part.transform;
    }
}
