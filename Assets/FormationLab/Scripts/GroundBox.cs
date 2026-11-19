using UnityEngine;

namespace FormationLab
{
    // XZ 平面上的有向矩形，局部坐标的 x、y 分别沿右方和前方。
    internal struct GroundBox
    {
        internal Vector2 Center;
        internal Vector2 Forward;
        internal Vector2 HalfSize;
        Vector2 Right => new Vector2(Forward.y, -Forward.x);

        internal Vector2 WorldPoint(Vector2 local)
        {
            return Center + Right * local.x + Forward * local.y;
        }

        internal Vector2 ReturnOffset(Vector2 point, float radius)
        {
            Vector2 local = LocalPoint(point);
            Vector2 extent = HalfSize - Vector2.one * radius;
            Vector2 nearest = new Vector2(Mathf.Clamp(local.x, -extent.x, extent.x),
                Mathf.Clamp(local.y, -extent.y, extent.y));
            return Right * (nearest.x - local.x) + Forward * (nearest.y - local.y);
        }

        // 对膨胀后的障碍做线段检测，保留切向位移。出生点必须在障碍外。
        internal bool TrySlide(Vector2 from, Vector2 to, float radius, out float hitTime,
            out Vector2 contact, out Vector2 slideEnd)
        {
            hitTime = 1f;
            contact = slideEnd = to;
            Vector2 start = LocalPoint(from);
            Vector2 end = LocalPoint(to);
            Vector2 delta = end - start;
            Vector2 extent = HalfSize + Vector2.one * radius;
            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(delta[axis]) < 0.000001f) continue;
                float face = delta[axis] > 0 ? -extent[axis] : extent[axis];
                float time = (face - start[axis]) / delta[axis];
                if (time < 0 || time > 1) continue;
                int tangent = 1 - axis;
                if (Mathf.Abs(start[tangent] + delta[tangent] * time) > extent[tangent]) continue;
                hitTime = time;
                end[axis] = face - Mathf.Sign(delta[axis]) * 0.0001f;
                Vector2 hit = start + delta * time;
                hit[axis] = end[axis];
                contact = WorldPoint(hit);
                slideEnd = WorldPoint(end);
                return true;
            }
            return false;
        }

        Vector2 LocalPoint(Vector2 point)
        {
            Vector2 offset = point - Center;
            return new Vector2(Vector2.Dot(offset, Right), Vector2.Dot(offset, Forward));
        }
    }
}
