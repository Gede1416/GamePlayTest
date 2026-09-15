# AGENTS.md — 项目记录

Unity 项目 `My project`，3D 俯视角，**y 为高度**（地面在 XZ 平面）。

## 环境事实（已核实）

- Unity `2022.3.62f2c1`（`ProjectSettings/ProjectVersion.txt`）
- `activeInputHandler: 0` → **老输入系统**，用 `Input.GetAxisRaw` / `Input.GetKey*`，不是 `InputAction`
- 没有 asmdef：`Assets/Scripts/*.cs` 直接编译进 `Assembly-CSharp`
- 资源：`Assets/Scenes/SampleScene.unity`；材质 `Assets/prefab/{entity,ground,skill}-material.mat`
- 没有 Rigidbody / 碰撞体 / 实体对象，场景还是默认空场景，脚本靠手动挂

## 脚本清单（Assets/Scripts/）

| 文件 | 职责 | 对外接口 |
|---|---|---|
| `ObjectMover.cs` | 只收移动指令并播协程：`Move(step)` → 问 `MapManager.TryMove` 要合法性与落点 → `Vector3.Lerp` 走到落点；**不碰地图数据**，动画中 `walking` 阻断输入 | `MapManager map`、`float speed`、`Vector3 moveInput`、`bool Move(Vector2Int step)`、`bool IsMoving`、`static Vector2Int ToStep(Vector2)`、`static Vector3 ReadWASD()` |
| `AutoPilot.cs` | 寻路管线编排（`[RequireComponent(typeof(ObjectMover))]`）：阶段一 → 阶段二 → 阶段三；`Awake` 里 `new` 出默认实现装配，三个属性可被外部替换（以后由实体类装配） | `ITargetSource TargetSource { get; set; }`、`IPathPlanner Planner { get; set; }`、`IPathExecutor Executor { get; set; }`、`bool RunPipeline()`、`bool MoveTo(Vector3)`、`bool MoveTo(Vector2Int)`、`void Stop()`、`List<Vector2Int> path`、`bool IsFollowing`、`int Team`（取自自己的 `Health.team`） |
| `PathPipeline.cs` | 三个阶段接口（依赖一律构造函数注入）+ `BfsPathPlanner(map)`（BFS 四方向等权）+ `MoverPathExecutor(map, mover)`（逐格交给 ObjectMover） | `ITargetSource.TryGetTarget(out cell)`、`IPathPlanner.TryBuild(start, goal, path)`、`IPathExecutor.Run(path)` |
| `TargetSources.cs` | 阶段一的两个实现，依赖由构造函数注入（map / self / team），距离一律用 `MapManager.Manhattan` | `ApproachNearestEnemy(map, self, team)`：走向最近的非己方，落在它四周离自己最近的空格；`FleeNearestEnemy(map, self, team)`：全图找离最近敌方最远、自己能站、走起来最省的格 |
| `NavTest.cs` | 测试用：`GoUp/GoDown/GoLeft/GoRight` 找 `anchor` 四周最近的可进入格子，再让 `pilot` 走过去；含手动目标 `manualTarget`（运行时改值即出发，右键组件菜单也能触发） | `MapManager map`、`Transform anchor`、`AutoPilot pilot`、`Vector2Int manualTarget`、`bool GoNearest(Vector2Int)`、`bool GoTo(Vector2Int)` |
| `Health.cs` | 生命值 + 阵营，死亡时 `SetActive(false)` 并打日志 | `float maxHealth`、`int team`、`float Current`、`bool IsDead`、`TakeDamage(float)` |
| `Attack.cs` | 碰触体（Collider + Is Trigger）碰到带 `Health` 的对象就扣血 | `float damage`、`Health haver`（排除自己/主人） |
| `SkillManager.cs` | 缓存技能 GameObject 列表，键 1~6 显示对应项 0.5 秒，同时只能放一个 | `List<GameObject> skills`、`float showTime`、`Show(int index)` |
| `MapManager.cs` | 自己管理网格与格子占用数据（外部只读）：按 map 包围盒 x/z 划分网格、实体初始位置（格子坐标）、`TryMove` 裁决移动并改占用 | `Build()`、`SyncOccupied()`、`ResetEntities()`、`CellToWorld`、`WorldToCell`、`InBounds`、`IsOccupied`、`CanEnter`、`static Manhattan(a, b)`、`TryMove(from, step, out to)`、`CancelMove(from, to)` |

## 场景（Assets/Scenes/SampleScene.unity）

- `ground`：MapManager（cellSize 1；地面 Plane 10×10 → Cols/Rows 10；`entities` = [entity, entity (1)]，`spawnPoints` = [(1,1), (0,0)] 是**格子坐标**）
- `entity`：Rigidbody + BoxCollider + ObjectMover（`map` 已指向 ground 的 MapManager）+ **AutoPilot** + Health + SkillManager；4 个 skill_* 是它的子物体
- `entity (1)`：BoxCollider + Health（`team: 1`，敌方）—— 寻路测试的参照物；`entity` 的 Health 是 `team: 0`
- `TestCanvas`：Canvas(Overlay) + CanvasScaler + GraphicRaycaster + `NavTest`；子物体 btn_up / btn_down / btn_left / btn_right，onClick 分别指到 `NavTest.GoUp / GoDown / GoLeft / GoRight`
- `EventSystem`：EventSystem + StandaloneInputModule（老输入系统）
- 场景检查：`python _validate_scene.py`（查 fileID 引用、组件归属、父子关系、SceneRoots、缩进）；手改场景前的备份在 `SampleScene.unity.bak`
- 注意：按钮目标 = 参照物四周最近的可进入格子，`entity (1)` 现在被 spawnPoints 放在 (0,0) 角落，所以 Down / Left 会因出界而拒绝（日志会说明）

## 约定

- 注释、Tooltip 用中文；字段名用英文。
- 只写被要求的功能：不加接口/工厂/配置项，不加脚手架。故意砍掉的东西在回复里说明"跳过了 X，需要 Y 时再加"，不预先实现。
- 用 `#` 对 `Vector2Int` 的格子坐标：`.x` = 列（世界 x 方向），`.y` = 行（世界 z 方向），**不是世界高度**。
- 世界坐标用 `Vector3`，地面用 `Vector2` 存 `(x, z)`；不要用 Vector2 直接赋给 `transform.position`（会把 y/z 清 0）。
- 管线三段（`ITargetSource` / `IPathPlanner` / `IPathExecutor`）的依赖走**构造函数注入**，接口只收"这次要处理什么"（起点/终点/路径）；不要往接口里塞 AutoPilot 或 MapManager。
- `Health` 是 2D/3D 无关的，其余脚本的维度假设见上表。
- 需要可视化的逻辑（如网格划分）用 `OnDrawGizmosSelected` 画出来核对，不写单元测试。

## 已知缺口（用户明确跳过的）

- `ObjectMover`：格子移动是四方向直线插值——无斜向、无转向、无寻路绕障碍；`TryMove` 通过时起点的占用就放开了，所以动画中起点是空的（要"动画中两侧都占住"就加个 `Arrive(from)`，到达时再放开起点）
- `Attack`：无攻击冷却、无阵营/友伤过滤、无挥砍窗口（靠启用/禁用 Collider 触发 enter）
- `SkillManager`：无冷却、无前摇、无打断（`Show` 在释放中直接忽略输入）
- `MapManager`：占用数据只按 `entities` 列表重建（不在列表里的实体会走但不会被记录占用）；`entities` 被搬动/销毁后要自己调 `SyncOccupied()` 对齐；无地形/障碍数据、无寻路
- `AutoPilot`：管线只在被调用时跑一次（不会周期性重算/自动追人）、路径算完不重算（中途被挡就放弃）、BFS 的目标格必须可进入（站着人的格子不能当终点）；三段现在由 `AutoPilot.Awake` 用 `??=` 兜底装配，**还没有实体类来统一构造**（用户说后续会加）
- `TargetSources`：每次调用都 `FindObjectsOfType<Health>()` 并按曼哈顿距离挑最近（没有单位注册表/分帧）；"靠近"落地成"站到敌人四周离自己最近的空格"（敌人那格进不去）；"远离"全图扫描一遍
- `Health.team`：阵营就是个 int，没有仇恨表/友军保护
- `NavTest`：纯测试组件——按钮文字用英文（内置字体没有中文字形）、不管连点/换目标、依赖 Inspector 里接好 map / anchor / pilot
- 场景里还没有任何预制体，脚本都还没在播放模式下跑过（`entity` 带 Rigidbody + 重力，配合直接写 `transform.position` 的移动可能抖）

## 版本管理

- 仓库：https://github.com/Gede1416/GamePlayTest.git，remote 名 `origin`，分支 `main`
- **每次改动都要单独提交一次**，提交信息按 `COMMIT_CONVENTION.md` 写：`<type>(<scope>): <中文描述>`，type 用 feat/fix/docs/style/refactor/perf/test/build/ci/chore/revert，scope 参考该文件里的模块表，一个提交只做一件事（用户靠提交记录回看修改）
- `.gitignore` 已排除 `Library/`、`Temp/`、`Logs/`、`obj/`、`UserSettings/` 和 Unity 生成的 `*.csproj` / `*.sln`
- 提交脚本改动前的自检：把 `Assembly-CSharp.csproj` 里没列到的 `Assets\Scripts\*.cs` 临时补成 `<Compile Include="..." />`，再 `dotnet build Assembly-CSharp.csproj`；0 错误 0 警告后再提交，跑完把 csproj 还原（Unity 会自己重新生成）
- 提交场景改动前的自检：`python _validate_scene.py`
- `commit` 不受沙箱影响；**`push` 需要放宽沙箱**（凭据管理器要创建管道，受限模式下报 `couldn't create signal pipe, Win32 error 5` + `could not read Username`）。推不上去就把命令交给用户在终端里跑一次。
- 本仓库本地设了 `http.sslBackend=openssl`：这台机器上 Windows schannel 握不上手（`SEC_E_NO_CREDENTIALS`）
- `SampleScene.unity.bak` 现在被 `.gitignore` 排除，git 已经能回看历史，不再需要它

## 改动前请注意

`Assets/Scripts/` 下的文件用户会直接改（例如 `ObjectMover`、`Attack`、`Health` 都已被手改过）。
**改之前先读当前文件，只做最小改动，不要整文件重写覆盖用户的手改。**
