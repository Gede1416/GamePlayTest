# FairyGUI 工作流测试

## 运行

1. Unity Hub 打开 `E:/_Goethe/Unity/project/My project`（不要把 Assets 当项目打开）。
2. 等待资源导入与编译完成，打开 `Assets/FairyGUIExample/FairyGUIWorkflow.unity`，按 Play。
3. 两个技能图标应显示循环冷却。点击任一图标：冷却重置，Console 输出对应 b0/b1 日志。
4. 停止后再播放，确认重新初始化；主战斗场景不受影响。

## 编辑 → 发布 → Unity

1. 安装官方 FairyGUI 编辑器：https://www.fairygui.com/product.html
2. 用 FairyGUI 编辑器打开本目录 `UIProject~/Workflow.fairy`。
3. 打开 Cooldown 包的 Main，修改 b0/b1 的位置或按钮外观；保留组件名 b0、b1，以及按钮内部 mask。
4. 发布 Cooldown 包。发布路径已经设置为 `../Resources/UI`（相对 UIProject~），即本目录 Resources/UI。
5. 确认生成/更新 Cooldown_fui.bytes 和 Cooldown_atlas0.png。不要改为 JSON 格式。
6. 回到 Unity，等待导入，重新 Play；验证布局变更及点击回调仍有效。

UIProject~ 的尾部波浪号让 Unity 忽略编辑器源工程；发布产物才进入 Unity。
技能图标 k0/k1 由 Resources/Cooldown 加载，不在 UI 图集中。

## 接线位置

- 场景 UIPanel：packageName = Cooldown、componentName = Main、packagePath = UI/Cooldown。
- Cooldown/CooldownMain.cs：获取 b0/b1、绑定点击、更新 mask.fillAmount 和倒计时。
- SDK：Assets/ThirdParty/FairyGUI；官方 MIT 源码及许可证，REVISION.txt 固定本次导入提交。
- 独立测试入口 Start 调用 Init，销毁时 Clear 解除按钮事件；视图销毁归 UIPanel。不接入战斗 Init/Clear 链。

## 验收与限制

检查场景脚本引用、包文件及编辑器源资源齐全后，还需要在本机 Unity Play 模式验证图标、遮罩与点击。
未自动安装 FairyGUI 编辑器，未添加 Build Settings 场景，也未验证打包。
官方源码：https://github.com/fairygui/FairyGUI-unity

## 运行时自检

播放后选中场景的 UIPanel 物体，在 CooldownMain 组件的右键菜单选择“运行时自检：重新初始化与点击”。
自检会连续初始化两次，然后触发两个按钮的事件，检查冷却重置、遮罩填满与数字显示。
预期 Console 出现 b0、b1 各一条点击日志以及“自检通过”；重复运行不应增加点击日志次数。
这个检查验证事件接线；真实鼠标命中仍需在 Game 视图手动点击确认。

## 常见问题

- 找不到 Cooldown 包：确认 UIPanel 的 packagePath 为 UI/Cooldown，Resources/UI 内存在 Cooldown_fui.bytes。
- 提示缺少 b0/b1/mask：恢复 FairyGUI 编辑器里的组件名，再发布。
- 修改后没有更新：先停止 Play，发布资源，等待 Unity 导入，再重新播放。
- 图标空白：检查 Resources/Cooldown/k0.png 和 k1.png。
- 没有 SDK 菜单或脚本不能运行：先解决 Console 中的编译错误；项目应从 My project 打开。

本次移除了官方示例的全局帧率修改和 Esc 退出监听，点击重置会立即刷新遮罩与倒计时。

## 综合控件实例（新增，需先发布）

场景：`Assets/FairyGUIExample/ControlsShowcase.unity`。
脚本：`ControlsShowcase.cs`。UI 源文件：`UIProject~/assets/Basics/Main.xml`。

这个实例包含：
- 左侧滚动列表：30 条数据，支持滚轮、拖动、选中高亮。
- 下拉框：全部、装备、消耗品；筛选后重新选中第一项。
- Toggle：控制右侧图片显示。
- 图片：选择不同行时切换现有技能图标。
- 文字：标题、使用提示、详情和操作结果。
- 按钮：确认当前选择，更新结果文字并输出 Console 日志。

首次运行步骤：
1. 从 https://www.fairygui.com/product.html 安装 FairyGUI 编辑器。
2. 打开 `UIProject~/Workflow.fairy`，进入 **Basics** 包，打开 **Main**。
3. 发布 **Basics** 包到 `../Resources/UI`，保持 Unity 二进制格式；确认生成 `Resources/UI/Basics_fui.bytes` 及相关图集。
4. Unity 导入后打开 ControlsShowcase 场景，再按 Play。
5. 测试列表滚动到末尾；下拉框筛选应由 30 条变为 15 条；点选不同项应更新详情与图片；Toggle 应显隐图片；按钮应显示确认结果。

注意：Cooldown 是原冷却测试包；Basics 是此次综合实例的包，两者独立。
Basics 控件皮肤复用官方示例，因此保留其配套资源与组件。布局在 FairyGUI 源文件里，代码只负责数据和事件，不在代码里拼 UI。
目前已完成脚本编译与 XML/资源路径检查；尚未发布新 Basics 包，也未验证 Unity 播放及实际鼠标交互。未发布前请不要运行新场景。
