#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把当前场景里的战斗对象存成预制体：
/// Ranger（原名 entity）-> Assets/prefab/Entity.prefab（实体）
/// ground -> Assets/prefab/Map.prefab（地图）
/// TurnManager -> Assets/prefab/TurnController.prefab（回合控制器）
/// 按名字找物体，名字列表里按顺序取第一个找到的（场景里改过名就把新名字加进列表）
///
/// 首次加载脚本时自动跑一次（留下 Temp 里的标记，之后不再自动跑）；也可以随时手动跑：
/// 菜单 Tools/导出战斗对象预制体。
/// 注意：跨对象的场景引用（地图组件、actors 列表等）Unity 不允许写进预制体，会被置空；
/// 运行时这些引用由组件自己 FindObjectOfType 找回来（Awake 里）。
/// </summary>
public static class BattlePrefabExporter
{
    const string MarkerPath = "Temp/battle-prefabs.done";
    const string Folder = "Assets/prefab";

    [InitializeOnLoadMethod]
    static void AutoRunOnce()
    {
        if (File.Exists(MarkerPath)) return;

        EditorApplication.delayCall += () =>
        {
            Export();
            File.WriteAllText(MarkerPath, "done");
        };
    }

    [MenuItem("Tools/导出战斗对象预制体")]
    static void Export()
    {
        Directory.CreateDirectory(Folder);

        Save("Entity", "实体", "Ranger", "entity");
        Save("Map", "地图", "ground");
        Save("TurnController", "回合控制器", "TurnManager");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void Save(string prefabName, string label, params string[] sceneObjectNames)
    {
        GameObject go = null;
        foreach (var objectName in sceneObjectNames)
        {
            go = GameObject.Find(objectName);           // 只找当前已加载场景里的激活物体
            if (go != null) break;
        }

        if (go == null)
        {
            Debug.LogWarning($"[BattlePrefab] 当前打开的场景里找不到 {string.Join(" / ", sceneObjectNames)}，" +
                             $"先打开 Assets/Scenes/SampleScene.unity 再跑一次");
            return;
        }

        var path = $"{Folder}/{prefabName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);      // 只生成预制体文件，不动场景里的物体
        Debug.Log($"[BattlePrefab] {label}：{go.name} -> {path}");
    }
}
#endif
