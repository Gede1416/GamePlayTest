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
- `Assets/Scripts/Managers/`：管理器（`BattleManager` / `MapManager` / `TurnManager`）+ 预制体加载工具（`PrefabLoader`）
- `Assets/Scripts/Components/`：功能组件（`Health`）
- `Assets/Scripts/Events/`：事件（`EventPipeline` 泛型收发 + `BattleEvents` 里各条消息的数据类）
- `Assets/Scripts/UI/`：战斗界面（`BattleUIManager` 总管 + `HealthBar` 血条 + `DamagePopup` 伤害数字）
- `Assets/Scripts/Pipeline/Movement/`：移动管线（`AutoPilot` / `PathPipeline` / `TargetSources` / `PathPipelineFactory`）
- `Assets/Scripts/Pipeline/Attack/`：攻击管线（`Attacker` / `AttackPipeline` / `AttackPipelineFactory`）
- `Assets/Editor/`：编辑器工具（`SceneAutoReload`）

| 文件 | 职责 | 对外接口 |
|---|---|---|
| `Pipeline/Movement/AutoPilot.cs` | 寻路管线编排：阶段一 → 阶段二 → 阶段三；**装配在本组件内**（`Build()` 调 `PathPipelineFactory.Wire`，按枚举造阶段一），和攻击管线的 `Attacker` 一个套路；阶段三自己改坐标，**没有独立的移动组件**；本回合可走步数也存在这里，只有"远离"挑落点时读它 | `TargetSourceType targetSourceType`（枚举定义在 PathPipelineFactory.cs）、`MapManager map`、`float speed`（世界单位/秒）、`TargetSourceType SourceType { get; set; }`、`void Build()`、`void ApplySteps(int steps)`、`ITargetSource TargetSource { get; set; }`、`IPathPlanner Planner { get; set; }`、`IPathExecutor Executor { get; set; }`、`bool RunPipeline()`、`bool MoveTo(Vector3)`、`bool MoveTo(Vector2Int)`、`List<Vector2Int> path`、`bool IsFollowing`、`int Team`（取自自己的 `Health.team`）、`void Init()`（找 Health + 装配三段，由 `Entity.Init` 调）、`void Clear()`（清理：停下 + 让地图数据跟坐标对齐 + 清空路径，由 `Entity.Clear` 调） |
| `Pipeline/Movement/PathPipeline.cs` | 三个阶段接口（依赖一律构造函数注入）+ `BfsPathPlanner(map)`（**薄壳，真正寻路在 `MapManager.FindPath`**）+ `MoverPathExecutor(map, self, speed)`（**自己把实体逐格插值挪过去**，落点与占用问 `MapManager.TryMove`，不再有 ObjectMover 组件） | `ITargetSource.TryGetTarget(out cell)`、`IPathPlanner.TryBuild(start, goal, path)`、`IPathExecutor.Run(path)` |
| `Pipeline/Movement/TargetSources.cs` | 阶段一的两个实现，依赖由构造函数注入（map / self / team / 步数委托），距离一律用 `MapManager.Manhattan` | `ApproachNearestEnemy(map, self, team)`：走向最近的非己方，落在它四周离自己最近的空格；`FleeNearestEnemy(map, self, team, stepsLeft)`：只在**本回合走得到**的格子里（曼哈顿距离 ≤ 本次 `stepsLeft()` 的返回；返回 0 或负数视为不限）挑离最近敌方最远的格 |
| `Managers/BattleManager.cs` | **战场管理**。`BuildBattle()` 按三步拆开：**① 清理资源 `ClearBattle()` → ② 资源加载 + 绑定 + 组件缓存 `LoadBattle()` → ③ 初始化 `InitBattle()`**。`LoadBattle()` 按 Inspector 里配的路径（`mapPath` / `turnPath` / `entityPaths`，见"场景"一节）加载并实例化到 `mapPos` 下，缓存 `MapManager` / `TurnManager` / `List<Entity>`，同时把地图引用发给实体、把实体物体列表接进地图 `entities`（顺序对应 `spawnPoints`）；`InitBattle()` 先订死亡消息，再依次 `MapManager.Init()` → 每个 `Entity.Init()` → `TurnManager.Init(TurnInitData)` → 按 `autoStart` 决定开打。**收到 `EntityDied` 时统计还活着的阵营：只剩一个（或全灭）就 `EndGame()`——让回合停手 + 发 `BattleEnded` 消息** | `void BuildBattle()`（三步走一遍，可反复调）、`void StartBattle()`（转发给回合管理器）、`void RebuildBattle()`（= 清场重来）、`void ClearBattle()`（步骤①：按 回合 → 实体 → 地图 调各组件的 `Clear()`，再销毁加载出来的对象并清缓存）、`bool LoadBattle()`（步骤②，私有，关键对象加载不到返回 false）、`void InitBattle()`（步骤③，私有，先 `uiManager.Init()` 再逐个实体 `Init()` 并发 `EntitySpawnedEvent`）、`void OnEntityDied(EntityDiedEvent)`（私有，收死亡消息）、`void EndGame(int winnerTeam)`（私有，结束游戏 + 发结束消息，-1 = 打平） |
| `Events/EventPipeline.cs` | **泛型事件管线**：按事件**类型**（`Dictionary<Type, Delegate>`）收发，加新事件不用改这个类 | `void Send<T>(T e)`、`void Subscribe<T>(Action<T>)`、`void Unsubscribe<T>(Action<T>)`、`void Clear()`（清场时清订阅） |
| `Events/BattleEvents.cs` | 各条消息的数据类（类型本身就是频道）：`TurnChangedEvent(round, totalRounds)`、`DamageEvent(target, amount)`、`HealthChangedEvent(entity, current, max)`、`EntityDiedEvent(entity, team)`、`EntitySpawnedEvent(entity)`、`BattleEndedEvent(winnerTeam)` | 每条消息只读字段 `round` / `totalRounds`、`target` / `amount`、`entity` / `current` / `max`、`entity` / `team`、`entity`、`winnerTeam` |
| `UI/BattleUIManager.cs` | **战斗界面总管**：全靠事件驱动——回合数（`TurnChangedEvent` 写场景 Canvas 上的文字）、伤害数字（`DamageEvent` 在被打实体 UI 点位下**实例化预制体**）、胜方（`BattleEndedEvent`）、血条（`EntitySpawnedEvent` 时给实体**实例化血条预制体**）。界面元素本身都是场景 Canvas / 预制体，这里只加载、实例化、销毁，**不在代码里组装 UI** | `void Init()`（加载预制体 + 订事件，由 `BattleManager` 调）、`void Clear()`（退订 + 销毁实例化出来的血条 / 伤害数字）、`Text roundText` / `Text resultText`（场景 Canvas 下的两个文字）、`string healthBarPath` / `string damagePopupPath`（预制体路径） |
| `UI/HealthBar.cs` | 实体头顶的血条：外观在预制体里（世界空间 Canvas + 底 / 前景两张 `Image`），组件只做三件事——`Init(entity)` 绑实体、按比例改前景 `sizeDelta.x`、`LateUpdate` 贴住实体 UI 点位并转向相机；数值靠 `HealthChangedEvent` 刷新 | `void Init(Entity entity)`（由 `BattleUIManager` 实例化后调）、`void Clear()`（退订）、`Image fill` / `float fullWidth`（预制体里接） |
| `UI/DamagePopup.cs` | 伤害数字：外观在预制体里（世界空间 Canvas + 一个 `Text`），组件负责写数字、上飘淡出、转向相机，飘完自毁 | `void Init(float amount)`（由 `BattleUIManager` 实例化后调）、`Text label` / `float rise` / `float life` / `float startHeight`（预制体里接 / 调） |
| `Managers/PrefabLoader.cs` | 预制体加载（`BattleManager` 与 `BattleUIManager` 共用）：先按文件名走 `Resources`，编辑器里再退回按资源路径加载，加载不到报错并返回 null | `static GameObject Load(string path)` |
| `UI/HealthBar.cs` | 实体头顶的血条：绑 `Health` 取初值，数值靠 `HealthChangedEvent` 刷新，`LateUpdate` 里贴住实体 UI 点位并转向相机；底 + 前景两块 `SpriteRenderer`（1×1 白图，不用美术资源） | `void Init(Entity entity, float width, float height)`（由 `BattleUIManager` 创建时调）、`void Clear()`（退订） |
| `UI/DamagePopup.cs` | 伤害数字：挂在被打实体的 UI 点位下，一边上飘一边淡出，飘完自己销毁 | `void Init(float amount, float rise, float life)`（由 `BattleUIManager` 创建时调） |
| `Components/Health.cs` | 生命值 + 阵营；**死亡逻辑单独在 `Die()`**（停用自己 + 发 `EntityDiedEvent`），扣血流程只把血扣到 0 再交给它；对外发 `DamageEvent`（伤害数字）/ `HealthChangedEvent`（血条）/ `EntityDiedEvent`（阵亡结算）；初始化由 `Entity.Init` 调 `Init()` | `float maxHealth`、`int team`、`float Current`、`bool IsDead`、`void Init()`（记住 owner + 把当前值补满）、`void Clear()`（清理：回到未初始化状态）、`TakeDamage(float)`（见底 → `Die()`）、`void Die()`（同一条命只走一次） |
| `Managers/MapManager.cs` | 网格 + **一份地图数据**：`int[,] cells` 格子→实体 uuid 的二维图（`Empty=0` 空 / `Unknown=-1` 占着但没登记 uuid）；**订阅死亡消息**：单位阵亡就放开它的格子。uuid→位置 / uuid→实体 的字典已删（当时没有任何消费者，留着只是多一份要同步的真相） | `Init()`（由 `BattleManager` 调：建网格 + 摆实体 + 订死亡消息）、`Clear()`（清理：退订 + 清空二维图与实体列表，保留 `spawnPoints` 配置）、`SyncOccupied()`（按坐标重建二维图 + uuid 查重）、`CanEnter(cell)`、`static Manhattan(a, b)`、`CellToWorld(col,row)` / `CellToWorld(cell)`、`WorldToCell(pos)`、`TryMove(from, step, out to)`（裁决 + 挪 uuid）、`FindPath(from, to, path)`（四方向等权 BFS）、`int Cols` / `int Rows`、常量 `Empty=0` / `Unknown=-1`；**私有**：`Build` / `ResetEntities` / `ReleaseEntity`（死亡消息触发，按坐标放开格子）/ `SelfCheck` / `SelfCheckPath`（后两个右键组件菜单能跑）；**二维图存取包装**：`HasGrid`（建好没）、`UuidAt(col,row)` / `UuidAt(cell)`（读）、`SetUuid(cell,uuid)`（写）、`ClearCells()`（整张清空）、`InBounds(cell)` |
| `Managers/TurnManager.cs` | 回合管理（**只管开始，停止/复位统一由 `BattleManager` 调它的 `Clear()`**）。数据：战斗实体列表 `actors` / 行动栈 `ActionStack`（每回合按先攻重建）/ 回合状态 `State`；每回合逐个出栈行动（`yield return actor.TakeTurnRoutine()`），跑满 `totalRounds` 结束 | `List<Entity> actors`、`List<Entity> ActionStack`、`TurnState State`（Idle / Running / Finished）、`int CurrentRound`、`Entity CurrentActor`、`int StackLeft`、`bool IsFinished`、`void Init(TurnInitData)`（由 `BattleManager` 调）、`TurnInitData`（actors / totalRounds / turnDelay）、`void StartBattle()`（**只管开始**，已在跑就直接返回，不会自己停）、`void Clear()`（清理：停协程 + 清空行动栈与参战列表 + 状态归零，由 `BattleManager.ClearBattle` 调）、每回合开头发 `TurnChangedEvent`（界面刷新回合数）、`int totalRounds`（`BuildActionStack` / `PopNext` 已收成私有，只由内部回合循环用）、`float turnDelay`、`bool autoStart` |
| `Entity.cs` | 实体：组件的统一入口 + **对外唯一门面**（回合 / 地图 / UI 只认 Entity），也是身上组件的初始化分发点（Health → AutoPilot → Attacker）；身份、先攻、回合步数、管线引用由自己持有，生命值与阵营归 `Health`（只转发），**技能由管线组件承担**（不再单独存技能数据） | `int Uuid`、`int initiative`（先攻）、`int moveSteps`、`float Hp` / `MaxHp`、`bool IsDead`、`int Team`、`void TakeDamage(float)`、`void Init(EntityInitData)`、`EntityInitData`（uuid / initiative / moveSteps / map / moveSource / attackType）、`AutoPilot Pilot`、`Health Health`、`Attacker Attacker`、`Transform uiPoint`（UI 点位，血条 / 伤害数字挂它下面；留空时 Init 在头顶自动建一个）、`TargetSourceType SourceType { get; set; }`（转发给 AutoPilot）、`IEnumerator TakeTurnRoutine()`（攻击→移动→攻击，直接调 `Attacker.RunPipeline()`）；`Init` 把地图发给 Health / AutoPilot / Attacker 并逐个调它们的 `Init()`（不依赖组件自己的生命周期，也不用 `FindObjectOfType`）、`void Clear()`（反向转发清理：Attacker → AutoPilot → Health） |
| `Pipeline/Movement/PathPipelineFactory.cs` | `TargetSourceType` 枚举（ApproachNearestEnemy / FleeNearestEnemy）+ 静态工厂：按枚举造阶段一、统一造阶段二/三 | `CreateSource(type, map, self, team, stepsLeft)`（`Func<int>` 步数委托，给"远离"用）、`CreatePlanner(map)`、`CreateExecutor(map, self, speed)`（`MoverPathExecutor`，自己改坐标）、`Wire(pilot, type, map, self, team, stepsLeft, speed)` |
| `Pipeline/Attack/AttackPipeline.cs` | 攻击管线三段接口（**都不依赖技能对象**，范围/目标数/伤害各自带）+ 实现：`CooldownCastCheck(cooldown)`（活着+冷却，顺带实现 `ICooldown`）、`MeleeTargetFinder(map)`（范围 1、1 目标）、`RangedTargetFinder(map)`（范围 3、1 目标）、`TargetPicker.Pick(map, caster, range, count, targets)`（两个 finder 共用的挑选逻辑）、`DamageCaster(damage)`（扣血走 `Entity.TakeDamage` 这个门面，不直接碰 `Health`） | `ICastCheck.CanCast(caster)`、`ITargetFinder.TryFindTargets(caster, targets)`、`ISkillCaster.Cast(caster, targets)`、`ICooldown.StartCooldown()/TickTurn()` |
| `Pipeline/Attack/AttackPipelineFactory.cs` | `AttackType` 枚举（Melee=近战范围1 / Ranged=远程范围3）+ 静态工厂：按枚举造阶段二、统一造阶段一/三，和移动管线的 `PathPipelineFactory` 一个套路 | `CreateCastCheck(cooldown)`、`CreateTargetFinder(type, map)`、`CreateCaster(damage)`、`Wire(attacker, type, map, cooldown, damage)` |
| `Pipeline/Attack/Attacker.cs` | 攻击管线编排（挂在实体上，`[RequireComponent(typeof(Entity))]`）：阶段一 → 二 → 三；`Init()`/`Build()` 调 `AttackPipelineFactory.Wire` 按攻击类型装配三段，属性可替换；含 `[ContextMenu]` 手动跑一次 | `AttackType attackType`（枚举定义在 AttackPipelineFactory.cs）、`AttackType Type { get; set; }`、`float damage`、`int cooldown`、`ICastCheck CastCheck { get; set; }`、`ITargetFinder TargetFinder { get; set; }`、`ISkillCaster Caster { get; set; }`、`void Init()`（找 Entity + 装配三段，由 `Entity.Init` 调）、`void Build()`、`bool RunPipeline()`、`void TickTurn()`、`void Clear()`（清理：清空目标 + 放开三段引用）、`List<Entity> targets` |

## 场景（Assets/Scenes/SampleScene.unity）

场景本体只剩一个 `BattleManager` 和一个空 `map_pos`（另有 Directional Light / Main Camera / EventSystem）；
**战斗对象全部在运行时由 `BattleManager` 从预制体加载**，所以改数值要改预制体，不是场景。

- `BattleManager`（GO `321782083`）：`mapPath` = `Assets/prefab/map1.prefab`、`turnPath` = `Assets/prefab/turn1.prefab`、
  `entityPaths` = [`Assets/prefab/Melee.prefab`, `Assets/prefab/Ranger.prefab`]（顺序对应地图的 `spawnPoints`）、
  `mapPos` = `map_pos`、`uiManager` = 同一个物体上的 `BattleUIManager`（`fontSize` 48、`textScale` 0.08、血条 0.8×0.12、伤害上飘 1 / 0.9 秒）
- `map_pos`（GO `308726978`）：只有 Transform，位置 (0,0,0)；加载出来的地图/实体/回合控制器都挂它下面（`mapPos` 留空就放场景根）
- `BattleCanvas`（GO `321782090`）：Screen Space - Overlay 的 Canvas（+ CanvasScaler 1920×1080），两个文字子物体——`RoundText`（左上角，`Round x/y`）与 `ResultText`（屏幕中央，胜方 / Draw），都接到 `BattleUIManager` 上
- 场景检查：`python _validate_scene.py`（现在 34 个块；查 fileID 引用、组件归属、父子关系、SceneRoots、缩进）
- 预制体（都在 `Assets/prefab/`，手工维护）：
  - `map1.prefab`：MeshFilter / MeshRenderer / MeshCollider + `MapManager`（`cellSize` 1；`spawnPoints` = [(1,1), (2,2)]；
    `entities` 是 2 个**空槽**——跨对象引用进不了预制体，由 `BattleManager` 运行时填）
  - `Melee.prefab`：Entity(uuid 2, 先攻 44, moveSteps 4) + AutoPilot(靠近, speed 5) + Attacker(近战 attackType 0, 伤害 20, 冷却 1) + Health(team 0, 50)
  - `Ranger.prefab`：Entity(uuid 1, 先攻 10, moveSteps 3) + AutoPilot(远离, speed 5) + Attacker(远程 attackType 1, 伤害 10, 冷却 1) + Health(team 1, 50)
  - `turn1.prefab`：`TurnManager`（`totalRounds` 5、`turnDelay` 0.2、`autoStart` 开 → `BattleManager` 初始化完就开打；`actors` 也是 2 个空槽，由 `BattleManager` 填）
  - `HealthBar.prefab`：世界空间 Canvas（scale 0.01 → 1.0×0.12 世界单位）+ `Back` / `Fill` 两张 Image（内置 UISprite，白图染色）+ `HealthBar` 组件（`fill` 接前景、`fullWidth` 100）
  - `DamagePopup.prefab`：世界空间 Canvas（scale 0.01 → 1.2×0.4 世界单位）+ `Label`（uGUI `Text`，40 号、居中、偏黄）+ `DamagePopup` 组件（`label` 接文字，上飘 1 / 存活 0.9 秒）
  - 四个预制体里的 `map` 字段全是空的：运行时 `BattleManager` 把地图实例发给 `Entity`，`Entity.Init` 再转给 AutoPilot / Attacker
- 编辑器工具 `Assets/Editor/SceneAutoReload.cs`：磁盘上的 `.unity` 一变就自动重新加载当前场景（内存里未保存的版本先另存到 `Temp/编辑器未保存版本_*.unity`）；菜单 `Tools/场景以磁盘为准` 开关（默认开）、`Tools/重新加载当前场景（以磁盘为准）` 手动触发；播放中不动场景

## 约定

- 注释、Tooltip 用中文；字段名用英文。
- **成员顺序**（每个类都照这个来，四段各自内部按调用顺序排）：
  1. **类属性**：字段与属性（序列化字段 → 常量 → 运行期状态字段 → 属性）
  2. **生命周期**：`Awake` / `Start` / `OnDisable` / `OnDrawGizmosSelected` 等 Unity 回调
  3. **公开方法**
  4. **私有方法**（含 `[ContextMenu]` 的调试方法）
  纯数据类（`EntityInitData` / `TurnInitData`）和静态工厂只有属性/方法，按同样的先后即可。
- **初始化统一走 `Init()`，由上级组件调用**：`BattleManager.Awake()`（唯一的入口）→ `MapManager.Init()` / `Entity.Init()` / `TurnManager.Init(TurnInitData)`；`Entity.Init()` 再把初始化发给身上的组件：`Health.Init()` → `AutoPilot.Init()`（找 Health + 装配三段 + `ApplySteps`）→ `Attacker.Init()`（找 Entity + 装配三段）。**子组件一律不写 `Awake` / `Start` 做初始化**（`AutoPilot.OnDisable` 只是收尾停手，不算初始化）；地图等依赖由上级先写进字段再调 `Init()`。谁忘了调 `Init()` 谁就不工作。
  **清理同样由上级发起**：每个组件都暴露 `Clear()`（TurnManager / MapManager / Entity / AutoPilot / Attacker / Health），`BattleManager.ClearBattle()` 按 回合 → 实体（Entity.Clear → Attacker → AutoPilot → Health）→ 地图 的顺序统一调，之后才销毁对象；组件自己不做收尾：`TurnManager.StartBattle()` 只管开始（已在跑就返回），不负责停。
- **可见性从紧**：只有**现在就有外部调用**的成员才写 `public`（其它类调用 / Unity 生命周期 / 编辑器菜单 `[ContextMenu]` / `[MenuItem]`）；只在自己类里用的收成 `private`（`[ContextMenu]` 能调私有方法，不用为了菜单放开）；暂时没人用的直接删，等外部真要用时再加一个包装方法向外公开。**例外**（用户点名要的对外入口，保持 `public`）：`BattleManager.BuildBattle` / `StartBattle` / `RebuildBattle` / `ClearBattle`、`Health.Die`、`EventPipeline` 的 `Send` / `Subscribe` / `Unsubscribe` / `Clear`。
- 只写被要求的功能：不加接口/工厂/配置项，不加脚手架。故意砍掉的东西在回复里说明"跳过了 X，需要 Y 时再加"，不预先实现。
- 用 `#` 对 `Vector2Int` 的格子坐标：`.x` = 列（世界 x 方向），`.y` = 行（世界 z 方向），**不是世界高度**。
- 世界坐标用 `Vector3`，地面用 `Vector2` 存 `(x, z)`；不要用 Vector2 直接赋给 `transform.position`（会把 y/z 清 0）。
- 管线三段（移动：`ITargetSource` / `IPathPlanner` / `IPathExecutor`；攻击：`ICastCheck` / `ITargetFinder` / `ISkillCaster`）的依赖都走**构造函数注入**，接口只收"这次要处理什么"（起点/终点/路径；施法者/目标列表）；不要往接口里塞 AutoPilot / MapManager / Entity。
- **地图数据只有一份**：`MapManager` 的 `int[,] cells`（格子→实体 uuid 二维图），外部只读；改数据只能走 `TryMove`（走一格，裁决 + 挪 uuid）或 `SyncOccupied`（按坐标整体重建），阵亡放开格子由内部的 `ReleaseEntity` 负责。类内部也不再散着 `cells[x, y]` 下标操作：读写一律走私有包装 `UuidAt(col,row)` / `UuidAt(cell)` / `SetUuid(cell,uuid)` / `ClearCells()`，判空走 `HasGrid`（裸下标只出现在这两个方法里），以后换存储方式只改这一组方法。要"某个 uuid 在哪格 / 是哪个实体"目前没有需求——**需要时再加一份索引或包装方法**，别提前留着两份真相。
- **数据所有权**：一份数据只有一个所有者。身份(uuid) / 先攻 / 回合步数 / 管线类型 归 `Entity`；生命值 + 阵营归 `Health`；本回合可走步数（`AutoPilot.ApplySteps`，只有"远离"挑落点用）、动画中（`AutoPilot.IsFollowing`）、冷却剩余（`Attacker`）属于各自组件的运行期状态。`Entity` 是对外唯一门面（`Hp` / `MaxHp` / `IsDead` / `Team` / `TakeDamage()` 转发），**不要把组件自己的属性搬进 Entity**（会变成两份真相 + 组件不能单独工作）。
- `Health` 是 2D/3D 无关的，其余脚本的维度假设见上表。
- 私有字段**不加下划线前缀**（`routine` / `cells` / `mapManager`，不是 `_routine`）。
- **属性标签横排一行**：同一个字段上的多个特性写在同一行，例如 `[Tooltip("攻击类型：近战 范围 1 / 远程 范围 3，都是 1 个目标")][SerializeField] AttackType attackType = AttackType.Melee;`（`[Header(...)]` 也照样接在同一行）。
- **代码分块用 `#region` / `#endregion`**：每个类里四段各一个 region（`属性` / `生命周期` / `公开方法` / `私有方法`），构造函数单独一个 `构造` region；`公开方法` 内部的语义子块（`查询` / `移动裁决` / `寻路` / `自检`）用**嵌套** region；region 与它包住的成员同缩进。方法体内部的注释块仍用 `// ---------- xxx ----------`（方法里不套 region）。只有一组公开静态方法的工厂类（`*PipelineFactory`）不用分块。
- **事件走泛型 `EventPipeline`**：`Subscribe<T>(处理函数)` / `Unsubscribe<T>(...)` / `Send(new XxxEvent(...))`，按类型分发（`Dictionary<Type, Delegate>`），**加新事件只要在 `BattleEvents.cs` 加一个数据类，管线本身一行都不用改**。现在的收发关系：
  - `TurnChangedEvent`：`TurnManager` 每回合开头发 → `BattleUIManager` 刷新回合数
  - `DamageEvent`：`Health.TakeDamage` 发 → `BattleUIManager` 在被打实体头顶飘伤害数字
  - `HealthChangedEvent`：`Health.Init` / `TakeDamage` 发 → `HealthBar` 刷新条子（血条自己还持有 `Health` 取初值）
  - `EntityDiedEvent`：`Health.Die` 发 → `BattleManager`（统计存活阵营 / 判胜负）+ `MapManager`（放开格子）
  - `EntitySpawnedEvent`：`BattleManager.InitBattle` 每个实体 Init 完发 → `BattleUIManager` 给它挂血条
  - `BattleEndedEvent`：`BattleManager.EndGame` 发 → `BattleUIManager` 显示胜方阵营
  订阅在各自的 `Init()` 里订、`Clear()` 里退订（`MapManager.Init` 是先退订再订，重复 Init 也不会订两遍），`BattleManager.ClearBattle()` 末尾再用 `EventPipeline.Clear()` 兜一道。
- 需要可视化的逻辑（如网格划分）用 `OnDrawGizmosSelected` 画出来核对，不写单元测试。

## 已知缺口（用户明确跳过的）

- 移动执行（原 `ObjectMover`，现已并入阶段三 `MoverPathExecutor`）：格子移动是四方向直线插值——无斜向、无转向、无绕障碍；**只按 `speed` 插值、不拦步数**（行动约束由阶段一/二决定，见 `AutoPilot.ApplySteps`）；`TryMove` 通过时起点的占用就放开了，所以动画中起点是空的（要"动画中两侧都占住"就加个 `Arrive(from)`，到达时再放开起点）；走到一半被 `AutoPilot.Clear()` / `OnDisable` 打断时，靠 `map.SyncOccupied()` 按坐标重建占用数据对齐（不是精确回退到起点）；`MapManager.TryMove` 要求**起点也在网格内**（走到地图外的物体会拒绝移动）
- `MapManager`：地图数据只按 `entities` 列表重建（不在列表里的实体会走、格子记 `Unknown`）；自检 `SelfCheck` 现在查的是"每个登记了 uuid 的单位在二维图里恰好占一格"；`entities` 被搬动/销毁后要自己调 `SyncOccupied()` 对齐；寻路是 `MapManager.FindPath`（四方向、每格等权 BFS，`BfsPathPlanner` 只是薄壳；A* / 加权代价没做）；无地形/障碍数据
- `AutoPilot`：管线只在被调用时跑一次（不会周期性重算/自动追人）、路径算完不重算（中途被挡就放弃）、BFS 的目标格必须可进入（站着人的格子不能当终点）；三段装配由 `AutoPilot.Build()` 自己完成（`Entity.Init` 只把地图发下去）
- `TargetSources`：目标从 `MapManager.entities`（地图就是单位注册表）里找、按曼哈顿距离挑最近——**不在 map 实体列表里的单位不会被当成目标**；"靠近"落地成"站到敌人四周离自己最近的空格"（敌人那格进不去，**不按步数裁剪**——目标在预算外就这回合走一段、下回合接着走），曼哈顿距离 ≤ 1 视为已贴身、这次不产生目标；"远离"只在本回合步数可达的菱形里挑最远格（地图没有障碍时等价于可达，以后有地形要换成按步数上限做 BFS 洪泛）
- `Health.team`：阵营就是个 int，没有仇恨表/友军保护；`Die()` 只做 `SetActive(false)` + 发消息，**放开格子交给 `MapManager`**——它收到死亡消息会 `ReleaseEntity()`（按坐标清掉二维图里的格子 + 把物体从 `entities` 里摘掉），所以阵亡单位不再挡路、也不会再被当成目标；尸体物体本身还在 `BattleManager` 的 `entities` 列表里（结算统计要用），清场时才销毁
- `TurnManager`：行动内容写死在 `Entity.TakeTurnRoutine()` 里（固定 攻击-移动-攻击，没做成可配置的行动序列；换顺序就改它）；状态只能轮询 `State` / `IsFinished`，没有回合开始/结束事件；行动栈每回合重建一次，**回合中不重排**（先攻变了要等下回合）；先攻相同时按列表顺序而不掷骰；没有"跳过/延后/守卫"这类规则
- `Entity` / `PathPipelineFactory`：先攻只有 `Entity.initiative` 一份（`TurnManager` 的排序和行动都走 Entity）；`moveSteps` 仍是 Entity 的数据，靠 `AutoPilot.ApplySteps(moveSteps)` 存进 AutoPilot（只有"远离"挑落点时当预算，阶段三不扣步数）；移动管线的装配在 `AutoPilot.Build()`（`Entity.Init` 只把地图发下去再调它），每调一次就重建三段（正常运行中重复调不会打断正在走的协程）
- `Attacker`：已接进回合（`Entity.TakeTurnRoutine()` 里攻击-移动-攻击，回合开始会 `TickTurn()` 推进冷却）；目标获取从 `MapManager.entities` 里找，只认"挂了 `Entity` 且有 `Health`"的单位、按曼哈顿距离排序；`DamageCaster` 只扣血（没有击退/buff/动画表现）；远程没有视线/弹道判定（只看格子距离）；近战/远程的范围是 `AttackPipeline.cs` 里的常量（现在 1 / 3，改小过一次），`AttackPipelineFactory` 只按枚举选实现（碰触体那套 `Attack` 与 `SkillManager` 都已删除，攻击只有攻击管线一条路）
- 预制体：`Assets/prefab/*.prefab` 由 `BattleManager` 在运行时实例化（场景里只有 BattleManager 和 map_pos）；跨对象引用进不了预制体，所以 `map` / `entities` / `actors` 这些槽由 `BattleManager` 填（见「场景」一节）；预制体目前是**手工维护**的，改了结构要自己存（原来那个按名字导出的 `BattlePrefabExporter` 已经删掉）
- `Entity.uuid`：在预制体上手填（`Ranger.prefab` uuid 1、`Melee.prefab` uuid 2）；`SyncOccupied()` 现在会查重但**只警告不修正**（重复时后者这次被跳过），uuid 分配器还没做
- `BattleManager`：预制体靠 Inspector 里手填的**路径字符串**加载——`LoadPrefab` 先按文件名走 `Resources.Load`，编辑器里再退回 `AssetDatabase.LoadAssetAtPath`，所以现在不改目录就能跑，但**打包后必须把预制体放进某个 `Resources` 目录**；清场用 `Destroy`（当帧末尾才真销毁，重开那一帧新旧对象并存，靠显式发地图引用避开旧地图）；实体初始位置完全依赖地图预制体上的 `spawnPoints`（数量不够的实体留在被实例化的位置）；`BuildBattle()` 装完会看 turn 预制体的 `autoStart` 决定要不要立刻开打（true 就调 `StartBattle()`），也可以外部随时调 `StartBattle()` / `RebuildBattle()`；目前没有任何 UI / 快捷键触发它们（NavTest 那套测试按钮已随场景重构删掉）；**组件都不再有 `Awake` / `Start` 初始化，只有 `BattleManager.Awake` 是入口**——谁没被 `Init()` 调到谁就不工作
- `EventPipeline`：静态总线（全局单例语义）——按类型分发、同一类型可订多个、按订阅顺序回调，没有优先级 / 取消 / 一次性订阅；`Clear()` 会清掉**所有类型**的订阅（`BattleManager.ClearBattle` 末尾调一次兜底，谁在它之后才订阅就会被误清，加订阅者时注意）；订阅用方法组（`Subscribe<T>(OnXxx)`），退订必须传同一个方法组——**别用 lambda 订阅**（lambda 退不掉）
- 战斗结束：`BattleManager.EndGame()` 直接调 `TurnManager.Clear()` 让回合停手，所以结束后 `TurnState` 是 `Idle`（不是 `Finished`）、参战列表也空了；要区分"打完了"可以再给 `TurnManager` 一个结束态
- **界面（UI）**：HUD 是场景 `BattleCanvas`（Overlay）上的 uGUI `Text`，血条与伤害数字是预制体（内部是**世界空间 Canvas**，这样两张 Image / 一个 Text 都只用内置 UISprite 与内置字体，不需要美术资源）；**内置字体没有中文字形，界面文字只能英文 / 数字**（`Round x/y` / `Team 0 Wins` / `Draw`），要中文得先进字体资源；血量条没有缓动、没有数字文本，伤害数字不合并同帧多次伤害、不加暴击 / 治疗前缀；字号、条宽、上飘高度这些是拍的初值，**没在播放模式下看过，需要在 Inspector / 预制体里微调**；世界空间 Canvas 每帧转向相机（`Camera.main`），场景里没有主相机会看不到血条与伤害数字
- 脚本都还没在播放模式下跑过（两个角色都没有 Rigidbody，移动是直接写 `transform.position`）

## 按用户给的结构做的重构（进度）

- **P1 实体数据** ✅ `Entity`：uuid / 先攻(`initiative`) / 回合步数 / 生命值门面（`Hp`/`TakeDamage`，数据仍归 `Health`）/ `Init(EntityInitData)`；技能由管线组件承担，不单独存
- **P2 地图数据** ✅ `MapManager`：`int[,] cells` 格子→uuid 二维图 + `Dictionary<int,Vector2Int>` uuid→位置（+ uuid→实体），查询 API 与 uuid 查重
- **P3 寻路** ✅ 搬进 `MapManager.FindPath`，`BfsPathPlanner` 退成薄壳
- **P4 回合** ✅ `TurnManager`：行动栈 `ActionStack`（每回合按先攻重建，`PopNext()` 出栈）+ `TurnState` 状态 + `Init(TurnInitData)`
- **P5 移动管线「接近」** — 用户确认是笔误（现有 靠近/远离 即全部），未做
- **P6 收尾** ✅ 场景 / 预制体同步 + 本文档校对
- **额外清理** ✅ 删掉旧技能系统的残留：`SkillManager`、碰触体 `Attack` 与 4 个 `skill_*` 子物体（攻击只剩攻击管线一条路）；预制体当时由 `BattlePrefabExporter` 重新导出（该工具后来随场景改造成预制体加载而删除）
- **额外清理** ✅ 删掉 `ObjectMover` 组件：移动逻辑坍缩进阶段三 `MoverPathExecutor.Run()`（直接改实体坐标），速度改挂 `AutoPilot.speed`，本回合步数留在 `AutoPilot.ApplySteps` 供"远离"当预算，`Entity.Mover` 一并移除
- **额外清理** ✅ 新增 `BattleManager`（战场统一管理）：把「加载 → 初始化」包成 `BuildBattle()`，另加 `ClearBattle()` 清场、`RebuildBattle()` = 清场 + 重新加载；`Entity.Init` 现在把地图也发给 `Attacker`
- **初始化收拢** ✅ 地图 / 回合 / 攻击 / 生命 / 移动组件的初始化全部搬进 `Init()`，由上级调用（BattleManager → MapManager·Entity·TurnManager，Entity → Health·AutoPilot·Attacker），组件不再自带 `Awake` / `Start` 初始化，也不再 `FindObjectOfType` 找地图（地图一律由上级发下来）
- **清理接口** ✅ 每个组件都暴露 `Clear()`，统一由 `BattleManager.ClearBattle()` 按 回合 → 实体 → 地图 调用（`Entity.Clear()` 再转发 Attacker / AutoPilot / Health）；`TurnManager` 去掉 `StopBattle()`，只保留 `StartBattle()`（只管开始），停止与复位一律由战斗管理器清理
- **事件与结算** ✅ 新增基础事件管线 `EventPipeline`（`Send` / `Subscribe` / `Unsubscribe` / `Clear` + `BattleEvent` / `BattleEventType`）；`Health` 抽出 `Die()` 单独处理死亡并 `Send(EntityDied)`；`BattleManager` 收死亡消息、统计存活阵营，只剩一个（或全灭）就 `EndGame()` 让回合停手并发 `BattleEnded`；`MapManager` 也订同一条消息，阵亡即 `ReleaseEntity()` 放开格子
- **地图数据瘦身** ✅ `MapManager` 删掉 `Dictionary positions`（uuid→位置）与 `byUuid`（uuid→实体）：两者当时只被彼此的维护代码和 `EntityAt` / `EntityOf` 用到，而这两个查询没有任何调用者；`ReleaseEntity` 改为按单位所在坐标放开格子，`SyncOccupied` 的 uuid 查重改用局部表，`SelfCheck` 改成查"每个 uuid 恰好占一格"
- **二维图读写包装** ✅ `MapManager` 内部不再直接下标访问 `cells`：读写走 `UuidAt` / `SetUuid` / `ClearCells`，判空走 `HasGrid`（裸下标只剩在这两个方法里），`IsOccupied` 合并进 `CanEnter`
- **可见性收紧 + 补说明** ✅ 扫了一遍公开面：只在自己类里用的收成 private（`MapManager.Build` / `ResetEntities` / `ReleaseEntity` / `UuidAt` / `EntityAt` / `EntityOf` / `IsOccupied` / `InBounds` / `SelfCheck*`，`TurnManager.BuildActionStack` / `PopNext`），没人用的直接删（`MapManager.EntitiesAt` / `TryGetCell` / `CancelMove`）；伤害改走 `Entity.TakeDamage` 门面（原先 `DamageCaster` 直接调 `Health`）；给管线各实现补上缺的 `<summary>`
- **UI 系统 + 泛型事件管线** ✅ `EventPipeline` 改成按类型收发的泛型管线，事件定义独立到 `BattleEvents.cs`（加事件不用改管线）；新增 `Assets/Scripts/UI/`：`BattleUIManager`（回合数 / 伤害数字 / 胜方显示 + 给实体挂血条）、`HealthBar`（绑 Health，事件刷新 + LateUpdate 贴点位转向相机）、`DamagePopup`（伤害数字上飘淡出）；`Health` 发伤害 / 生命变化消息，`TurnManager` 每回合发回合刷新消息，`BattleManager` 发实体就绪与战斗结束消息并把界面接管进 Init / Clear；`Entity` 加 `uiPoint` 点位（留空自动建在头顶）
- **界面改成 Canvas + 预制体** ✅ HUD 从相机下的 `TextMesh` 换成场景 `BattleCanvas`（Overlay + 两个 uGUI `Text`，直接引用）；血条与伤害数字改为 `Assets/prefab/HealthBar.prefab` / `DamagePopup.prefab`，由 `BattleUIManager` 用新增的 `PrefabLoader` 加载后实例化到实体的 UI 点位下；三个界面脚本都不再在代码里拼 GameObject / 精灵 / 文字，只留绑定、刷新、销毁
- **规范化** ✅ 全部脚本按用户给的顺序重排：**属性 → 生命周期 → 公开方法 → 私有方法**（各组内按调用顺序），属性标签横排一行；私有字段统一去掉下划线前缀，序列化字段补中文 Tooltip；删掉过期的 `BattlePrefabExporter`；分块统一用 `#region` / `#endregion`
- 未做：Map 配置对象（用户说暂时不用）

## 版本管理

- 仓库：https://github.com/Gede1416/GamePlayTest.git，remote 名 `origin`，分支 `main`
- **每次改动都要单独提交一次**，提交信息按 `COMMIT_CONVENTION.md` 写：`<type>(<scope>): <中文描述>`，type 用 feat/fix/docs/style/refactor/perf/test/build/ci/chore/revert，scope 参考该文件里的模块表，一个提交只做一件事（用户靠提交记录回看修改）
- **提交信息用 `git commit -F <临时文件>`**，文件写到 `Temp/`（已被 gitignore 排除）或系统临时目录，**别放仓库根目录**——`git add -A` 会把它一起提交进去（踩过一次）；或者用 `-m` 但保证消息里不含英文双引号 / `<>` / 反引号：PowerShell 里 `-m` 中的英文引号会把消息截断，后面每一段都被 git 当成 pathspec 报 `did not match any file(s)`（踩过两次）。
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
