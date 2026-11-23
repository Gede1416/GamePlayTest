using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(CombatTreeDemo))]
public sealed class CombatTreeInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("编辑行为树连线"))
            CombatTreeEditor.Open((CombatTreeDemo)target);
    }
}

public sealed class CombatTreeEditor : EditorWindow
{
    [SerializeField] CombatTreeDemo demo;

    internal static void Open(CombatTreeDemo demo)
    {
        var window = GetWindow<CombatTreeEditor>("行为树连线");
        window.demo = demo;
        window.Refresh();
    }

    void OnEnable()
    {
        Undo.undoRedoPerformed += Refresh;
        Refresh();
    }

    void OnDisable() => Undo.undoRedoPerformed -= Refresh;

    void Refresh()
    {
        rootVisualElement.Clear();
        if (!demo)
        {
            rootVisualElement.Add(new Label("请从测试场景的 CombatTreeDemo 组件打开。"));
            return;
        }
        rootVisualElement.Add(new Label("从根节点端口拖到行为输入口。端口 1 至 4 决定优先级；选中连线按 Delete 断开。保存场景即可保留修改。"));
        var graph = new CombatGraph(new SerializedObject(demo));
        graph.style.flexGrow = 1;
        rootVisualElement.Add(graph);
    }

    sealed class CombatGraph : GraphView
    {
        readonly Port[] slots = new Port[4];
        readonly Dictionary<CombatBranch, Port> inputs = new Dictionary<CombatBranch, Port>();
        readonly Port fightOutput;
        readonly Port plannerInput;
        readonly SerializedObject owner;

        public CombatGraph(SerializedObject owner)
        {
            this.owner = owner;
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentDragger());
            SetupZoom(0.3f, 1.5f);
            var root = AddNode("根节点 · 优先选择", 265, 18);
            var children = owner.FindProperty("tree.children");
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = AddPort(root, Direction.Output, (i + 1).ToString());
                var branch = (CombatBranch)(i + 1);
                var node = AddNode(CombatTree.Label(branch), 16 + i * 165, 240);
                inputs[branch] = AddPort(node, Direction.Input, "行为");
                if (branch == CombatBranch.Fight) fightOutput = AddPort(node, Direction.Output, "执行");
            }
            plannerInput = AddPort(AddNode("技能规划 · ReGoap", 511, 390), Direction.Input, "计划");
            for (int i = 0; i < slots.Length; i++)
            {
                var child = (CombatBranch)children.GetArrayElementAtIndex(i).enumValueIndex;
                if (inputs.TryGetValue(child, out var input)) AddElement(slots[i].ConnectTo(input));
            }
            if (owner.FindProperty("tree.skillPlanner").boolValue)
                AddElement(fightOutput.ConnectTo(plannerInput));
            graphViewChanged = Apply;
        }

        public override List<Port> GetCompatiblePorts(Port start, NodeAdapter adapter)
        {
            var result = new List<Port>();
            foreach (var port in ports.ToList())
            {
                if (port.direction == start.direction) continue;
                var output = start.direction == Direction.Output ? start : port;
                var input = start.direction == Direction.Input ? start : port;
                if ((output == fightOutput) == (input == plannerInput)) result.Add(port);
            }
            return result;
        }

        Node AddNode(string name, float x, float y)
        {
            var node = new Node { title = name };
            node.capabilities &= ~(Capabilities.Deletable | Capabilities.Movable | Capabilities.Copiable);
            node.SetPosition(new Rect(x, y, 155, 80));
            AddElement(node);
            return node;
        }

        static Port AddPort(Node node, Direction direction, string name)
        {
            var port = node.InstantiatePort(Orientation.Horizontal, direction, Port.Capacity.Single, typeof(bool));
            port.portName = name;
            (direction == Direction.Input ? node.inputContainer : node.outputContainer).Add(port);
            node.RefreshPorts();
            node.RefreshExpandedState();
            return port;
        }

        GraphViewChange Apply(GraphViewChange change)
        {
            if (owner.targetObject == null) return change;
            owner.Update();
            if (change.elementsToRemove != null)
                foreach (var item in change.elementsToRemove)
                    if (item is Edge edge) Write(edge, false);
            if (change.edgesToCreate != null)
                foreach (var edge in change.edgesToCreate) Write(edge, true);
            if (owner.ApplyModifiedProperties() && !Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(((CombatTreeDemo)owner.targetObject).gameObject.scene);
            return change;
        }

        void Write(Edge edge, bool connected)
        {
            if (edge.output == fightOutput)
            {
                owner.FindProperty("tree.skillPlanner").boolValue = connected;
                return;
            }
            int index = System.Array.IndexOf(slots, edge.output);
            foreach (var pair in inputs)
                if (pair.Value == edge.input)
                    owner.FindProperty("tree.children").GetArrayElementAtIndex(index).enumValueIndex =
                        (int)(connected ? pair.Key : CombatBranch.Empty);
        }
    }
}
