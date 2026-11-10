using FairyGUI;
using LuaInterface;
using UnityEngine;

// 独立测试场景入口：C# 只启动 Lua，界面交互由 Lua 扩展负责。
public class FairyGUIToLuaTest : MonoBehaviour
{
    LuaState lua;

    void Start()
    {
        Init();
    }

    void OnDestroy()
    {
        Clear();
    }

    void Init()
    {
        GRoot.inst.SetContentScaleFactor(1136, 640);
        lua = new LuaState();
        LuaBinder.Bind(lua);
        DelegateFactory.Init();
        lua.Start();
        lua.DoFile("CounterPanel.lua");
        lua.Call("ToLuaCounter.Start", false);
        Debug.Log("[ToLua 测试] 右侧按钮加一，左侧按钮清零；组件右键菜单可运行自检。", this);
    }

    void Clear()
    {
        if (lua == null) return;
        try
        {
            lua.Call("ToLuaCounter.Stop", false);
        }
        finally
        {
            lua.Dispose();
            lua = null;
        }
    }

    [ContextMenu("运行时自检：Lua 扩展与按钮事件")]
    void CheckInteraction()
    {
        if (!Application.isPlaying || lua == null) return;
        lua.Call("ToLuaCounter.Check", false);
    }
}
