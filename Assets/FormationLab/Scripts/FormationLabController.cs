using UnityEngine;

namespace FormationLab
{
    public sealed class FormationLabController : MonoBehaviour
    {
        [SerializeField] Camera groundCamera = null;
        [SerializeField, Min(0), Tooltip("阵型中心每秒移动的世界距离")] float marchSpeed = 2.5f;

        const int TeamSize = 24;
        const int TeamCount = 2;
        readonly FlockUnit[] units = new FlockUnit[TeamSize * TeamCount];
        readonly Transform[] unitViews = new Transform[TeamSize * TeamCount];
        readonly MarchingFormation[] formations = new MarchingFormation[TeamCount];
        readonly Transform[] frames = new Transform[TeamCount];
        FlockSimulation simulation;

        void Awake()
        {
            var wall = new GroundBox
            {
                Center = new Vector2(2, 2), Forward = new Vector2(0.6f, 0.8f), HalfSize = new Vector2(0.35f, 2.5f)
            };
            Transform barrier = MakeView("Wall", PrimitiveType.Cube, transform, Ground(wall.Center, 0.3f),
                new Vector3(wall.HalfSize.x * 2, 0.6f, wall.HalfSize.y * 2), Color.gray);
            barrier.rotation = Quaternion.LookRotation(Ground(wall.Forward));
            CreateTeam(0, new Vector2(-6, 0), Vector2.right, new Color(0.2f, 0.66f, 1));
            CreateTeam(1, new Vector2(6, 0), Vector2.left, new Color(1, 0.57f, 0.12f));
            simulation = new FlockSimulation(formations, new FlockMap(wall), units);
        }

        void Update()
        {
            bool left = Input.GetMouseButtonDown(0);
            bool right = Input.GetMouseButtonDown(1);
            if ((!left && !right) || !groundCamera.pixelRect.Contains(Input.mousePosition)) return;
            Ray ray = groundCamera.ScreenPointToRay(Input.mousePosition);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
            Vector3 hit = ray.GetPoint(distance);
            Vector2 destination = new Vector2(hit.x, hit.z);
            if (left) formations[0].Destination = destination;
            if (right) formations[1].Destination = destination;
        }

        void FixedUpdate()
        {
            simulation.Step(marchSpeed, Time.fixedDeltaTime);
            for (int i = 0; i < formations.Length; i++) SyncFrame(i);
            for (int i = 0; i < units.Length; i++)
                unitViews[i].position = Ground(units[i].Position, FlockUnit.Radius);
        }

        void CreateTeam(int team, Vector2 center, Vector2 forward, Color color)
        {
            // 局部 y 对应前方，长边沿行进方向。
            var bounds = new GroundBox { Center = center, Forward = forward, HalfSize = new Vector2(2, 3) };
            var formation = new MarchingFormation(bounds);
            formations[team] = formation;
            Transform teamRoot = new GameObject("Team " + (team + 1)).transform;
            teamRoot.SetParent(transform, false);
            Transform frame = new GameObject("Formation").transform;
            frame.SetParent(teamRoot, false);
            frames[team] = frame;
            DrawOutline(frame, bounds.HalfSize, color);
            DrawOutline(frame, formation.GetBounds(UnitType.Ranged).HalfSize, Color.Lerp(color, Color.white, 0.45f));
            SyncFrame(team);

            for (int i = 0; i < TeamSize; i++)
            {
                bool ranged = i >= TeamSize / 2;
                int slot = i % (TeamSize / 2);
                Vector2 local = ranged
                    ? new Vector2((slot % 3 - 1) * 0.75f, (slot / 3 - 1.5f) * 0.8f)
                    : new Vector2(slot % 2 == 0 ? -1.6f : 1.6f, (slot / 2 - 2.5f) * 0.95f);
                Vector2 position = bounds.WorldPoint(local);
                int index = team * TeamSize + i;
                units[index] = new FlockUnit(formation, ranged ? UnitType.Ranged : UnitType.Melee, position);
                unitViews[index] = MakeView(ranged ? "Ranged" : "Melee",
                    ranged ? PrimitiveType.Cube : PrimitiveType.Sphere, teamRoot, Ground(position, FlockUnit.Radius),
                    Vector3.one * FlockUnit.Radius * (ranged ? 1.4f : 2f), color);
            }
        }

        void DrawOutline(Transform frame, Vector2 halfSize, Color color)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                MakeView("Edge", PrimitiveType.Cube, frame, new Vector3(sign * halfSize.x, 0, 0),
                    new Vector3(0.05f, 0.05f, halfSize.y * 2), color);
                MakeView("Edge", PrimitiveType.Cube, frame, new Vector3(0, 0, sign * halfSize.y),
                    new Vector3(halfSize.x * 2, 0.05f, 0.05f), color);
            }
        }

        void SyncFrame(int team)
        {
            GroundBox bounds = formations[team].GetBounds(UnitType.Melee);
            frames[team].SetPositionAndRotation(Ground(bounds.Center, 0.04f), Quaternion.LookRotation(Ground(bounds.Forward)));
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
