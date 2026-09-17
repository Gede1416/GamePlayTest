#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 让 Unity 以磁盘上的场景文件为准：外部工具（脚本）直接改了 .unity 文件后，
/// 编辑器会自动重新加载，不再需要手动切窗口/避免误保存覆盖。
/// 丢掉编辑器里未保存的改动之前，会先另存一份到 Temp/ 备份。
/// 开关：菜单 Tools/场景以磁盘为准（默认开）。
/// </summary>
[InitializeOnLoad]
public static class SceneAutoReload
{
    const string MenuPath = "Tools/场景以磁盘为准";
    const string PrefKey = "DSH.SceneAutoReload";
    const double Interval = 1.0;          // 秒，多久查一次文件时间

    static bool hooked;
    static double nextCheck;
    static string lastPath;               // 盯着哪个场景
    static DateTime lastWrite;            // 上次看到的磁盘写入时间

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    static SceneAutoReload()
    {
        if (hooked) return;               // 关了域重载时 update 会重复挂
        hooked = true;
        EditorApplication.update += Tick;
    }

    [MenuItem(MenuPath, false, 100)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Debug.Log($"[SceneAutoReload] 以磁盘为准：{(Enabled ? "开" : "关")}");
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    [MenuItem("Tools/重新加载当前场景（以磁盘为准）", false, 101)]
    static void ReloadActiveScene()
    {
        Reload(SceneManager.GetActiveScene(), "手动触发");
    }

    static void Tick()
    {
        if (!Enabled) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;   // 播放中不动场景
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + Interval;

        var scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path) || !scene.isLoaded) return;

        var write = File.GetLastWriteTimeUtc(scene.path);

        // 换场景了（或第一次）就重新记基准，不触发重载
        if (scene.path != lastPath)
        {
            lastPath = scene.path;
            lastWrite = write;
            return;
        }

        if (write == lastWrite) return;    // 磁盘没变
        lastWrite = write;

        // 编辑器自己保存也会改写入时间，但那时内存和磁盘一致，不用重载
        if (!scene.isDirty)
        {
            Debug.Log($"[SceneAutoReload] {scene.path} 在磁盘上变了，重新加载");
        }

        Reload(scene, "磁盘上的文件变了");
    }

    static void Reload(Scene scene, string reason)
    {
        var path = scene.path;
        if (string.IsNullOrEmpty(path) || !scene.isLoaded) return;

        // 内存里有没保存的改动：先另存一份再丢，避免真的丢东西
        if (scene.isDirty)
        {
            var backup = Path.Combine("Temp",
                $"编辑器未保存版本_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(path)}");
            try
            {
                EditorSceneManager.SaveScene(scene, backup, true);   // saveAsCopy：不动原场景路径
                Debug.LogWarning($"[SceneAutoReload] {reason}，按磁盘重新加载 {path}；" +
                                 $"编辑器里未保存的改动已另存到 {backup}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneAutoReload] 备份失败，取消重载以免丢改动：{e.Message}");
                return;
            }
        }
        else
        {
            Debug.Log($"[SceneAutoReload] {reason}，按磁盘重新加载 {path}");
        }

        try
        {
            EditorSceneManager.CloseScene(scene, true);                   // 丢掉内存版本
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);     // 从磁盘读回来
            lastPath = path;
            lastWrite = File.GetLastWriteTimeUtc(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneAutoReload] 重新加载 {path} 失败：{e.Message}");
        }
    }
}
#endif
