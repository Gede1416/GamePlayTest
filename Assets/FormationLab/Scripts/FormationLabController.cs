using UnityEngine;

namespace FormationLab
{
    public sealed class FormationLabController : MonoBehaviour
    {
        [SerializeField] Camera groundCamera = null;
        [SerializeField, Min(0), Tooltip("阵型中心每秒移动的世界距离")] float marchSpeed = 2.5f;

        const int Count = 24;
        readonly FlockUnit[] units = new FlockUnit[Count];
        readonly Transform[] unitViews = new Transform[Count];
        MarchingFormation formation;
        FlockSimulation simulation;
        Transform frame;
        Transform enemy;
        Vector2 destination;

        void Awake()
        {
            var bounds = new GroundBox
            {
                Center = new Vector2(-5, 0), Forward = Vector2.up, HalfSize = new Vector2(3, 2)
            };
            var wall = new GroundBox
            {
                Center = new Vector2(2, 2), Forward = new Vector2(0.6f, 0.8f), HalfSize = new Vector2(0.35f, 2.5f)
            };
            formation = new MarchingFormation(bounds);
            destination = bounds.Center;
            frame = new GameObject("Formation").transform;
            frame.SetParent(transform, false);
            Color edge = new Color(0.3f, 0.8f, 0.7f);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                MakeView("Edge", PrimitiveType.Cube, frame, new Vector3(sign * bounds.HalfSize.x, 0, 0),
                    new Vector3(0.05f, 0.05f, bounds.HalfSize.y * 2), edge);
                MakeView("Edge", PrimitiveType.Cube, frame, new Vector3(0, 0, sign * bounds.HalfSize.y),
                    new Vector3(bounds.HalfSize.x * 2, 0.05f, 0.05f), edge);
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
                Vector2 position = bounds.Center + new Vector2((i % 6 - 2.5f) * 0.85f, (i / 6 - 1.5f) * 0.8f);
                if (i == 0) position.x -= 2;
                Transform view = MakeView(ranged ? "Ranged" : "Melee", PrimitiveType.Sphere, transform,
                    Ground(position, FlockUnit.Radius), Vector3.one * (FlockUnit.Radius * 2),
                    ranged ? new Color(0.25f, 0.65f, 1) : new Color(1, 0.72f, 0.12f));
                unitViews[i] = view;
                units[i] = new FlockUnit(formation, ranged ? UnitType.Ranged : UnitType.Melee, position);
            }
            simulation = new FlockSimulation(formation, new FlockMap(wall), units);
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
            Vector2 target = new Vector2(enemy.position.x, enemy.position.z);
            simulation.Step(destination, target, marchSpeed, Time.fixedDeltaTime);
            SyncFrame();
            for (int i = 0; i < units.Length; i++)
                unitViews[i].position = Ground(units[i].Position, FlockUnit.Radius);
        }

        void SyncFrame()
        {
            frame.SetPositionAndRotation(Ground(formation.Bounds.Center, 0.04f), Quaternion.LookRotation(Ground(formation.Bounds.Forward)));
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
