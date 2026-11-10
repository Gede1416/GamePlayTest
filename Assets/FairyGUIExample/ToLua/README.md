# FairyGUI 官方 ToLua 扩展测试

## 运行

用 Unity 2022.3 打开项目根目录 `My project`，等待编译后打开本目录的 `FairyGUIToLuaTest.unity`，按 Play。

- 右侧技能按钮：点击一次加一，数字显示在按钮上。
- 左侧技能按钮：清零。
- 选中 `FairyGUI ToLua Test`，在 `FairyGUIToLuaTest` 组件右键菜单选择“运行时自检：Lua 扩展与按钮事件”。
- 自检成功时 Console 输出 `[ToLua TEST PASS] extension, click, reset, dispose, reopen`。

场景复用已经发布的 `Cooldown/Main`，不需要再次发布 UI。布局仍来自 FairyGUI 资源，不在代码里拼 UI。原冷却演示和战斗场景未修改。

## 代码位置

- `Lua/CounterPanel.lua`：通过 `fgui.extension_class(GComponent)` 定义扩展，在创建视图之前调用 `fgui.register_extension`；负责取控件、监听点击、计数、刷新和释放。
- `Lua/FairyGUI.lua`：FairyGUI 官方 ToLua 适配文件，来自与当前 SDK 相同的提交 `8cc8f214cca79685532eef13372d0a4f74acdf08`，未修改。
- `FairyGUIToLuaTest.cs`：独立场景入口，启动 LuaState、绑定导出类型、执行 Lua，销毁时先释放界面再关闭 LuaState。
- `Assets/ThirdParty/ToLua/Editor/Custom/CustomSettings.cs`：类型导出配置；更改后在 Unity 执行 `Lua > Generate All`。
- `Assets/ThirdParty/ToLua/Generate`：已经生成的绑定代码，不要手改。
- `Assets/csc.rsp`：启用 `FAIRYGUI_TOLUA`，无需手工修改 Player Settings。

每个 UI 实例保存独立的 Lua 回调闭包，并用同一回调解除监听，避免关闭重开时复用已释放的 peer table。停止时恢复该资源的默认工厂，移除它对 Lua 扩展函数的引用。

## 接入范围与兼容调整

ToLua 来源：https://github.com/topameng/tolua ，固定提交见 `Assets/ThirdParty/ToLua/REVISION.txt`，保留 MIT 许可证。仅导入 Windows x64 原生库及所需托管依赖，未导入官方示例场景。

ToLua 运行时代码和生成绑定通过 `.asmref` 加入现有 FairyGUI 程序集，避免两者互相引用造成程序集循环；编辑器工具单独编译。原 FairyGUI C# 源码未修改。

上游代码的必要调整：Lua 路径和导出目录指向当前布局；关闭默认 Lua Socket 开关；`BlendWeights` 改为 Unity 2022 的 `SkinWeights`；批处理模式跳过首次生成的交互弹窗；Windows DLL 仅启用 Windows Editor / Win64。

此次目标是 **Windows Unity Editor 测试**。Lua 文件从 Assets 目录加载，未接入 Player 的 Lua 资源打包，也未验证其他平台；未修改 Build Settings。

## 验证

在隔离的 Unity 2022.3.62f1c1 项目副本中，连同当前项目脚本编译成功；播放模式下执行真实 Lua 扩展，验证加一、清零、销毁重开、整个 LuaState 销毁重建，两轮自检均通过。自检通过事件调用触发按钮，实际鼠标点击仍可按上面的操作步骤验证。

官方说明：https://fairygui.com/docs/unity/lua/
