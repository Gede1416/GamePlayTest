using UnityEngine;

/// <summary>
/// 预制体加载：先按文件名走 Resources（打包后也能用），编辑器里再退回按资源路径加载。
/// BattleManager 与 BattleUIManager 都用它，省得两边各写一份。
/// </summary>
public static class PrefabLoader
{
    #region 公开方法

    /// <summary>按路径加载预制体；加载不到会报错并返回 null</summary>
    public static GameObject Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var go = Resources.Load<GameObject>(System.IO.Path.GetFileNameWithoutExtension(path));
#if UNITY_EDITOR
        if (go == null) go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
        if (go == null)
            Debug.LogError($"[PrefabLoader] 加载不到预制体：{path}（放进 Resources 目录就能在打包后也加载到）");

        return go;
    }

    #endregion
}
