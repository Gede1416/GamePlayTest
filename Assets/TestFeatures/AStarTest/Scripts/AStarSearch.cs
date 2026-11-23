using System;
using System.Collections.Generic;
using UnityEngine;

// 每次 Step 从候选集合取出一个格子，方便在两次选择之间观察搜索状态。
internal sealed class AStarSearch
{
    internal enum State { Searching, Found, NoPath }
    internal enum Mark { Unseen, Open, Closed, Path }

    private static readonly Vector2Int[] Directions =
        { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

    private readonly bool[,] walls;
    private readonly Vector2Int start;
    private readonly Vector2Int goal;
    private readonly List<Vector2Int> open = new List<Vector2Int>();
    private readonly Vector2Int[,] parent;
    private readonly int[,] cost;
    private readonly Mark[,] marks;

    internal State Status { get; private set; }
    internal Vector2Int Current { get; private set; }
    internal int StepCount { get; private set; }
    internal int Candidates => open.Count;

    internal AStarSearch(bool[,] walls, Vector2Int start, Vector2Int goal)
    {
        this.walls = walls;
        this.start = start;
        this.goal = goal;
        int width = walls.GetLength(0);
        int height = walls.GetLength(1);
        cost = new int[width, height];
        marks = new Mark[width, height];
        parent = new Vector2Int[width, height];
        marks[start.x, start.y] = Mark.Open;
        open.Add(start);
    }

    internal Mark MarkAt(Vector2Int cell) => marks[cell.x, cell.y];
    internal int G(Vector2Int cell) => cost[cell.x, cell.y];
    internal int H(Vector2Int cell) => Math.Abs(cell.x - goal.x) + Math.Abs(cell.y - goal.y);
    internal int F(Vector2Int cell) => G(cell) + H(cell);

    internal void Step()
    {
        if (Status != State.Searching) return;

        // 小地图直接扫描，方便对应屏幕上的分数。F 相同时优先 H 小的，再按入队顺序。
        int best = 0;
        for (int i = 1; i < open.Count; i++)
        {
            Vector2Int a = open[i];
            Vector2Int b = open[best];
            if (F(a) < F(b) || (F(a) == F(b) && H(a) < H(b))) best = i;
        }
        Current = open[best];
        open.RemoveAt(best);
        marks[Current.x, Current.y] = Mark.Closed;
        StepCount++;

        // 终点被选中时，沿父格回到起点，即可还原最短路线。
        if (Current == goal)
        {
            for (Vector2Int cell = goal; cell != start; cell = parent[cell.x, cell.y])
                marks[cell.x, cell.y] = Mark.Path;
            marks[start.x, start.y] = Mark.Path;
            Status = State.Found;
            return;
        }

        foreach (Vector2Int direction in Directions)
        {
            Vector2Int next = Current + direction;
            if (next.x < 0 || next.x >= walls.GetLength(0) ||
                next.y < 0 || next.y >= walls.GetLength(1)) continue;
            if (walls[next.x, next.y] || MarkAt(next) == Mark.Closed) continue;

            // 第一次发现，或经当前格走过去更短，都要更新 G 与父格。
            int newCost = G(Current) + 1;
            if (MarkAt(next) == Mark.Open && newCost >= G(next)) continue;
            if (MarkAt(next) == Mark.Unseen) open.Add(next);
            marks[next.x, next.y] = Mark.Open;
            cost[next.x, next.y] = newCost;
            parent[next.x, next.y] = Current;
        }
        if (open.Count == 0) Status = State.NoPath;
    }
}
