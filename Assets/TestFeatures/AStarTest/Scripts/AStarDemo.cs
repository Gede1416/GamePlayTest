using UnityEngine;

// 独立教学场景的入口。格子的第二个坐标是行号，画面中向上递增。
public sealed class AStarDemo : MonoBehaviour
{
    private const int Cols = 10;
    private const int Rows = 7;
    private const int CellSize = 56;
    private static readonly Vector2Int StartCell = new Vector2Int(1, 3);
    private static readonly Vector2Int Goal = new Vector2Int(8, 3);
    private readonly bool[,] walls = new bool[Cols, Rows];
    private AStarSearch search;
    private bool playing;
    private float nextStep;
    private GUIStyle label;
    private GUIStyle tile;
    private GUIStyle button;

    private void Awake()
    {
        // 直线方向上有一堵墙，观察 H 低估绕行距离的情形。
        for (int row = 1; row < Rows; row++) walls[5, row] = true;
        Restart();
    }

    private void Update()
    {
        if (playing && Time.unscaledTime >= nextStep) Advance();
    }

    private void OnGUI()
    {
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            tile = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
        }
        Matrix4x4 oldMatrix = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 900f, Screen.height / 520f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 900 * scale) / 2,
            (Screen.height - 520 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);

        Paint(new Rect(0, 0, 900, 520), new Color(0.07f, 0.10f, 0.14f));
        GUI.Label(new Rect(18, 10, 550, 32), "A* 寻路   /   四方向网格 · 每步代价 1", label);
        for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Cols; col++)
                DrawCell(new Vector2Int(col, row));

        bool wasEnabled = GUI.enabled;
        GUI.enabled = search.Status == AStarSearch.State.Searching;
        if (GUI.Button(new Rect(18, 450, 130, 40), "下一步", button))
        {
            playing = false;
            Advance();
        }
        if (GUI.Button(new Rect(158, 450, 130, 40), playing ? "暂停" : "自动演示", button))
        {
            playing = !playing;
            nextStep = Time.unscaledTime;
        }
        GUI.enabled = wasEnabled;
        if (GUI.Button(new Rect(298, 450, 130, 40), "重新搜索", button)) Restart();
        DrawExplanation();
        GUI.matrix = oldMatrix;
    }

    private void Restart()
    {
        playing = false;
        search = new AStarSearch(walls, StartCell, Goal);
    }

    private void Advance()
    {
        search.Step();
        nextStep = Time.unscaledTime + 0.5f;
        if (search.Status != AStarSearch.State.Searching) playing = false;
    }

    private void DrawCell(Vector2Int cell)
    {
        Rect rect = new Rect(18 + cell.x * CellSize, 48 + (Rows - 1 - cell.y) * CellSize, 54, 54);
        AStarSearch.Mark mark = search.MarkAt(cell);
        Color color = new Color(0.19f, 0.23f, 0.28f);
        string caption = "";
        if (walls[cell.x, cell.y]) { color = Color.black; caption = "墙"; }
        if (mark == AStarSearch.Mark.Open) color = new Color(0.08f, 0.38f, 0.43f);
        if (mark == AStarSearch.Mark.Closed) color = new Color(0.17f, 0.27f, 0.47f);
        if (search.StepCount > 0 && search.Current == cell) color = new Color(0.63f, 0.31f, 0.07f);
        if (mark == AStarSearch.Mark.Path) color = new Color(0.13f, 0.44f, 0.24f);
        if (cell == StartCell) caption = "起";
        if (cell == Goal) caption = "终";
        if (mark != AStarSearch.Mark.Unseen)
            caption += (caption.Length > 0 ? "\n" : "") + search.F(cell);
        Paint(rect, color);
        GUI.Label(rect, caption, tile);
        if (cell != StartCell && cell != Goal && GUI.Button(rect, GUIContent.none, GUIStyle.none))
        {
            walls[cell.x, cell.y] = !walls[cell.x, cell.y];
            Restart();
        }
    }

    private void DrawExplanation()
    {
        GUI.Label(new Rect(598, 44, 286, 110),
            "怎样选下一格？\n候选格里先选 F 最小的。同分时先选 H 较小的，再按发现顺序选。", label);
        GUI.Label(new Rect(598, 150, 286, 104),
            "格中数字是 F = G + H\nG  从起点走到这里的已知步数\nH  忽略墙到终点的横纵距离之和", label);
        string status = "起点已加入候选，点击下一步。";
        if (search.StepCount > 0)
        {
            Vector2Int c = search.Current;
            status = $"选中 ({c.x}, {c.y})   F = {search.G(c)} + {search.H(c)} = {search.F(c)}";
        }
        if (search.Status == AStarSearch.State.Found)
            status += $"\n沿父格回溯，最短路线 {search.G(Goal)} 步。";
        if (search.Status == AStarSearch.State.NoPath)
            status += "\n候选已用尽，终点不可达。";
        GUI.Label(new Rect(598, 263, 286, 105),
            $"第 {search.StepCount} 步   ·   候选 {search.Candidates} 格\n{status}", label);
        GUI.Label(new Rect(598, 379, 288, 120),
            "青色 候选   ·   蓝色 已搜索\n橙色 当前   ·   绿色 最短路线\n\n点击空地或墙编辑地图并重新搜索。", label);
    }

    private static void Paint(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
