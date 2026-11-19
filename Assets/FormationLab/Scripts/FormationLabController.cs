using UnityEngine;

namespace FormationLab
{
    public sealed class FormationLabController : MonoBehaviour
    {
        [SerializeField] Camera groundCamera = null;
        [SerializeField, Min(0), Tooltip("阵型中心每秒移动的世界距离")] float marchSpeed = 2.5f;

        const int Count = 24;
        const float Radius = 0.24f;
        readonly Unit[] units = new Unit[Count];
        readonly Vector2[] nextPositions = new Vector2[Count];
        GroundBox formation = new GroundBox
        {
            Center = new Vector2(-5, 0), Forward = Vector2.up, HalfSize = new Vector2(3, 2)
        };
        readonly GroundBox wall = new GroundBox
        {
            Center = new Vector2(2, 2), Forward = new Vector2(0.6f, 0.8f), HalfSize = new Vector2(0.35f, 2.5f)
        };
        Transform frame;
        Transform enemy;
        Vector2 destination;

        struct Unit
        {
            internal Transform View;
            internal bool Ranged;
            internal Vector2 Position;
            internal Vector2 Velocity;
        }

        void Awake()
        {
            destination = formation.Center;
            frame = new GameObject("Formation").transform;
            frame.SetParent(transform, false);
            Color edge = new Color(0.3f, 0.8f, 0.7f);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                MakeView("Edge", PrimitiveType.Cube, frame, new Vector3(sign * formation.HalfSize.x, 0, 0),
                    new Vector3(0.05f, 0.05f, formation.HalfSize.y * 2), edge);
                MakeView("Edge", PrimitiveType.Cube, frame, new Vector3(0, 0, sign * formation.HalfSize.y),
                    new Vector3(formation.HalfSize.x * 2, 0.05f, 0.05f), edge);
            }
            SyncFrame();
            Transform barrier = MakeView("Wall", PrimitiveType.Cube, transform, Ground(wall.Center, 0.3f),
                new Vector3(wall.HalfSize.x * 2, 0.6f, wall.HalfSize.y * 2), Color.gray);
            barrier.rotation = Quaternion.LookRotation(Ground(wall.Forward));
            enemy = MakeView("Enemy", PrimitiveType.Sphere, transform, new Vector3(-2, 0.35f, -1),
                Vector3.one * 0.7f, Color.red);
            for (int i = 0; i < Count; i++)
            {
                bool ranged = i % 2 == 1;
                Vector2 position = formation.Center + new Vector2((i % 6 - 2.5f) * 0.85f, (i / 6 - 1.5f) * 0.8f);
                if (i == 0) position.x -= 2;
                Transform view = MakeView(ranged ? "Ranged" : "Melee", PrimitiveType.Sphere, transform,
                    Ground(position, Radius), Vector3.one * (Radius * 2),
                    ranged ? new Color(0.25f, 0.65f, 1) : new Color(1, 0.72f, 0.12f));
                units[i] = new Unit { View = view, Position = position, Ranged = ranged };
            }
        }

        void Update()
        {
            bool moveFormation = Input.GetMouseButtonDown(0);
            if (!moveFormation && !Input.GetMouseButtonDown(1)) return;
            if (!groundCamera.pixelRect.Contains(Input.mousePosition)) return;
            Ray ray = groundCamera.ScreenPointToRay(Input.mousePosition);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
            Vector3 hit = ray.GetPoint(distance);
            if (moveFormation) destination = new Vector2(hit.x, hit.z);
            else enemy.position = new Vector3(hit.x, 0.35f, hit.z);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector2 previous = formation.Center;
            formation.Center = Vector2.MoveTowards(previous, destination, marchSpeed * dt);
            Vector2 march = (formation.Center - previous) / dt;
            if (march.sqrMagnitude > 0.0001f) formation.Forward = march.normalized;
            SyncFrame();

            Vector2 target = new Vector2(enemy.position.x, enemy.position.z);
            for (int i = 0; i < Count; i++)
            {
                Unit unit = units[i];
                Vector2 home = formation.ReturnOffset(unit.Position, Radius);
                Vector2 acceleration = (march - unit.Velocity) * 3 + home * 7
                    + NeighborAcceleration(i) + EnemyAcceleration(unit, target);
                float limit = marchSpeed + 0.7f + Mathf.Min(home.magnitude, 2f);
                Vector2 velocity = Vector2.ClampMagnitude(unit.Velocity + Vector2.ClampMagnitude(acceleration, 12) * dt, limit);
                nextPositions[i] = wall.Slide(unit.Position, unit.Position + velocity * dt, Radius);
            }
            // 所有单位计算完成后统一提交，本步遍历顺序不影响邻居读数。
            for (int i = 0; i < Count; i++)
            {
                units[i].Velocity = (nextPositions[i] - units[i].Position) / dt;
                units[i].Position = nextPositions[i];
                units[i].View.position = Ground(nextPositions[i], Radius);
            }
        }

        Vector2 NeighborAcceleration(int index)
        {
            Unit self = units[index];
            Vector2 separation = Vector2.zero;
            Vector2 alignment = Vector2.zero;
            Vector2 cohesion = Vector2.zero;
            int nearby = 0;
            for (int i = 0; i < Count; i++)
            {
                if (i == index) continue;
                Vector2 offset = units[i].Position - self.Position;
                float distance = offset.magnitude;
                if (distance > 1.6f) continue;
                if (distance < 0.85f)
                {
                    Vector2 away = distance > 0.0001f ? -offset / distance : (i < index ? Vector2.right : Vector2.left);
                    separation += away * (0.85f - distance) * 12;
                }
                alignment += units[i].Velocity - self.Velocity;
                cohesion += offset;
                nearby++;
            }
            return nearby == 0 ? Vector2.zero : separation + (alignment * 0.6f + cohesion * 0.3f) / nearby;
        }

        static Vector2 EnemyAcceleration(Unit unit, Vector2 target)
        {
            Vector2 offset = target - unit.Position;
            float distance = offset.magnitude;
            if (distance > 4.5f) return Vector2.zero;
            float min = unit.Ranged ? 2.4f : 0.7f;
            float max = unit.Ranged ? 3.2f : 1f;
            float error = distance - Mathf.Clamp(distance, min, max);
            Vector2 toward = distance > 0.0001f ? offset / distance : Vector2.right;
            return toward * Mathf.Clamp(error * 4, -5, 5);
        }

        void SyncFrame()
        {
            frame.SetPositionAndRotation(Ground(formation.Center, 0.04f), Quaternion.LookRotation(Ground(formation.Forward)));
        }

        static Vector3 Ground(Vector2 position, float height = 0)
        {
            return new Vector3(position.x, height, position.y);
        }

        static Transform MakeView(string label, PrimitiveType shape, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            GameObject item = GameObject.CreatePrimitive(shape);
            item.name = label;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            item.GetComponent<Renderer>().SetPropertyBlock(properties);
            return item.transform;
        }
    }
}
