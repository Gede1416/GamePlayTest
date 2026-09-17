# AGENTS.md — 项目记录

Unity 项目 `My project`，3D 俯视角，**y 为高度**（地面在 XZ 平面）。

## 环境事实（已核实）

- Unity `2022.3.62f2c1`（`ProjectSettings/ProjectVersion.txt`）
- `activeInputHandler: 0` → **老输入系统**，用 `Input.GetAxisRaw` / `Input.GetKey*`，不是 `InputAction`
- 没有 asmdef：`Assets/Scripts/*.cs` 直接编译进 `Assembly-CSharp`
- 资源：`Assets/Scenes/SampleScene.unity`；材质 `Assets/prefab/{entity,ground,skill}-material.mat`（`skill-material.mat` 已经没有引用了，随时可删）
- 场景现在只有「`BattleManager` + 一个空 `map_pos`」加 Directional Light / Main Camera / EventSystem：战斗对象全部由 `BattleManager` 在运行时从预制体加载出来，详见下面"场景"一节

## 脚本清单

- `Assets/Scripts/`：`Entity`（实体，唯一的顶层脚本）
- `Assets/Scripts/Managers/`：管理器（`BattleManager` / `MapManager` / `TurnManager`）
- `Assets/Scripts/Components/`：功能组件（`Health`）
- `Assets/Scripts/Pipeline/Movement/`：移动管线（`AutoPilot` / `PathPipeline` / `TargetSources` / `PathPipelineFactory`）
- `Assets/Scripts/Pipeline/Attack/`：攻击管线（`Attacker` / `AttackPipeline` / `AttackPipelineFactory`）
- `Assets/Editor/`：编辑器工具（`SceneAutoReload`、`BattlePrefabExporter`）

| 文件 | 职责 | 对外接口 |
|---|---|---|
| `Pipeline/Movement/AutoPilot.cs` | 寻路管线编排：阶段一 → 阶段二 → 阶段三；**装配在本组件内**（`Build()` 调 `PathPipelineFactory.Wire`，按枚举造阶段一），和攻击管线的 `Attacker` 一个套路；阶段三自己改坐标，**没有独立的移动组件**；本回合可走步数也存在这里，只有"远离"挑落点时读它 | `TargetSourceType targetSourceType`（枚举定义在 PathPipelineFactory.cs）、`MapManager map`、`float speed`（世界单位/秒）、`TargetSourceType SourceType { get; set; }`、`void Build()`、`void ApplySteps(int steps)`、`ITargetSource TargetSource { get; set; }`、`IPathPlanner Planner { get; set; }`、`IPathExecutor Executor { get; set; }`、`bool RunPipeline()`、`bool MoveTo(Vector3)`、`bool MoveTo(Vector2Int)`、`void Stop()`、`List<Vector2Int> path`、`bool IsFollowing`、`int Team`（取自自己的 `Health.team`） |
| `Pipeline/Movement/PathPipeline.cs` | 三个阶段接口（依赖一律构造函数注入）+ `BfsPathPlanner(map)`（**薄壳，真正寻路在 `MapManager.FindPath`**）+ `MoverPathExecutor(map, self, speed)`（**自己把实体逐格插值挪过去**，落点与占用问 `MapManager.TryMove`，不再有 ObjectMover 组件） | `ITargetSource.TryGetTarget(out cell)`、`IPathPlanner.TryBuild(start, goal, path)`、`IPathExecutor.Run(path)` |
| `Pipeline/Movement/TargetSources.cs` | 阶段一的两个实现，依赖由构造函数注入（map / self / team / 步数委托），距离一律用 `MapManager.Manhattan` | `ApproachNearestEnemy(map, self, team)`：走向最近的非己方，落在它四周离自己最近的空格；`FleeNearestEnemy(map, self, team, stepsLeft)`：只在**本回合走得到**的格子里（曼哈顿距离 ≤ 本次 `stepsLeft()` 的返回；返回 0 或负数视为不限）挑离最近敌方最远的格 |
| `Managers/BattleManager.cs` | **战场管理：统一负责加载 / 初始化 / 清场**。按 Inspector 里配的路径加载 `mapPath` / `turnPath` / `entityPaths`（路径见"场景"一节），把加载出来的对象挂到 `mapPos` 下，再依次：地图 `entities` 填好 → `MapManager.Init()` 摆位建索引 → 每个 `Entity.Init()` → 回合 `actors` 填好 → `TurnManager.Init(TurnInitData)`；只装不打 | `void BuildBattle()`（加载 + 初始化，开头先清场所以可反复调）、`void StartBattle()`（转发给回合管理器）、`void RebuildBattle()`（= 清场重来）、`void ClearBattle()`（停手 + 销毁加载出来的地图/实体/回合控制器 + 清缓存） |
| `Components/Health.cs` | 生命值 + 阵营，死亡时 `SetActive(false)`（不再打日志） | `float maxHealth`、`int team`、`float Current`、`bool IsDead`、`TakeDamage(float)` |
| `Managers/MapManager.cs` | 网格 + **两份地图数据**（`int[,] cells` 格子→uuid 二维图、`Dictionary<int,Vector2Int> positions` uuid→位置，另有 `byUuid` uuid→实体），外部只读；`TryMove`/`CancelMove`/`SyncOccupied` 负责维护 | `Init()`、`Build()`、`SyncOccupied()`（重建 + uuid 查重）、`ResetEntities()`、`UuidAt(cell)`、`EntityAt(cell)`、`EntitiesAt(cells)`、`EntityOf(uuid)`、`TryGetCell(uuid, out cell)`、`IsOccupied`、`CanEnter`、`InBounds`、`static Manhattan(a, b)`、`CellToWorld(col,row)` / `CellToWorld(cell)`、`WorldToCell`、`TryMove(from, step, out to)`、`CancelMove(from, to)`、`FindPath(from, to, path)`（四方向等权 BFS）、`SelfCheck()` / `SelfCheckPath()`（右键菜单自检）、常量 `Empty=0` / `Unknown=-1` |
| `Managers/TurnManager.cs` | 回合管理。数据：战斗实体列表 `actors` / 行动栈 `ActionStack`（每回合按先攻重建）/ 回合状态 `State`；每回合逐个出栈行动（`yield return actor.TakeTurnRoutine()`），跑满 `totalRounds` 结束 | `List<Entity> actors`、`List<Entity> ActionStack`、`TurnState State`（Idle / Running / Finished）、`int CurrentRound`、`Entity CurrentActor`、`int StackLeft`、`bool IsFinished`、`void Init(TurnInitData)`、`TurnInitData`（actors / totalRounds / turnDelay）、`void StartBattle()`、`void StopBattle()`、`void BuildActionStack()`、`Entity PopNext()`、`int totalRounds`、`float turnDelay`、`bool autoStart` |
| `Entity.cs` | 实体：组件的统一入口 + **对外唯一门面**（回合 / 地图 / UI 只认 Entity）；身份、先攻、回合步数、管线引用由自己持有，生命值与阵营归 `Health`（只转发），**技能由管线组件承担**（不再单独存技能数据） | `int Uuid`、`int initiative`（先攻）、`int moveSteps`、`float Hp` / `MaxHp`、`bool IsDead`、`int Team`、`void TakeDamage(float)`、`void Init(EntityInitData)`、`EntityInitData`（uuid / initiative / moveSteps / map / moveSource / attackType）、`AutoPilot Pilot`、`Health Health`、`Attacker Attacker`、`TargetSourceType SourceType { get; set; }`（转发给 AutoPilot）、`IEnumerator TakeTurnRoutine()`（攻击→移动→攻击，直接调 `Attacker.RunPipeline()`）；`Init` 把地图发给 AutoPilot **和 Attacker**（两条管线都重建，不依赖组件自己 `FindObjectOfType`） |
| `Pipeline/Movement/PathPipelineFactory.cs` | `TargetSourceType` 枚举（ApproachNearestEnemy / FleeNearestEnemy）+ 静态工厂：按枚举造阶段一、统一造阶段二/三 | `CreateSource(type, map, self, team, stepsLeft)`（`Func<int>` 步数委托，给"远离"用）、`CreatePlanner(map)`、`CreateExecutor(map, self, speed)`（`MoverPathExecutor`，自己改坐标）、`Wire(pilot, type, map, self, team, stepsLeft, speed)` |
| `Pipeline/Attack/AttackPipeline.cs` | 攻击管线三段接口（**都不依赖技能对象**，范围/目标数/伤害各自带）+ 实现：`CooldownCastCheck(cooldown)`（活着+冷却，顺带实现 `ICooldown`）、`MeleeTargetFinder(map)`（范围 1、1 目标）、`RangedTargetFinder(map)`（范围 3、1 目标）、`TargetPicker.Pick(map, caster, range, count, targets)`（两个 finder 共用的挑选逻辑）、`DamageCaster(damage)` | `ICastCheck.CanCast(caster)`、`ITargetFinder.TryFindTargets(caster, targets)`、`ISkillCaster.Cast(caster, targets)`、`ICooldown.StartCooldown()/TickTurn()` |
| `Pipeline/Attack/AttackPipelineFactory.cs` | `AttackType` 枚举（Melee=近战范围1 / Ranged=远程范围3）+ 静态工厂：按枚举造阶段二、统一造阶段一/三，和移动管线的 `PathPipelineFactory` 一个套路 | `CreateCastCheck(cooldown)`、`CreateTargetFinder(type, map)`、`CreateCaster(damage)`、`Wire(attacker, type, map, cooldown, damage)` |
| `Pipeline/Attack/Attacker.cs` | 攻击管线编排（挂在实体上，`[RequireComponent(typeof(Entity))]`）：阶段一 → 二 → 三；`Awake`/`Build()` 调 `AttackPipelineFactory.Wire` 按攻击类型装配三段，属性可替换；含 `[ContextMenu]` 手动跑一次 | `AttackType attackType`（枚举定义在 AttackPipelineFactory.cs）、`AttackType Type { get; set; }`、`float damage`、`int cooldown`、`ICastCheck CastCheck { get; set; }`、`ITargetFinder TargetFinder { get; set; }`、`ISkillCaster Caster { get; set; }`、`void Build()`、`bool RunPipeline()`、`void TickTurn()`、`List<Entity> targets` |

## 场景（Assets/Scenes/SampleScene.unity）

场景本体只剩一个 `BattleManager` 和一个空 `map_pos`（另有 Directional Light / Main Camera / EventSystem）；
**战斗对象全部在运行时由 `BattleManager` 从预制体加载**，所以改数值要改预制体，不是场景。

- `BattleManager`（GO `321782083`）：`mapPath` = `Assets/prefab/map1.prefab`、`turnPath` = `Assets/prefab/turn1.prefab`、
  `entityPaths` = [`Assets/prefab/Melee.prefab`, `Assets/prefab/Ranger.prefab`]（顺序对应地图的 `spawnPoints`）、
  `mapPos` = `map_pos`
- `map_pos`（GO `308726978`）：只有 Transform，位置 (0,0,0)；加载出来的地图/实体/回合控制器都挂它下面（`mapPos` 留空就放场景根）
- 场景检查：`python _validate_scene.py`（现在 21 个块；查 fileID 引用、组件归属、父子关系、SceneRoots、缩进）
- 预制体（都在 `Assets/prefab/`，手工维护）：
  - `map1.prefab`：MeshFilter / MeshRenderer / MeshCollider + `MapManager`（`cellSize` 1；`spawnPoints` = [(1,1), (2,2)]；
    `entities` 是 2 个**空槽**——跨对象引用进不了预制体，由 `BattleManager` 运行时填）
  - `Melee.prefab`：Entity(uuid 2, 先攻 44, moveSteps 4) + AutoPilot(靠近, speed 5) + Attacker(近战 attackType 0, 伤害 20, 冷却 1) + Health(team 0, 50)
  - `Ranger.prefab`：Entity(uuid 1, 先攻 10, moveSteps 3) + AutoPilot(远离, speed 5) + Attacker(远程 attackType 1, 伤害 10, 冷却 1) + Health(team 1, 50)
  - `turn1.prefab`：`TurnManager`（`totalRounds` 5、`turnDelay` 0.2、`autoStart` 开；`actors` 也是 2 个空槽，由 `BattleManager` 填）
  - 四个预制体里的 `map` 字段全是空的：运行时 `BattleManager` 把地图实例发给 `Entity`，`Entity.Init` 再转给 AutoPilot / Attacker
- 编辑器工具 `Assets/Editor/SceneAutoReload.cs`：磁盘上的 `.unity` 一变就自动重新加载当前场景（内存里未保存的版本先另存到 `Temp/编辑器未保存版本_*.unity`）；菜单 `Tools/场景以磁盘为准` 开关（默认开）、`Tools/重新加载当前场景（以磁盘为准）` 手动触发；播放中不动场景
- `Assets/Editor/BattlePrefabExporter.cs` **已过期**：它按名字找场景里的 `Ranger` / `ground` / `TurnManager` 存成预制体，而场景里这些物体已经没了（会打"找不到"的警告）。预制体现在是手工维护的——要么删掉这个工具，要么把它改成按 `BattleManager` 的配置导出。`Temp/battle-prefabs.done` 标记还在，所以它不会自动跑

## 约定

- 注释、Tooltip 用中文；字段名用英文。
- **成员顺序**：公开成员在前、私有在后；公开部分按调用顺序排（数据/字段 → 对外门面属性 → 组件引用 → 行为方法），Unity 生命周期方法（Awake/Start/Update/OnDisable/OnDrawGizmosSelected…）与私有辅助函数放在最后。
- 只写被要求的功能：不加接口/工厂/配置项，不加脚手架。故意砍掉的东西在回复里说明"跳过了 X，需要 Y 时再加"，不预先实现。
- 用 `#` 对 `Vector2Int` 的格子坐标：`.x` = 列（世界 x 方向），`.y` = 行（世界 z 方向），**不是世界高度**。
- 世界坐标用 `Vector3`，地面用 `Vector2` 存 `(x, z)`；不要用 Vector2 直接赋给 `transform.position`（会把 y/z 清 0）。
- 管线三段（移动：`ITargetSource` / `IPathPlanner` / `IPathExecutor`；攻击：`ICastCheck` / `ITargetFinder` / `ISkillCaster`）的依赖都走**构造函数注入**，接口只收"这次要处理什么"（起点/终点/路径；施法者/目标列表）；不要往接口里塞 AutoPilot / MapManager / Entity。
- **地图数据**：格子→uuid 二维图与 uuid→位置/实体 字典都由 `MapManager` 维护，外部只读查询（`UuidAt` / `EntityAt` / `EntitiesAt` / `EntityOf` / `TryGetCell`）；要改只能走 `TryMove` / `CancelMove` / `SyncOccupied`。
- **数据所有权**：一份数据只有一个所有者。身份(uuid) / 先攻 / 回合步数 / 管线类型 归 `Entity`；生命值 + 阵营归 `Health`；本回合可走步数（`AutoPilot.ApplySteps`，只有"远离"挑落点用）、动画中（`AutoPilot.IsFollowing`）、冷却剩余（`Attacker`）属于各自组件的运行期状态。`Entity` 是对外唯一门面（`Hp` / `MaxHp` / `IsDead` / `Team` / `TakeDamage()` 转发），**不要把组件自己的属性搬进 Entity**（会变成两份真相 + 组件不能单独工作）。
- `Health` 是 2D/3D 无关的，其余脚本的维度假设见上表。
- 需要可视化的逻辑（如网格划分）用 `OnDrawGizmosSelected` 画出来核对，不写单元测试。

## 已知缺口（用户明确跳过的）

- 移动执行（原 `ObjectMover`，现已并入阶段三 `MoverPathExecutor`）：格子移动是四方向直线插值——无斜向、无转向、无绕障碍；**只按 `speed` 插值、不拦步数**（行动约束由阶段一/二决定，见 `AutoPilot.ApplySteps`）；`TryMove` 通过时起点的占用就放开了，所以动画中起点是空的（要"动画中两侧都占住"就加个 `Arrive(from)`，到达时再放开起点）；走到一半被 `AutoPilot.Stop()` / `OnDisable` 打断时，靠 `map.SyncOccupied()` 按坐标重建占用数据对齐（不是精确回退到起点）；`MapManager.TryMove` 要求**起点也在网格内**（走到地图外的物体会拒绝移动）
- `MapManager`：地图数据只按 `entities` 列表重建（不在列表里的实体会走、格子记 `Unknown`，索引里查不到）；`entities` 被搬动/销毁后要自己调 `SyncOccupied()` 对齐；寻路是 `MapManager.FindPath`（四方向、每格等权 BFS，`BfsPathPlanner` 只是薄壳；A* / 加权代价没做）；无地形/障碍数据
- `AutoPilot`：管线只在被调用时跑一次（不会周期性重算/自动追人）、路径算完不重算（中途被挡就放弃）、BFS 的目标格必须可进入（站着人的格子不能当终点）；三段装配由 `AutoPilot.Build()` 自己完成（`Entity.Init` 只把地图发下去）
- `TargetSources`：目标从 `MapManager.entities`（地图就是单位注册表）里找、按曼哈顿距离挑最近——**不在 map 实体列表里的单位不会被当成目标**；"靠近"落地成"站到敌人四周离自己最近的空格"（敌人那格进不去，**不按步数裁剪**——目标在预算外就这回合走一段、下回合接着走），曼哈顿距离 ≤ 1 视为已贴身、这次不产生目标；"远离"只在本回合步数可达的菱形里挑最远格（地图没有障碍时等价于可达，以后有地形要换成按步数上限做 BFS 洪泛）
- `Health.team`：阵营就是个 int，没有仇恨表/友军保护
- `TurnManager`：行动内容写死在 `Entity.TakeTurnRoutine()` 里（固定 攻击-移动-攻击，没做成可配置的行动序列；换顺序就改它）；状态只能轮询 `State` / `IsFinished`，没有回合开始/结束事件；行动栈每回合重建一次，**回合中不重排**（先攻变了要等下回合）；先攻相同时按列表顺序而不掷骰；没有"跳过/延后/守卫"这类规则
- `Entity` / `PathPipelineFactory`：先攻只有 `Entity.initiative` 一份（`TurnManager` 的排序和行动都走 Entity）；`moveSteps` 仍是 Entity 的数据，靠 `AutoPilot.ApplySteps(moveSteps)` 存进 AutoPilot（只有"远离"挑落点时当预算，阶段三不扣步数）；移动管线的装配在 `AutoPilot.Build()`（`Entity.Init` 只把地图发下去再调它），每调一次就重建三段（正常运行中重复调不会打断正在走的协程）
- `Attacker`：已接进回合（`Entity.TakeTurnRoutine()` 里攻击-移动-攻击，回合开始会 `TickTurn()` 推进冷却）；目标获取从 `MapManager.entities` 里找，只认"挂了 `Entity` 且有 `Health`"的单位、按曼哈顿距离排序；`DamageCaster` 只扣血（没有击退/buff/动画表现）；远程没有视线/弹道判定（只看格子距离）；近战/远程的范围是 `AttackPipeline.cs` 里的常量（现在 1 / 3，改小过一次），`AttackPipelineFactory` 只按枚举选实现（碰触体那套 `Attack` 与 `SkillManager` 都已删除，攻击只有攻击管线一条路）
- 预制体：`Assets/prefab/*.prefab` 由 `BattleManager` 在运行时实例化（场景里只有 BattleManager 和 map_pos）；跨对象引用进不了预制体，所以 `map` / `entities` / `actors` 这些槽由 `BattleManager` 填（见「场景」一节）；预制体目前是**手工维护**的，改了结构要自己存（`BattlePrefabExporter` 已过期，见「场景」一节）
- `Entity.uuid`：在预制体上手填（`Ranger.prefab` uuid 1、`Melee.prefab` uuid 2）；`SyncOccupied()` 现在会查重但**只警告不修正**（重复时后者这次被跳过），uuid 分配器还没做
- `BattleManager`：预制体靠 Inspector 里手填的**路径字符串**加载——`LoadPrefab` 先按文件名走 `Resources.Load`，编辑器里再退回 `AssetDatabase.LoadAssetAtPath`，所以现在不改目录就能跑，但**打包后必须把预制体放进某个 `Resources` 目录**；清场用 `Destroy`（当帧末尾才真销毁，重开那一帧新旧对象并存，靠显式发地图引用避开旧地图）；实体初始位置完全依赖地图预制体上的 `spawnPoints`（数量不够的实体留在被实例化的位置）；`BuildBattle()` 只装不打，开打靠 turn 预制体的 `autoStart` 或外部调 `StartBattle()`；目前没有任何 UI / 快捷键触发 `StartBattle` / `RebuildBattle`（NavTest 那套测试按钮已随场景重构删掉）
- 脚本都还没在播放模式下跑过（两个角色都没有 Rigidbody，移动是直接写 `transform.position`）

## 按用户给的结构做的重构（进度）

- **P1 实体数据** ✅ `Entity`：uuid / 先攻(`initiative`) / 回合步数 / 生命值门面（`Hp`/`TakeDamage`，数据仍归 `Health`）/ `Init(EntityInitData)`；技能由管线组件承担，不单独存
- **P2 地图数据** ✅ `MapManager`：`int[,] cells` 格子→uuid 二维图 + `Dictionary<int,Vector2Int>` uuid→位置（+ uuid→实体），查询 API 与 uuid 查重
- **P3 寻路** ✅ 搬进 `MapManager.FindPath`，`BfsPathPlanner` 退成薄壳
- **P4 回合** ✅ `TurnManager`：行动栈 `ActionStack`（每回合按先攻重建，`PopNext()` 出栈）+ `TurnState` 状态 + `Init(TurnInitData)`
- **P5 移动管线「接近」** — 用户确认是笔误（现有 靠近/远离 即全部），未做
- **P6 收尾** ✅ 场景 / 预制体同步 + 本文档校对
- **额外清理** ✅ 删掉旧技能系统的残留：`SkillManager`、碰触体 `Attack` 与 4 个 `skill_*` 子物体（攻击只剩攻击管线一条路）；预制体等 Unity 重载后由 `BattlePrefabExporter` 重新导出
- **额外清理** ✅ 删掉 `ObjectMover` 组件：移动逻辑坍缩进阶段三 `MoverPathExecutor.Run()`（直接改实体坐标），速度改挂 `AutoPilot.speed`，本回合步数留在 `AutoPilot.ApplySteps` 供"远离"当预算，`Entity.Mover` 一并移除
- **额外清理** ✅ 新增 `BattleManager`（战场统一管理）：把「加载 → 初始化」包成 `BuildBattle()`，另加 `ClearBattle()` 清场、`RebuildBattle()` = 清场 + 重新加载；`Entity.Init` 现在把地图也发给 `Attacker`
- 未做：Map 配置对象（用户说暂时不用）

## 版本管理

- 仓库：https://github.com/Gede1416/GamePlayTest.git，remote 名 `origin`，分支 `main`
- **每次改动都要单独提交一次**，提交信息按 `COMMIT_CONVENTION.md` 写：`<type>(<scope>): <中文描述>`，type 用 feat/fix/docs/style/refactor/perf/test/build/ci/chore/revert，scope 参考该文件里的模块表，一个提交只做一件事（用户靠提交记录回看修改）
- `.gitignore` 已排除 `Library/`、`Temp/`、`Logs/`、`obj/`、`UserSettings/` 和 Unity 生成的 `*.csproj` / `*.sln`
- 提交脚本改动前的自检：复制 `Assembly-CSharp.csproj` 成临时 csproj，把所有 `<Compile Include="..." />` 换成一条 `<Compile Include="Assets\**\*.cs" />` 再 `dotnet build`（Unity 生成的清单会过期，文件搬过目录后尤其明显；这份 csproj 自带 UnityEditor 程序集引用，编辑器脚本一并覆盖）；0 错误 0 警告后再提交，临时 csproj / bin / obj 删掉
- 提交场景改动前的自检：`python _validate_scene.py`
- **用户已授权（2026-09-17）：`add` / `commit` / `push` 直接做，不必逐次询问。**每完成一处改动就自己提交、自己推，不用先问要不要提交。
- `commit` 不受沙箱影响；**`push` 需要放宽沙箱**（凭据管理器要创建管道，受限模式下报 `couldn't create signal pipe, Win32 error 5` + `could not read Username`），所以推的时候直接带 `sandbox_permissions: danger-full-access`（这是已获授权的常规操作，不是新请求）；只有推送仍然失败（如网络不通）才把命令交给用户在终端里跑一次。
- 本仓库本地设了 `http.sslBackend=openssl`：这台机器上 Windows schannel 握不上手（`SEC_E_NO_CREDENTIALS`）
- `SampleScene.unity.bak` 现在被 `.gitignore` 排除，git 已经能回看历史，不再需要它

## 改动前请注意

`Assets/Scripts/` 下的文件用户会直接改（例如 `Health`、`MapManager`、`ObjectMover`（已删）都曾被手改过）。
**改之前先读当前文件，只做最小改动，不要整文件重写覆盖用户的手改。**

`Assets/Scenes/SampleScene.unity` 也可以直接改：Unity 那边的 `SceneAutoReload` 会以磁盘为准自动重载（见"场景"一节的编辑器工具），所以**不需要再提醒用户先保存或手动重载**；只在编辑器里手工改场景时才会出现"编辑器版本覆盖磁盘"的情况（那时丢掉的编辑器版本会备份到 `Temp/`）。
