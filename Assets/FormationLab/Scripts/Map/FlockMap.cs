using UnityEngine;

namespace FormationLab
{
    internal sealed class FlockMap
    {
        readonly GroundBox[] obstacles;

        internal FlockMap(params GroundBox[] obstacles)
        {
            this.obstacles = obstacles;
        }

        internal Vector2 ResolveMove(Vector2 from, Vector2 to, float radius)
        {
            // 先找路径上的第一面墙，再沿墙移动。遇到第二面墙时停在接触点。
            int wall = FirstHit(from, to, radius, -1, out Vector2 contact, out Vector2 slideEnd);
            if (wall < 0) return to;
            int second = FirstHit(contact, slideEnd, radius, wall, out Vector2 corner, out _);
            return second < 0 ? slideEnd : corner;
        }

        int FirstHit(Vector2 from, Vector2 to, float radius, int skip, out Vector2 contact, out Vector2 slideEnd)
        {
            int first = -1;
            float earliest = float.PositiveInfinity;
            contact = slideEnd = to;
            for (int i = 0; i < obstacles.Length; i++)
            {
                if (i == skip || !obstacles[i].TrySlide(from, to, radius, out float time,
                    out Vector2 hit, out Vector2 end) || time >= earliest) continue;
                first = i;
                earliest = time;
                contact = hit;
                slideEnd = end;
            }
            return first;
        }
    }
}
