using UnityEngine;

namespace BoidsComparison
{
    public sealed class GpuBoidsDemo : MonoBehaviour
    {
        [SerializeField, Min(1), Tooltip("重新启用场景组件后生效")] int count = 512;
        [SerializeField] ComputeShader simulation;
        [SerializeField] Shader drawShader;
        [SerializeField, Range(0, 6), Tooltip("远离过近的邻居")] float separationWeight = 3f;
        [SerializeField, Range(0, 6), Tooltip("朝邻居的平均速度转向")] float alignmentWeight = 1f;
        [SerializeField, Range(0, 6), Tooltip("朝附近邻居的中心转向")] float cohesionWeight = 0.8f;

        static readonly Bounds FieldBounds = new Bounds(Vector3.zero, new Vector3(20, 2, 20));
        ComputeBuffer current;
        ComputeBuffer next;
        int kernel;
        int groups;
        Mesh mesh;
        Material material;

        void OnEnable()
        {
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsInstancing)
            {
                Debug.LogError("GPU 集群演示需要支持计算着色器和实例化绘制的显卡。", this);
                enabled = false;
                return;
            }
            mesh = BoidDisplay.CreateArrow();
            material = new Material(drawShader) { enableInstancing = true };
            kernel = simulation.FindKernel("Step");
            simulation.GetKernelThreadGroupSizes(kernel, out uint threads, out _, out _);
            groups = Mathf.CeilToInt(count / (float)threads);
            current = new ComputeBuffer(count, 4 * sizeof(float));
            next = new ComputeBuffer(count, 4 * sizeof(float));
            current.SetData(BoidSample.Create(count));
        }

        void FixedUpdate()
        {
            simulation.SetInt("_Count", current.count);
            simulation.SetFloat("_DeltaTime", Time.fixedDeltaTime);
            simulation.SetVector("_Weights", new Vector4(separationWeight, alignmentWeight, cohesionWeight, 0));
            simulation.SetBuffer(kernel, "_Previous", current);
            simulation.SetBuffer(kernel, "_Next", next);
            simulation.Dispatch(kernel, groups, 1, 1);
            (current, next) = (next, current);
        }

        void LateUpdate()
        {
            // 顶点着色器直接读取当前模拟结果。
            material.SetBuffer("_Boids", current);
            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, FieldBounds, current.count);
        }

        void OnDisable()
        {
            current?.Release();
            next?.Release();
            current = next = null;
            Destroy(mesh);
            Destroy(material);
        }
    }
}
