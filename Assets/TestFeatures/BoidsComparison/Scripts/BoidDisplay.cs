using UnityEngine;

namespace BoidsComparison
{
    // CPU 和 DOTS 共用同一套实例化绘制，每批不超过 Unity 的上限。
    internal sealed class BoidDisplay
    {
        const int BatchSize = 1023;
        readonly Matrix4x4[][] batches;
        readonly Mesh mesh;
        readonly Material material;

        internal BoidDisplay(int count, Material source)
        {
            batches = new Matrix4x4[(count + BatchSize - 1) / BatchSize][];
            for (int i = 0; i < batches.Length; i++)
                batches[i] = new Matrix4x4[Mathf.Min(BatchSize, count - i * BatchSize)];
            mesh = CreateArrow();
            material = new Material(source) { enableInstancing = true };
        }

        internal static Mesh CreateArrow()
        {
            var arrow = new Mesh { name = "Boid" };
            arrow.vertices = new[] { new Vector3(0, 0, 0.32f),
                new Vector3(0.14f, 0, -0.2f), new Vector3(-0.14f, 0, -0.2f) };
            arrow.triangles = new[] { 0, 1, 2 };
            arrow.RecalculateNormals();
            return arrow;
        }

        internal void Set(int i, Vector2 position, Vector2 velocity)
        {
            var rotation = Quaternion.LookRotation(new Vector3(velocity.x, 0, velocity.y));
            batches[i / BatchSize][i % BatchSize] = Matrix4x4.TRS(
                new Vector3(position.x, 0, position.y), rotation, Vector3.one);
        }

        internal void Draw()
        {
            foreach (var batch in batches)
                Graphics.DrawMeshInstanced(mesh, 0, material, batch, batch.Length);
        }

        internal void Release()
        {
            Object.Destroy(mesh);
            Object.Destroy(material);
        }
    }
}
