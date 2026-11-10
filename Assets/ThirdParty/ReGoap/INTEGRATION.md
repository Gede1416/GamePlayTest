# ReGoap 接入说明

## 来源与范围

- 上游：https://github.com/luxkun/ReGoap
- 固定提交：`69eeea4a5489506b2e0d3f2db4a02c288d8d38fa`（2026-11-03 导入）。
- 许可证：原始 `LICENSE`（Apache-2.0）；保留源码中的第三方声明。
- 导入 `Core`、`Planner`、`Utilities`、Unity 根目录适配组件，以及两个 Editor 调试窗口脚本。
- 未导入 Godot、FSM/FSMExample、上游 Test 及 NUnit 测试，未引入额外包。
- `ReGoap.asmdef` 是运行时程序集，自动供 `Assembly-CSharp` 引用；`ReGoap.Editor.asmdef` 仅在编辑器编译。

本次只安装库，没有更改战斗场景、预制体、Entity、移动或技能管线。

## 使用入口

核心 API 在 `ReGoap.Core` / `ReGoap.Planner`；Unity 组件在 `ReGoap.Unity`。

按上游 `Unity/README.md`，创建具体的非泛型派生组件，统一使用同一组泛型参数，例如 `string, object`：

1. 派生 `ReGoapMemory<string, object>`，由 Sensor 写入世界事实。
2. 派生 `ReGoapAction<string, object>`，配置前置条件、效果、成本并实现实际动作。
3. 派生 `ReGoapGoal<string, object>`，配置目标状态及优先级。
4. 派生 `ReGoapAgent<string, object>`，挂到对应对象；场景放一个同类型的 `ReGoapPlannerManager<string, object>` 具体派生组件。
5. 播放时通过 `Window > ReGoap Debugger` 检查 Agent 的世界状态和计划。

也可以直接调用 `ReGoapPlanner<string, object>.Plan(...)`，通过核心接口接入现有回合流程，不使用 Unity 组件的自动执行循环。

规划效果只描述预计变化，实际移动、攻击和记忆更新仍由业务代码负责。接入当前战斗时，应由现有 BattleManager / Entity 的 Init、Clear 和回合流程掌握执行时机；上游组件有自己的 Awake / Update 生命周期，不能直接挂上就视为已接入现有战斗。

## 本地兼容补丁

`Planner/PlanDebugger.cs`：在 `NET_STANDARD_2_0` / `NET_STANDARD_2_1` 下排除旧 T4 生成器的 `CompilerErrorCollection` 字段、`Errors` 属性和 `Error` / `Warning` 方法。这些诊断成员没有内部调用，Unity 的 .NET Standard 不提供所需 CodeDom 类型；规划及 DOT 图导出代码保留。未更改项目 API 兼容级别或引入 CodeDom DLL。

上游更新或从 `PlanDebugger.tt` 重新生成源码时，需重新检查此补丁。更新时保留现有 `.meta` GUID 和本地两个 asmdef，不要把整个上游仓库直接覆盖进 Assets。

## 本次验证

- 使用已安装 Unity 2022.3.62f1 的 Roslyn 编译器及本项目生成的程序集引用，按 .NET Standard 2.1 编译 26 个运行时源码、2 个 Editor 源码：通过。
- 独立 Mono 冒烟测试：规划选择低成本的 `Equip -> Attack`，逐步前置条件成立、达到目标、规划本身不执行效果；无可用动作时返回无解：通过。
- 不依赖 CodeDom 的 DOT 导出：通过。
- 未执行 Unity 编辑器内播放及 Player 构建；上述检查不代表已经验证游戏内行为。
