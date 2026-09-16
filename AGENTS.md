# AGENTS.md — 项目记录

Unity 项目 `My project`，3D 俯视角，**y 为高度**（地面在 XZ 平面）。

## 环境事实（已核实）

- Unity `2022.3.62f2c1`（`ProjectSettings/ProjectVersion.txt`）
- `activeInputHandler: 0` → **老输入系统**，用 `Input.GetAxisRaw` / `Input.GetKey*`，不是 `InputAction`
- 没有 asmdef：`Assets/Scripts/*.cs` 直接编译进 `Assembly-CSharp`
- 资源：`Assets/Scenes/SampleScene.unity`；材质 `Assets/prefab/{entity,ground,skill}-material.mat`
- 场景已不是空场景：`entity` / `entity (1)` / `ground` / `TestCanvas` + 4 个按钮 / `EventSystem` / `TurnManager`，详见下面"场景"一节（旧的 4 个 `skill_*` 已删，`skill-material.mat` 材质文件还留着）

## 脚本清单

- `Assets/Scripts/`：`Entity`（实体）、`NavTest`（测试用）
- `Assets/Scripts/Managers/`：管理器（`MapManager` / `TurnManager`）
- `Assets/Scripts/Components/`：功能组件（`Health` / `ObjectMover`）
- `Assets/Scripts/Pipeline/Movement/`：移动管线（`AutoPilot` / `PathPipeline` / `TargetSources` / `PathPipelineFactory`）
- `Assets/Scripts/Pipeline/Attack/`：攻击管线（`Attacker` / `AttackPipeline`）
- `Assets/Editor/`：编辑器工具（`SceneAutoReload`、`BattlePrefabExporter`）

| 文件 | 职责 | 对外接口 |
|---|---|---|
| `Components/ObjectMover.cs` | 只收移动指令并播协程：`Move(step)` → 查剩余步数 → 问 `MapManager.TryMove` 要合法性与落点 → `Vector3.Lerp` 走到落点；**不碰地图数据**，动画中 `walking` 阻断后续指令；**没有键盘输入**（WASD 已移除，指令只从移动管线/外部脚本来） | `MapManager map`、`float speed`、`int stepLimit`（每回合步数上限，0=不限）、`int stepsLeft`、`bool HasSteps`、`void ResetSteps()`、`bool Move(Vector2Int step)`、`bool IsMoving` |
| `Pipeline/Movement/AutoPilot.cs` | 寻路管线编排（`[RequireComponent(typeof(ObjectMover))]`）：阶段一 → 阶段二 → 阶段三；三段由 `Entity.Wire()` 装配，`Awake` 里的 `??=` 只是兜底 | `ITargetSource TargetSource { get; set; }`、`IPathPlanner Planner { get; set; }`、`IPathExecutor Executor { get; set; }`、`bool RunPipeline()`、`bool MoveTo(Vector3)`、`bool MoveTo(Vector2Int)`、`void Stop()`、`List<Vector2Int> path`、`bool IsFollowing`、`int Team`（取自自己的 `Health.team`） |
| `Pipeline/Movement/PathPipeline.cs` | 三个阶段接口（依赖一律构造函数注入）+ `BfsPathPlanner(map)`（**薄壳，真正寻路在 `MapManager.FindPath`**）+ `MoverPathExecutor(map, mover)`（逐格交给 ObjectMover） | `ITargetSource.TryGetTarget(out cell)`、`IPathPlanner.TryBuild(start, goal, path)`、`IPathExecutor.Run(path)` |
| `Pipeline/Movement/TargetSources.cs` | 阶段一的两个实现，依赖由构造函数注入（map / self / team / mover），距离一律用 `MapManager.Manhattan` | `ApproachNearestEnemy(map, self, team)`：走向最近的非己方，落在它四周离自己最近的空格；`FleeNearestEnemy(map, self, team, mover)`：只在**本回合走得到**的格子里（曼哈顿距离 ≤ `mover.stepsLeft`；`stepLimit` 为 0 时视为不限）挑离最近敌方最远的格 |
| `NavTest.cs` | 测试用：`GoUp/GoDown/GoLeft/GoRight` 找 `anchor` 四周最近的可进入格子，再让 `pilot` 走过去；含手动目标 `manualTarget`（运行时改值即出发，右键组件菜单也能触发） | `MapManager map`、`Transform anchor`、`AutoPilot pilot`、`Vector2Int manualTarget`、`bool GoNearest(Vector2Int)`、`bool GoTo(Vector2Int)` |
| `Components/Health.cs` | 生命值 + 阵营，死亡时 `SetActive(false)` 并打日志 | `float maxHealth`、`int team`、`float Current`、`bool IsDead`、`TakeDamage(float)` |
| `Managers/MapManager.cs` | 网格 + **两份地图数据**（`int[,] cells` 格子→uuid 二维图、`Dictionary<int,Vector2Int> positions` uuid→位置，另有 `byUuid` uuid→实体），外部只读；`TryMove`/`CancelMove`/`SyncOccupied` 负责维护 | `Init()`、`Build()`、`SyncOccupied()`（重建 + uuid 查重）、`ResetEntities()`、`UuidAt(cell)`、`EntityAt(cell)`、`EntitiesAt(cells)`、`EntityOf(uuid)`、`TryGetCell(uuid, out cell)`、`IsOccupied`、`CanEnter`、`InBounds`、`static Manhattan(a, b)`、`CellToWorld(col,row)` / `CellToWorld(cell)`、`WorldToCell`、`TryMove(from, step, out to)`、`CancelMove(from, to)`、`FindPath(from, to, path)`（四方向等权 BFS）、`SelfCheck()` / `SelfCheckPath()`（右键菜单自检）、常量 `Empty=0` / `Unknown=-1` |
| `Managers/TurnManager.cs` | 回合管理。数据：战斗实体列表 `actors` / 行动栈 `ActionStack`（每回合按先攻重建）/ 回合状态 `State`；每回合逐个出栈行动（`yield return actor.TakeTurnRoutine()`），跑满 `totalRounds` 结束 | `List<Entity> actors`、`List<Entity> ActionStack`、`TurnState State`（Idle / Running / Finished）、`int CurrentRound`、`Entity CurrentActor`、`int StackLeft`、`bool IsFinished`、`void Init(TurnInitData)`、`TurnInitData`（actors / totalRounds / turnDelay）、`void StartBattle()`、`void StopBattle()`、`void BuildActionStack()`、`Entity PopNext()`、`int totalRounds`、`float turnDelay`、`bool autoStart` |
| `Entity.cs` | 实体：组件的统一入口 + **对外唯一门面**（回合 / 地图 / UI 只认 Entity）；身份、先攻、回合步数、管线引用由自己持有，生命值与阵营归 `Health`（只转发），**技能由管线组件承担**（不再单独存技能数据） | `int Uuid`、`int initiative`（先攻）、`int moveSteps`、`float Hp` / `MaxHp`、`bool IsDead`、`int Team`、`void TakeDamage(float)`、`void Init(EntityInitData)`、`EntityInitData`（uuid / initiative / moveSteps / map / moveSource / attackType）、`AutoPilot Pilot`、`ObjectMover Mover`、`Health Health`、`Attacker Attacker`、`TargetSourceType SourceType { get; set; }`、`void Wire()`、`void ApplySteps()`、`IEnumerator TakeTurnRoutine()`（攻击→移动→攻击）、`bool AttackOnce()` |
| `Pipeline/Movement/PathPipelineFactory.cs` | `TargetSourceType` 枚举（ApproachNearestEnemy / FleeNearestEnemy）+ 静态工厂：按枚举造阶段一、统一造阶段二/三 | `CreateSource(type, map, self, team, mover)`、`CreatePlanner(map)`、`CreateExecutor(map, mover)`、`Wire(pilot, type, map, self, team, mover)` |
| `Pipeline/Attack/AttackPipeline.cs` | 攻击管线三段接口（**都不依赖技能对象**，范围/目标数/伤害各自带）+ 实现：`CooldownCastCheck(cooldown)`（活着+冷却，顺带实现 `ICooldown`）、`MeleeTargetFinder(map)`（范围 1、1 目标）、`RangedTargetFinder(map)`（范围 3、1 目标）、`TargetPicker.Pick(map, caster, range, count, targets)`（两个 finder 共用的挑选逻辑）、`DamageCaster(damage)` | `ICastCheck.CanCast(caster)`、`ITargetFinder.TryFindTargets(caster, targets)`、`ISkillCaster.Cast(caster, targets)`、`ICooldown.StartCooldown()/TickTurn()` |
| `Pipeline/Attack/Attacker.cs` | 攻击管线编排（挂在实体上，`[RequireComponent(typeof(Entity))]`）：阶段一 → 二 → 三；`Awake`/`Build()` 按攻击类型装配三段（近战/远程各用各的目标获取），属性可替换；含 `[ContextMenu]` 手动跑一次 | `AttackType attackType`（Melee=近战范围1 / Ranged=远程范围3）、`AttackType Type { get; set; }`、`float damage`、`int cooldown`、`ICastCheck CastCheck { get; set; }`、`ITargetFinder TargetFinder { get; set; }`、`ISkillCaster Caster { get; set; }`、`void Build()`、`bool RunPipeline()`、`void TickTurn()`、`List<Entity> targets` |

## 场景（Assets/Scenes/SampleScene.unity）

- `ground`：MapManager（cellSize 1；地面 Plane 10×10 → Cols/Rows 10；`entities` = [entity, entity (1)]，`spawnPoints` = [(1,1), (5,5)] 是**格子坐标**）
- `entity`：Transform/MeshFilter/MeshRenderer + BoxCollider + ObjectMover（`map` 已指向 ground 的 MapManager）+ **AutoPilot** + Health(`team: 0`) + **Entity(uuid 1, 先攻 10, moveSteps 4)** + **Attacker(近战，范围 1 / 1 目标，伤害 10 / 冷却 0)**（Rigidbody 已被用户在编辑器里删掉；旧的 4 个 skill_* 碰触体子物体已随 `Attack` 一起删除）
- `entity (1)`：BoxCollider + Health（`team: 1`，敌方）+ **ObjectMover + AutoPilot**（`map` / `mover` 已接好）+ **Entity(uuid 2, 先攻 10, moveSteps 3, targetSourceType = FleeNearestEnemy)** ——只有移动管线（没有 AutoPilot 之外的攻击组件）
- `TestCanvas`：Canvas(Overlay) + CanvasScaler + GraphicRaycaster + `NavTest`；子物体 btn_up / btn_down / btn_left / btn_right，onClick 分别指到 `NavTest.GoUp / GoDown / GoLeft / GoRight`
- `EventSystem`：EventSystem + StandaloneInputModule（老输入系统）
- `TurnManager`：只有 TurnManager 组件（没有渲染物），`actors` = [entity 上的 Entity, entity (1) 上的 Entity]，先攻在各自 `Entity.initiative` 上（现在都是 10 → 相同则按列表顺序，entity 先动），`totalRounds` 5、`autoStart` 开 → 播放就自动跑 5 回合
- 场景检查：`python _validate_scene.py`（查 fileID 引用、组件归属、父子关系、SceneRoots、缩进）；手改场景前的备份在 `SampleScene.unity.bak`
- 预制体：`Assets/prefab/Entity.prefab`（实体，来自场景 `entity`）、`Map.prefab`（地图，来自 `ground`）、`TurnController.prefab`（回合控制器，来自 `TurnManager`）——由编辑器工具 `Assets/Editor/BattlePrefabExporter.cs` 生成（首次加载自动跑一次，标记在 `Temp/battle-prefabs.done`；**结构改了要删掉标记或走菜单 `Tools/导出战斗对象预制体` 重新导出**，否则预制体里的字段会停留在旧版本）。
  `Entity.prefab` 里带着导出时那份 `uuid`：**实例化多个实体时要各自覆盖 uuid**（预制体覆盖），否则地图会报 uuid 重复。
  注意：跨对象的**场景**引用（`MapManager` 组件、`MapManager.entities`、`TurnManager.actors` 等）Unity 不允许写进预制体，生成时会被置空；运行时靠组件 `Awake` 里的 `FindObjectOfType<MapManager>()` 找回来，`actors` / `entities` 这类列表要在场景里的实例上重新接。
- 编辑器工具 `Assets/Editor/SceneAutoReload.cs`：磁盘上的 `.unity` 一变就自动重新加载当前场景（内存里未保存的版本先另存到 `Temp/编辑器未保存版本_*.unity`）；菜单 `Tools/场景以磁盘为准` 开关（默认开）、`Tools/重新加载当前场景（以磁盘为准）` 手动触发；播放中不动场景
- 注意：按钮目标 = 参照物四周最近的可进入格子；spawnPoints 现在是 [(1,1), (5,5)]，`entity (1)` 不再贴角落，四个方向都能走

## 约定

- 注释、Tooltip 用中文；字段名用英文。
- 只写被要求的功能：不加接口/工厂/配置项，不加脚手架。故意砍掉的东西在回复里说明"跳过了 X，需要 Y 时再加"，不预先实现。
- 用 `#` 对 `Vector2Int` 的格子坐标：`.x` = 列（世界 x 方向），`.y` = 行（世界 z 方向），**不是世界高度**。
- 世界坐标用 `Vector3`，地面用 `Vector2` 存 `(x, z)`；不要用 Vector2 直接赋给 `transform.position`（会把 y/z 清 0）。
- 管线三段（移动：`ITargetSource` / `IPathPlanner` / `IPathExecutor`；攻击：`ICastCheck` / `ITargetFinder` / `ISkillCaster`）的依赖都走**构造函数注入**，接口只收"这次要处理什么"（起点/终点/路径；施法者/目标列表）；不要往接口里塞 AutoPilot / MapManager / Entity。
- **地图数据**：格子→uuid 二维图与 uuid→位置/实体 字典都由 `MapManager` 维护，外部只读查询（`UuidAt` / `EntityAt` / `EntitiesAt` / `EntityOf` / `TryGetCell`）；要改只能走 `TryMove` / `CancelMove` / `SyncOccupied`。
- **数据所有权**：一份数据只有一个所有者。身份(uuid) / 先攻 / 回合步数 / 管线类型 归 `Entity`；生命值 + 阵营归 `Health`；剩余步数、动画中、冷却剩余属于各自组件的运行期状态。`Entity` 是对外唯一门面（`Hp` / `MaxHp` / `IsDead` / `Team` / `TakeDamage()` 转发），**不要把组件自己的属性搬进 Entity**（会变成两份真相 + 组件不能单独工作）。
- `Health` 是 2D/3D 无关的，其余脚本的维度假设见上表。
- 需要可视化的逻辑（如网格划分）用 `OnDrawGizmosSelected` 画出来核对，不写单元测试。

## 已知缺口（用户明确跳过的）

- `ObjectMover`：格子移动是四方向直线插值——无斜向、无转向、无寻路绕障碍；`TryMove` 通过时起点的占用就放开了，所以动画中起点是空的（要"动画中两侧都占住"就加个 `Arrive(from)`，到达时再放开起点）；步数上限（`stepLimit`）对所有移动指令都生效（`Move()` 是唯一入口），一格算一步，`Entity.TakeTurnRoutine()` 开始时补满，回合外调 `MoveTo` 也吃这个限制；`MapManager.TryMove` 现在要求**起点也在网格内**（走到地图外的物体会拒绝移动）
- `MapManager`：地图数据只按 `entities` 列表重建（不在列表里的实体会走、格子记 `Unknown`，索引里查不到）；`entities` 被搬动/销毁后要自己调 `SyncOccupied()` 对齐；寻路是 `MapManager.FindPath`（四方向、每格等权 BFS，`BfsPathPlanner` 只是薄壳；A* / 加权代价没做）；无地形/障碍数据
- `AutoPilot`：管线只在被调用时跑一次（不会周期性重算/自动追人）、路径算完不重算（中途被挡就放弃）、BFS 的目标格必须可进入（站着人的格子不能当终点）；三段装配以 `Entity.Wire()` 为准，`AutoPilot.Awake` 里的 `??=` 只是没挂 Entity 时的兜底
- `TargetSources`：目标从 `MapManager.entities`（地图就是单位注册表）里找、按曼哈顿距离挑最近——**不在 map 实体列表里的单位不会被当成目标**；"靠近"落地成"站到敌人四周离自己最近的空格"（敌人那格进不去，**不按步数裁剪**——目标在预算外就这回合走一段、下回合接着走），曼哈顿距离 ≤ 1 视为已贴身、这次不产生目标；"远离"只在本回合步数可达的菱形里挑最远格（地图没有障碍时等价于可达，以后有地形要换成按步数上限做 BFS 洪泛）
- `Health.team`：阵营就是个 int，没有仇恨表/友军保护
- `TurnManager`：行动内容写死在 `Entity.TakeTurnRoutine()` 里（固定 攻击-移动-攻击，没做成可配置的行动序列；换顺序就改它）；状态只能轮询 `State` / `IsFinished`，没有回合开始/结束事件；行动栈每回合重建一次，**回合中不重排**（先攻变了要等下回合）；先攻相同时按列表顺序而不掷骰；没有"跳过/延后/守卫"这类规则
- `Entity` / `PathPipelineFactory`：先攻只有 `Entity.initiative` 一份（`TurnManager` 的排序和行动都走 Entity）；`Entity.Wire()` 每调一次就重建三段（正常只在 `Awake` 调一次，运行中重复调不会打断正在走的协程）
- `Attacker`：已接进回合（`Entity.TakeTurnRoutine()` 里攻击-移动-攻击，回合开始会 `TickTurn()` 推进冷却）；目标获取从 `MapManager.entities` 里找，只认"挂了 `Entity` 且有 `Health`"的单位、按曼哈顿距离排序；`DamageCaster` 只扣血（没有击退/buff/动画表现）；远程没有视线/弹道判定（只看格子距离）（碰触体那套 `Attack` 与 `SkillManager` 都已删除，攻击只有攻击管线一条路）
- 预制体：`Assets/prefab/*.prefab` 只是对象模板，跨对象引用会被 Unity 置空（见「场景」一节）；场景里目前用的还是原来那几个物体，没有换成预制体实例（要换成实例就用 `SaveAsPrefabAssetAndConnect`）
- `Entity.uuid`：场景里手填（`entity`=1、`entity (1)`=2）；`SyncOccupied()` 现在会查重但**只警告不修正**（重复时后者这次被跳过），uuid 分配器还没做
- `NavTest`：纯测试组件——按钮文字用英文（内置字体没有中文字形）、不管连点/换目标、依赖 Inspector 里接好 map / anchor / pilot
- 脚本都还没在播放模式下跑过（两个角色都没有 Rigidbody，移动是直接写 `transform.position`）；预制体由 `BattlePrefabExporter` 生成，见「场景」一节

## 按用户给的结构做的重构（进度）

- **P1 实体数据** ✅ `Entity`：uuid / 先攻(`initiative`) / 回合步数 / 生命值门面（`Hp`/`TakeDamage`，数据仍归 `Health`）/ `Init(EntityInitData)`；技能由管线组件承担，不单独存
- **P2 地图数据** ✅ `MapManager`：`int[,] cells` 格子→uuid 二维图 + `Dictionary<int,Vector2Int>` uuid→位置（+ uuid→实体），查询 API 与 uuid 查重
- **P3 寻路** ✅ 搬进 `MapManager.FindPath`，`BfsPathPlanner` 退成薄壳
- **P4 回合** ✅ `TurnManager`：行动栈 `ActionStack`（每回合按先攻重建，`PopNext()` 出栈）+ `TurnState` 状态 + `Init(TurnInitData)`
- **P5 移动管线「接近」** — 用户确认是笔误（现有 靠近/远离 即全部），未做
- **P6 收尾** ✅ 场景 / 预制体同步 + 本文档校对
- **额外清理** ✅ 删掉旧技能系统的残留：`SkillManager`、碰触体 `Attack` 与 4 个 `skill_*` 子物体（攻击只剩攻击管线一条路）；预制体等 Unity 重载后由 `BattlePrefabExporter` 重新导出
- 未做：Map 配置对象（用户说暂时不用）

## 版本管理

- 仓库：https://github.com/Gede1416/GamePlayTest.git，remote 名 `origin`，分支 `main`
- **每次改动都要单独提交一次**，提交信息按 `COMMIT_CONVENTION.md` 写：`<type>(<scope>): <中文描述>`，type 用 feat/fix/docs/style/refactor/perf/test/build/ci/chore/revert，scope 参考该文件里的模块表，一个提交只做一件事（用户靠提交记录回看修改）
- `.gitignore` 已排除 `Library/`、`Temp/`、`Logs/`、`obj/`、`UserSettings/` 和 Unity 生成的 `*.csproj` / `*.sln`
- 提交脚本改动前的自检：复制 `Assembly-CSharp.csproj` 成临时 csproj，把所有 `<Compile Include="..." />` 换成一条 `<Compile Include="Assets\**\*.cs" />` 再 `dotnet build`（Unity 生成的清单会过期，文件搬过目录后尤其明显；这份 csproj 自带 UnityEditor 程序集引用，编辑器脚本一并覆盖）；0 错误 0 警告后再提交，临时 csproj / bin / obj 删掉
- 提交场景改动前的自检：`python _validate_scene.py`
- `commit` 不受沙箱影响；**`push` 需要放宽沙箱**（凭据管理器要创建管道，受限模式下报 `couldn't create signal pipe, Win32 error 5` + `could not read Username`）。推不上去就把命令交给用户在终端里跑一次。
- 本仓库本地设了 `http.sslBackend=openssl`：这台机器上 Windows schannel 握不上手（`SEC_E_NO_CREDENTIALS`）
- `SampleScene.unity.bak` 现在被 `.gitignore` 排除，git 已经能回看历史，不再需要它

## 改动前请注意

`Assets/Scripts/` 下的文件用户会直接改（例如 `ObjectMover`、`Health`、`MapManager` 都已被手改过）。
**改之前先读当前文件，只做最小改动，不要整文件重写覆盖用户的手改。**

`Assets/Scenes/SampleScene.unity` 也可以直接改：Unity 那边的 `SceneAutoReload` 会以磁盘为准自动重载（见"场景"一节的编辑器工具），所以**不需要再提醒用户先保存或手动重载**；只在编辑器里手工改场景时才会出现"编辑器版本覆盖磁盘"的情况（那时丢掉的编辑器版本会备份到 `Temp/`）。
