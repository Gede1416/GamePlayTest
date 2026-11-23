# ReGoap 使用测试场景

打开 `Assets/TestFeatures/ReGoapTest/ReGoapTestScene.unity`，点击 Unity 的播放按钮即可。无须手动挂组件或配置引用。

- 青色胶囊：Agent；金色长方体：武器；红色胶囊：敌人。
- 默认有武器：`Take weapon → Approach enemy → Sword attack`，规划成本 3。
- 按 **B** 或点击 **No weapon**：重开并移除武器，规划改为 `Approach enemy → Unarmed attack`，成本 11。
- 按 **R** 或点击 **With weapon**：恢复默认测试，可在执行中反复重开。
- 左上角显示目标、完整计划、当前动作、总成本及世界事实。完成时显示 `Goal reached`，敌人消失。

## 代码入口

`ReGoapTestAgent.cs` 是纯 C# 世界状态、动作和目标模型，调用真实 `ReGoapPlanner<string, object>.Plan` 选出动作序列。动作列表刻意先放昂贵的徒手攻击，便于验证规划结果由条件与成本决定。

`ReGoapTestManager.cs` 是测试场唯一的初始化入口，负责创建简单物体、执行返回的计划、在实际动作完成后更新世界事实，以及重开时取消协程和释放资源。所有物体在运行时生成，地面为 XZ 平面。材质由场景引用现有 `ground-material.mat`，不使用 Shader.Find。

本例演示核心规划器的显式执行方式，不使用上游 Unity Agent / PlannerManager 的自动执行循环，因此上游 ReGoap Debugger 不会自动发现这个纯 C# Agent；使用场景 HUD 和 Console 检查。

规划成本是人为设定的权重，未按实际距离计算。移动采用直线插值，攻击简化为消除目标，不使用战斗系统、NavMesh、寻路避障或伤害结算。

## 自动检查

编辑器菜单 **Tools → ReGoap → 验证测试场景规划** 可在非播放状态运行，无需 NUnit：检查两种计划顺序、低成本选择、逐步前置条件、目标达成、规划不直接执行效果，以及无动作时的无解返回。

场景引用与结构检查，在 Unity 项目根目录运行：

```powershell
python _validate_scene.py Assets/TestFeatures/ReGoapTest/ReGoapTestScene.unity
```
