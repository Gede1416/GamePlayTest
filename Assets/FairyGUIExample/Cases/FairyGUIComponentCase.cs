using FairyGUI;
using UnityEngine;

[RequireComponent(typeof(UIPanel))]
public class FairyGUIComponentCase : MonoBehaviour
{
    [SerializeField] int caseIndex;
    GComponent view;
    Window normalWindow;
    Window modalWindow;
    GList virtualList;
    float progress;

    // 独立测试场景入口；界面由 UIPanel 加载编辑器发布资源。
    void Start()
    {
        Init();
    }

    void Update()
    {
        if (caseIndex != 9 || view == null) return;
        progress = (progress + Time.deltaTime * 20) % 100;
        foreach (GObject child in view.GetChildren())
            if (child is GProgressBar bar) bar.value = progress;
    }

    void OnDestroy()
    {
        Clear();
    }

    void Init()
    {
        view = GetComponent<UIPanel>().ui;
        if (view == null) return;
        switch (caseIndex)
        {
            case 0:
                virtualList = view.GetChild("n0").asList;
                virtualList.RemoveChildrenToPool();
                virtualList.SetVirtual();
                virtualList.itemRenderer = (index, item) =>
                {
                    item.text = $"物品 {index + 1:0000}";
                    item.icon = "ui://Basics/k19";
                };
                virtualList.numItems = 1000;
                virtualList.onClickItem.Add(context => Debug.Log($"[虚拟列表] {((GObject)context.data).text}", this));
                break;
            case 1:
                GTree tree = view.GetChild("tree2").asTree;
                tree.treeNodeRender = (node, cell) => cell.text = (string)node.data;
                for (int i = 0; i < 3; i++)
                {
                    GTreeNode folder = new GTreeNode(true) { data = $"分类 {i + 1}" };
                    tree.rootNode.AddChild(folder);
                    for (int j = 0; j < 10; j++)
                        folder.AddChild(new GTreeNode(false) { data = $"条目 {i + 1}-{j + 1}" });
                }
                tree.onClickItem.Add(context => Debug.Log($"[树形列表] {((GObject)context.data).text}", this));
                break;
            case 2:
                view.GetChild("n9").asList.scrollPane.pageMode = true;
                view.GetChild("n4").asList.scrollPane.snapToItem = true;
                break;
            case 3:
                normalWindow = new Window { contentPane = UIPackage.CreateObject("Basics", "WindowA").asCom };
                modalWindow = new Window { contentPane = UIPackage.CreateObject("Basics", "WindowB").asCom, modal = true };
                view.GetChild("n0").onClick.Add(() => { normalWindow.Center(); normalWindow.Show(); });
                view.GetChild("n1").onClick.Add(() => { modalWindow.Center(); modalWindow.Show(); });
                break;
            case 4:
                view.GetController("c1").onChanged.Add(() => Debug.Log($"[控制器] 页码 {view.GetController("c1").selectedIndex}", this));
                break;
            case 5:
                view.GetTransition("t0").Play(-1, 0, null);

                break;
            case 6:
                view.GetChild("a").draggable = true;
                GButton source = view.GetChild("b").asButton;
                source.draggable = true;
                source.onDragStart.Add(context =>
                {
                    context.PreventDefault();
                    DragDropManager.inst.StartDrag(source, source.icon, source.icon, (int)context.data);
                });
                GButton target = view.GetChild("c").asButton;
                target.icon = null;
                target.onDrop.Add(context => { target.icon = (string)context.data; Debug.Log("[拖放] 接收成功", this); });
                GObject bounds = view.GetChild("n7");
                GObject bounded = view.GetChild("d");
                bounded.draggable = true;
                bounded.dragBounds = bounds.TransformRect(new Rect(0, 0, bounds.width, bounds.height), GRoot.inst);
                break;
            case 7:
                view.GetChild("n12").asRichTextField.onClickLink.Add(context =>
                    Debug.Log($"[富文本] 链接 {context.data}", this));
                view.GetChild("n25").onClick.Add(() => view.GetChild("n24").text = view.GetChild("n22").text);
                break;
            case 8:
                foreach (GObject child in view.GetChildren())
                    if (child is GSlider slider)
                        slider.onChanged.Add(() => Debug.Log($"[滑块] {slider.name}: {slider.value:0.0}", this));
                break;
        }
        Debug.Log($"[FairyGUI 用例 {caseIndex + 1:00}] 已初始化；操作步骤见 Cases/README.md", this);
    }

    void Clear()
    {
        normalWindow?.Dispose();
        modalWindow?.Dispose();
        if (caseIndex == 6) DragDropManager.inst.Cancel();
        // UIPanel 拥有视图；销毁视图时一并清理其事件、虚拟列表和动效。
        view = null;
    }

    [ContextMenu("验证虚拟列表复用")]
    void CheckVirtualList()
    {
        if (!Application.isPlaying || virtualList == null) return;
        bool passed = virtualList.numItems == 1000 && virtualList.numChildren < 100;
        Debug.Assert(passed, "虚拟列表应有 1000 条数据，并仅创建可视区域附近的条目。", this);
        Debug.Log($"[虚拟列表] 数据={virtualList.numItems}，实例={virtualList.numChildren}，通过={passed}", this);
    }
}

