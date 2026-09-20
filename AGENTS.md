# AGENTS.md — 项目记录

Unity 项目 `My project`，3D 俯视角，**y 为高度**（地面在 XZ 平面）。

## 环境事实（已核实）

- Unity `2022.3.62f2c1`（`ProjectSettings/ProjectVersion.txt`）
- `activeInputHandler: 0` → **老输入系统**，用 `Input.GetAxisRaw` / `Input.GetKey*`，不是 `InputAction`
- 没有 asmdef：`Assets/Scripts/*.cs` 直接编译进 `Assembly-CSharp`
- 资源：`Assets/Scenes/SampleScene.unity`；材质 `Assets/prefab/{entity,ground,skill}-material.mat`（`entity-material.mat` / `skill-material.mat` 是 Ranger / Melee 用的，**已切成标准着色器的 Fade 透明模式**——阵亡淡出要用；`ground-material.mat` 是地图地面的，仍然不透明）
- 场景现在只有「`BattleManager` + 一个空 `map_pos`」加 Directional Light / Main Camera / EventSystem：战斗对象全部由 `BattleManager` 在运行时从预制体加载出来，详见下面"场景"一节

## 脚本清单

- `Assets/Scripts/`：`Entity`（实体，唯一的顶层脚本）
- `Assets/Scripts/Managers/`：管理器（`BattleManager` / `MapManager` / `TurnManager`）+ 预制体加载工具（`PrefabLoader`）
- `Assets/Scripts/Components/`：功能组件（`Health` 生命值 / `DeathEffect` 阵亡淡出）
- `Assets/Scripts/Buffs/`：Buff（`BuffManager` 队列 + 类型与映射 / `BuffPipeline` 两条接口 / `BuffEffect/` 效果零件 / `Combination/` 一个 buff 一个类）
- `Assets/Scripts/Events/`：事件（`EventPipeline` 泛型收发 + `BattleEvents` 里各条消息的数据类）
- `Assets/Scripts/UI/`：战斗界面（`BattleUIManager` 总管 + `HealthBar` 血条 + `BuffBar` buff 条 + `DamagePopup` 伤害数字）
- `Assets/Scripts/Pipeline/Movement/`：移动管线（`AutoPilot` / `PathPipeline` / `TargetSources` / `PathPipelineFactory`）
- `Assets/Scripts/Pipeline/Skill/`：技能管线（`SkillManager` 编排、Inspector 只配技能名 + 记使用次数缓存 / `SkillPipeline` 三段接口 + 组合接口 / `CastCheck/` 阶段一 释放判断 / `TargetFinder/` 阶段二 目标获取 / `SkillCaster/` 阶段三 技能释放 / `Combination/` 把三段拼成一条技能）
- `Assets/Scripts/MPBTest/`：批处理对照测试场脚本（`MPBTestManager`，**与战斗逻辑无关**，见下面「MaterialPropertyBlock 批处理测试场」一节）
- `Assets/Editor/`：编辑器工具（`SceneAutoReload`）

| 文件 | 职责 | 对外接口 |
|---|---|---|
| `Pipeline/Movement/AutoPilot.cs` | 寻路管线编排：阶段一 → 阶段二 → 阶段三；**装配在本组件内**（`Build()` 调 `PathPipelineFactory.Wire`，按枚举造阶段一），和技能管线的 `SkillManager` 一个套路；阶段三自己改坐标，**没有独立的移动组件**；本回合可走步数也存在这里，只有"远离"挑落点时读它 | `TargetSourceType targetSourceType`（枚举定义在 PathPipelineFactory.cs）、`MapManager map`、`float speed`（世界单位/秒）、`TargetSourceType SourceType { get; set; }`、`void Build()`、`void ApplySteps(int steps)`、`ITargetSource TargetSource { get; set; }`、`IPathPlanner Planner { get; set; }`、`IPathExecutor Executor { get; set; }`、`bool RunPipeline()`、`bool MoveTo(Vector3)`、`bool MoveTo(Vector2Int)`、`List<Vector2Int> path`、`bool IsFollowing`、`int Team`（取自自己的 `Health.team`）、`void Init()`（找 Health + 装配三段，由 `Entity.Init` 调）、`void Clear()`（清理：停下 + 让地图数据跟坐标对齐 + 清空路径，由 `Entity.Clear` 调） |
| `Pipeline/Movement/PathPipeline.cs` | 三个阶段接口（依赖一律构造函数注入）+ `BfsPathPlanner(map)`（**薄壳，真正寻路在 `MapManager.FindPath`**）+ `MoverPathExecutor(map, self, speed)`（**自己把实体逐格插值挪过去**，落点与占用问 `MapManager.TryMove`，不再有 ObjectMover 组件） | `ITargetSource.TryGetTarget(out cell)`、`IPathPlanner.TryBuild(start, goal, path)`、`IPathExecutor.Run(path)` |
| `Pipeline/Movement/TargetSources.cs` | 阶段一的两个实现，依赖由构造函数注入（map / self / team / 步数委托），距离一律用 `MapManager.Manhattan` | `ApproachNearestEnemy(map, self, team)`：走向最近的非己方，落在它四周离自己最近的空格；`FleeNearestEnemy(map, self, team, stepsLeft)`：只在**本回合走得到**的格子里（曼哈顿距离 ≤ 本次 `stepsLeft()` 的返回；返回 0 或负数视为不限）挑离最近敌方最远的格 |
| `Managers/BattleManager.cs` | **战场管理**。`BuildBattle()` 按三步拆开：**① 清理资源 `ClearBattle()` → ② 资源加载 + 绑定 + 组件缓存 `LoadBattle()` → ③ 初始化 `InitBattle()`**。`LoadBattle()` 按 Inspector 里配的路径（`mapPath` / `turnPath` / `entityPaths`，见"场景"一节）加载并实例化到 `mapPos` 下，缓存 `MapManager` / `TurnManager` / `List<Entity>`，同时把地图引用发给实体、把实体物体列表接进地图 `entities`（顺序对应 `spawnPoints`）；`InitBattle()` 先订死亡消息，再依次 `MapManager.Init()` → 每个 `Entity.Init()` → `TurnManager.Init(TurnInitData)` → 按 `autoStart` 决定开打。**收到 `EntityDied` 时统计还活着的阵营：只剩一个（或全灭）就 `EndGame()`——让回合停手 + 发 `BattleEnded` 消息** | `void BuildBattle()`（三步走一遍，可反复调）、`void StartBattle()`（转发给回合管理器）、`void RebuildBattle()`（= 清场重来）、`void ClearBattle()`（步骤①：按 回合 → 实体 → 地图 调各组件的 `Clear()`，再销毁加载出来的对象并清缓存）、`bool LoadBattle()`（步骤②，私有，关键对象加载不到返回 false）、`void InitBattle()`（步骤③，私有，先 `uiManager.Init()` 再逐个实体 `Init()` 并发 `EntitySpawnedEvent`）、`void OnEntityDied(EntityDiedEvent)`（私有，收死亡消息）、`void EndGame(int winnerTeam)`（私有，结束游戏 + 发结束消息，-1 = 打平） |
| `Events/EventPipeline.cs` | **泛型事件管线**：按事件**类型**（`Dictionary<Type, Delegate>`）收发，加新事件不用改这个类 | `void Send<T>(T e)`、`void Subscribe<T>(Action<T>)`、`void Unsubscribe<T>(Action<T>)`、`void Clear()`（清场时清订阅） |
| `Events/BattleEvents.cs` | 各条消息的数据类（类型本身就是频道）：`TurnChangedEvent(round, totalRounds)`、`DamageEvent(target, amount)`、`HealthChangedEvent(entity, current, max)`、`EntityDiedEvent(entity, team)`、`EntitySpawnedEvent(entity)`、`BuffChangedEvent(entity)`、`BattleEndedEvent(winnerTeam)` | 每条消息只读字段 `round` / `totalRounds`、`target` / `amount`、`entity` / `current` / `max`、`entity` / `team`、`entity`、`entity`、`winnerTeam` |
| `UI/BattleUIManager.cs` | **战斗界面总管**：全靠事件驱动——回合数（`TurnChangedEvent` 写场景 Canvas 上的文字）、伤害数字（`DamageEvent` 在被打实体 UI 点位下**实例化预制体**）、胜方（`BattleEndedEvent`）、血条（`EntitySpawnedEvent` 时给实体**实例化血条预制体**）。界面元素本身都是场景 Canvas / 预制体，这里只加载、实例化、销毁，**不在代码里组装 UI** | `void Init()`（加载预制体 + 订事件，由 `BattleManager` 调）、`void Clear()`（退订 + 销毁实例化出来的血条 / 伤害数字）、`TMP_Text roundText` / `TMP_Text resultText`（场景 Canvas 下的两个 TMP 文字）、`string healthBarPath` / `string damagePopupPath` / `string buffBarPath`（预制体路径） |
| `UI/HealthBar.cs` | 实体头顶的血条：外观在预制体里（世界空间 Canvas + 底 / 前景两张 `Image`），组件只做三件事——`Init(entity)` 绑实体、按比例改前景 `sizeDelta.x`、`LateUpdate` 贴住实体 UI 点位并转向相机；数值靠 `HealthChangedEvent` 刷新 | `void Init(Entity entity)`（由 `BattleUIManager` 实例化后调）、`void Clear()`（退订）、`Image fill` / `float fullWidth`（预制体里接） |
| `UI/DamagePopup.cs` | 伤害数字：外观在预制体里（世界空间 Canvas + 一个 TMP 文字），组件负责写数字、上飘淡出、转向相机，飘完自毁 | `void Init(float amount)`（由 `BattleUIManager` 实例化后调）、`TMP_Text label` / `float rise` / `float life` / `float startHeight`（预制体里接 / 调） |
| `UI/BuffBar.cs` | **buff 条**：把实体当前生效的 buff 列在头顶。外观全在预制体里（世界空间 Canvas + 一个 TMP 文字），这里只做三件事——`Init(entity)` 绑实体并订 `BuffChangedEvent`、收到消息且是自己那个实体就重拼文字、`LateUpdate` 贴住 UI 点位并转向相机；**不轮询 buff、也不改 buff 数据**（只读 `entity.Buffs.Active`）。由 `BattleUIManager` 在实体就绪时实例化 | `void Init(Entity entity)`（由 `BattleUIManager` 实例化后调）、`void Clear()`（退订）、`TMP_Text label`（预制体里接） |
| `Managers/PrefabLoader.cs` | 预制体加载（`BattleManager` 与 `BattleUIManager` 共用）：先按文件名走 `Resources`，编辑器里再退回按资源路径加载，加载不到报错并返回 null | `static GameObject Load(string path)` |
| `Components/Health.cs` | 生命值 + 阵营；**死亡逻辑单独在 `Die()`**（有 `DeathEffect` 就交给它淡出、没有就直接停用自己，随后发 `EntityDiedEvent`），扣血流程只把血扣到 0 再交给它；对外发 `DamageEvent`（伤害数字）/ `HealthChangedEvent`（血条）/ `EntityDiedEvent`（阵亡结算）；初始化由 `Entity.Init` 调 `Init()` | `float maxHealth`、`int team`、`float Current`、`bool IsDead`、`void Init()`（记住 owner + 把当前值补满）、`void Clear()`（清理：回到未初始化状态）、`TakeDamage(float)`（见底 → `Die()`）、`Heal(float)`（回血不超上限、已阵亡的不治）、`void Die()`（同一条命只走一次） |
| `Components/DeathEffect.cs` | 阵亡表现：把身上的渲染器逐渐淡出，淡完停用自己。原理是用 `MaterialPropertyBlock` 逐渲染器改 `_Color` 的 alpha——**不生成材质实例、也不碰共享材质**（同材质的其它单位不受影响），代价是要求材质本身是透明模式；只在 `Init()` 时收集一次渲染器 | `void Init()`（收集渲染器与它们的原始颜色，由 `Entity.Init` 调）、`void Clear()`（停协程 + 去掉属性覆盖，颜色回到材质上的值）、`void Play()`（由 `Health.Die` 调；正在淡就直接返回，`fadeTime <= 0` 就立刻消失）、`float fadeTime`（淡出秒数） |
| `Buffs/BuffManager.cs` | **Buff 管理器**（挂在实体上，一实体一个）：`BuffType` 枚举（Heal / AddMoveSteps / AddAttack）也在本文件；`Queue<IBuff>` 存正在生效的 buff；`TickTurn()` 轮转一圈——出队 → `Trigger()` → 过期的 `Remove()` 且**不再入队**，没过期的转回队尾；类型名 → 组合 buff 的映射是它自己的 `CreateBuff`（数值 / 持续回合在各组合类里，这里只管队列与生命周期） | `void Init()`（记住主人 + 清队列，由 `Entity.Init` 调）、`void Clear()`（先把每条 `Remove()` 撤掉加成再清队列，由 `Entity.Clear` 调）、`void Add(BuffType, List<Entity> targets = null)`（targets 留空 = 挂给自己）、`void TickTurn()`、`int Count`、`IEnumerable<IBuff> Active`（只读给界面遍历）；挂上 / 到期 / 清场时广播 `BuffChangedEvent` |
| `Buffs/BuffPipeline.cs` | 两条接口：`IBuff`（一条正在生效的 buff：`Type` / `Left` / `IsOver` + `Add` / `Trigger` / `Remove`）与 `IBuffEffect`（效果零件：`Apply(target)` / `Tick(target)` / `Revert(target)`，**数值自己带**，构造函数注入） | `IBuff.Type` / `Left`（还剩几次结算，永久 = -1）/ `IsOver`、`IBuffEffect.Apply` / `Tick` / `Revert` |
| `Buffs/BuffEffect/HealEffect.cs` / `MoveStepsEffect.cs` / `AddAttackEffect.cs` | 三个效果零件，一个文件一个：治疗（挂上 / 每次结算各回一次血）、加移动步数（Apply +N / Revert −N，走 `Entity.AddMoveSteps`）、加攻击力（Apply +N / Revert −N，走 `Entity.AddDamage`）；**每个动作都打一条日志**（目标名字 + 变化量 / 回血后的 Hp/MaxHp） | `HealEffect(float)`、`MoveStepsEffect(int)`、`AddAttackEffect(float)`，都只实现 `IBuffEffect` |
| `Buffs/Combination/HealBuff.cs` / `AddMoveStepsBuff.cs` / `AddAttackBuff.cs` | **一条 buff = 一组效果零件拼起来**（和技能一个套路）：类型名 / 数值 / 持续回合是各自的常量，`effects` 列表 + 目标列表 + 已结算次数是自己的状态，`Add` / `Trigger` / `Remove` 就是"对每个目标、每个零件跑一遍对应动作"；持续回合按**结算次数**算（`Duration <= 0` = 永久）。数值：治疗 20 × 3、步数 +2 × 3、攻击 +5 × 3 | `Add()` / `Trigger()` / `Remove()`、`BuffType Type`、`int Left`、`bool IsOver`、构造 `XxxBuff(List<Entity> targets)` |
| `Managers/MapManager.cs` | 网格 + **一份地图数据**：`int[,] cells` 格子→实体 uuid 的二维图（`Empty=0` 空 / `Unknown=-1` 占着但没登记 uuid）；**订阅死亡消息**：单位阵亡就放开它的格子。uuid→位置 / uuid→实体 的字典已删（当时没有任何消费者，留着只是多一份要同步的真相） | `Init()`（由 `BattleManager` 调：建网格 + 摆实体 + 订死亡消息）、`Clear()`（清理：退订 + 清空二维图与实体列表，保留 `spawnPoints` 配置）、`SyncOccupied()`（按坐标重建二维图 + uuid 查重）、`CanEnter(cell)`、`InBounds(cell)`（外部工具要用，公开）、`static Manhattan(a, b)`、`CellToWorld(col,row)` / `CellToWorld(cell)`、`WorldToCell(pos)`、`TryMove(from, step, out to)`（裁决 + 挪 uuid）、`FindPath(from, to, path)`（四方向等权 BFS）、`int Cols` / `int Rows`、常量 `Empty=0` / `Unknown=-1`；**私有**：`Build` / `ResetEntities` / `ReleaseEntity`（死亡消息触发，按坐标放开格子）/ `SelfCheck` / `SelfCheckPath`（后两个右键组件菜单能跑）；**二维图存取包装**：`HasGrid`（建好没）、`UuidAt(col,row)` / `UuidAt(cell)`（读）、`SetUuid(cell,uuid)`（写）、`ClearCells()`（整张清空） |
| `Managers/TurnManager.cs` | 回合管理（**只管开始，停止/复位统一由 `BattleManager` 调它的 `Clear()`**）。数据：战斗实体列表 `actors` / 行动栈 `ActionStack`（每回合按先攻重建）/ 回合状态 `State`；每回合逐个出栈行动（`yield return actor.TakeTurnRoutine()`），跑满 `totalRounds` 结束 | `List<Entity> actors`、`List<Entity> ActionStack`、`TurnState State`（Idle / Running / Finished）、`int CurrentRound`、`Entity CurrentActor`、`int StackLeft`、`bool IsFinished`、`void Init(TurnInitData)`（由 `BattleManager` 调）、`TurnInitData`（actors / totalRounds / turnDelay）、`void StartBattle()`（**只管开始**，已在跑就直接返回，不会自己停）、`void Clear()`（清理：停协程 + 清空行动栈与参战列表 + 状态归零，由 `BattleManager.ClearBattle` 调）、每回合开头发 `TurnChangedEvent`（界面刷新回合数）、`int totalRounds`（`BuildActionStack` / `PopNext` 已收成私有，只由内部回合循环用）、`float turnDelay`、`bool autoStart` |
| `Entity.cs` | 实体：组件的统一入口 + **对外唯一门面**（回合 / 地图 / UI 只认 Entity），也是身上组件的初始化分发点（Health → DeathEffect → Buffs → AutoPilot → Skills）；身份、先攻、回合步数、管线引用由自己持有，生命值与阵营归 `Health`（只转发），**技能由管线组件承担**（不再单独存技能数据） | `int Uuid`、`int initiative`（先攻）、`int moveSteps`、`float Hp` / `MaxHp`、`bool IsDead`、`int Team`、`void TakeDamage(float)`、`void Heal(float)`、`void AddMoveSteps(int)`、`void AddDamage(float)`、`void AddBuff(BuffType, List<Entity> targets = null)`、`void Init(EntityInitData)`、`EntityInitData`（uuid / initiative / moveSteps / map / moveSource；技能不在这里，由预制体的技能列表配）、`AutoPilot Pilot`、`Health Health`、`SkillManager Skills`、`BuffManager Buffs`、`Transform uiPoint`（UI 点位，血条 / 伤害数字挂它下面；留空时 Init 在头顶自动建一个）、`TargetSourceType SourceType { get; set; }`（转发给 AutoPilot）、`IEnumerator TakeTurnRoutine()`（先 `Buffs.TickTurn()` 结算 buff，再放技能→移动→再放技能（第二遍只补放走完才够得着的那次；放成过的会被各自的释放判断挡住——技能冷却不在这里推，见技能那几行））；`Init` 把地图发给 Health / AutoPilot / Skills 并逐个调它们的 `Init()`（不依赖组件自己的生命周期，也不用 `FindObjectOfType`）、`void Clear()`（反向转发清理：Skills → AutoPilot → Buffs → DeathEffect → Health） |
| `Pipeline/Movement/PathPipelineFactory.cs` | `TargetSourceType` 枚举（ApproachNearestEnemy / FleeNearestEnemy）+ 静态工厂：按枚举造阶段一、统一造阶段二/三 | `CreateSource(type, map, self, team, stepsLeft)`（`Func<int>` 步数委托，给"远离"用）、`CreatePlanner(map)`、`CreateExecutor(map, self, speed)`（`MoverPathExecutor`，自己改坐标）、`Wire(pilot, type, map, self, team, stepsLeft, speed)` |
| `Pipeline/Skill/SkillPipeline.cs` | 技能管线三段接口（**都不依赖技能对象、也不存外部依赖**，范围 / 目标数 / 伤害 / 挂哪种 buff 各自带）+ 一条技能的组合接口 `ISkill`（三段 + `Type` + `AddDamage`）+ 两个攻击 finder 共用的 `TargetPicker.Pick(...)`（返回目标列表） | `ISkill.Type` / `ISkill.AddDamage(float)`、`ICastCheck.CanCast(caster)`、`ITargetFinder.TryFindTargets(caster, map)`（**地图每次调用传进来**）、`ISkillCaster.UsedCount` / `Cast(caster, targets)` |
| `Pipeline/Skill/CastCheck/CooldownCastCheck.cs` | 阶段一「施法者活着 + 冷却好了」。**次数自己问出来**：拿缓存的技能名去施法者身上取使用次数（`caster.Skills.UsedCount(skillType)`），比上次多就是刚放成、进冷却；收 `TurnChangedEvent` 每大回合减一——所以**冷却 ≥ 1 顺带就是"每回合最多放一次"**；订阅在构造函数里做 | `CooldownCastCheck(SkillType, int cooldown = 0)`、`bool CanCast(caster)` |
| `Pipeline/Skill/CastCheck/WoundedCastCheck.cs` | **只管一条血量判断**："自己的血量没满"（满血时治疗没意义）——不继承谁、也不管活着 / 冷却，那些条件由组合技能在 `castChecks` 列表里与起来；血量读 `Entity.Hp` / `MaxHp` 门面 | `WoundedCastCheck()`、`bool CanCast(caster)` |
| `Pipeline/Skill/CastCheck/OnlyCastOnceCastCheck.cs` | **一场战斗只放一次**（例如加攻 buff）：**内部缓存技能名**，判断时通过施法者取这条技能的使用次数，> 0 就是放过了；**一场 = 一份技能**（每场按名单重建，次数跟着归零） | `OnlyCastOnceCastCheck(SkillType)`、`bool CanCast(caster)` |
| `Pipeline/Skill/TargetFinder/MeleeTargetFinder.cs` / `RangedTargetFinder.cs` / `SelfTargetFinder.cs` | 阶段二：近战（范围 1 格、1 目标）/ 远程（范围 3 格、1 目标）/ 自己（目标就是自己，给上 buff 的技能用）；**都是无状态零件**——地图由 `TryFindTargets(caster, map)` 每次传进来，不存字段 | `MeleeTargetFinder.Range` / `TargetCount`、`RangedTargetFinder.Range` / `TargetCount`、`List<Entity> TryFindTargets(caster, map)`（没挑到返回空列表 / `null`） |
| `Pipeline/Skill/SkillCaster/DamageCaster.cs` / `BuffCaster.cs` | 阶段三：扣血（走 `Entity.TakeDamage` 门面，`damage` 能被 buff 加减）/ 挂 buff（走 `Entity.AddBuff` 门面，挂哪种由构造函数带）；**放成一次就 `UsedCount++`**（使用次数缓存） | `DamageCaster(float damage)`、`AddDamage(float)`、`BuffCaster(BuffType)`、`int UsedCount`、`bool Cast(caster, targets)` |
| `Pipeline/Skill/Combination/MeleeSkill.cs` 等四个 | **一条技能 = 三段零件拼起来**：`castChecks`（判断，与）/ `targetFinders`（目标，或）/ `skillCasters`（释放，与）；技能名 / 伤害 / 冷却 / 挂哪种 buff 是这里的常量。四个组合：`MeleeSkill`（伤害 20、冷却 1）、`RangedSkill`（伤害 10、冷却 1）、`HealBuffSkill`（冷却 1 + 血量没满，两条判断的"与" + 挂 Heal）、`AttackBuffSkill`（活着 + 一场一次 + 挂 AddAttack） | `SkillType Type`、`int UsedCount`（各释放零件次数之和）、`CanCast` / `TryFindTargets(caster, map)` / `Cast`、`AddDamage(float)`（只有伤害零件认） |
| `Pipeline/Skill/SkillManager.cs` | **技能管理器**（挂在实体上，`[RequireComponent(typeof(Entity))]`）：按预制体上的 `skillTypes`（**只有技能名**）调自己的 `CreateSkill` 造组合技能（技能名 → 组合技能的映射就在这里，`SkillType` 枚举也在本文件）；`RunPipeline()` 挨个跑 **判断 → 目标（地图从这儿给）→ 释放**，三段都过才算放成、并把次数记进 `skillUseCount`；**额度 / 冷却一行都不在这里**（归各技能的释放判断，判据就是这份缓存）；含 `[ContextMenu]` 跑一次 / 打印技能 | `List<SkillType> skillTypes`（预制体上配）、`MapManager map`、`Dictionary<SkillType,int> skillUseCount`（使用次数缓存）、`int UsedCount(SkillType)`、`IReadOnlyList<ISkill> Skills`、`void Init()`（找 Entity + 造技能，由 `Entity.Init` 调）、`void Build()`（按配置重建 + 清缓存）、`bool RunPipeline()`、`void AddDamage(float)`、`void Clear()` |

## 场景（Assets/Scenes/SampleScene.unity）

场景本体只剩一个 `BattleManager` 和一个空 `map_pos`（另有 Directional Light / Main Camera / EventSystem）；
**战斗对象全部在运行时由 `BattleManager` 从预制体加载**，所以改数值要改预制体，不是场景。

- `BattleManager`（GO `321782083`）：`mapPath` = `Assets/prefab/map1.prefab`、`turnPath` = `Assets/prefab/turn1.prefab`、
  `entityPaths` = [`Assets/prefab/Melee.prefab`, `Assets/prefab/Ranger.prefab`]（顺序对应地图的 `spawnPoints`）、
  `mapPos` = `map_pos`、`uiManager` = 同一个物体上的 `BattleUIManager`（接好 `roundText` / `resultText` 指向 BattleCanvas 下的两个 TMP 文字，预制体路径 `Assets/prefab/HealthBar.prefab`、`Assets/prefab/DamagePopup.prefab` 与 `Assets/prefab/BuffBar.prefab`）
- `map_pos`（GO `308726978`）：只有 Transform，位置 (0,0,0)；加载出来的地图/实体/回合控制器都挂它下面（`mapPos` 留空就放场景根）
- `BattleCanvas`（GO `321782090`）：Screen Space - Overlay 的 Canvas + CanvasScaler（1920×1080）+ **GraphicRaycaster**（按钮要点得到，配合场景里原有的 EventSystem）；子物体分三处——`RoundText`（左上角，`Round x/y`）与 `ResultText`（屏幕中央，胜方 / Draw）都是 TMP 文字、接到 `BattleUIManager` 上；`GameObject`（GO `2125401385`）是个容器，里面装着两个按钮：`StartButton`（Image + Button，标签 `Start`）onClick 接 `BattleManager.StartBattle()`、`RebuildButton`（Image + Button，标签 `Rebuild`）onClick 接 `BattleManager.RebuildBattle()`
- 场景检查：`python _validate_scene.py`（现在 56 个块；查 fileID 引用、组件归属、父子关系、SceneRoots、缩进）。**手工改场景 YAML 的脚本要整块替换（连 `--- !u!N &id` 那一行一起），只换块正文会把块头吃掉**，场景里就查不到这个组件了（踩过一次，靠这个检查脚本抓出来）
- 预制体（都在 `Assets/prefab/`，手工维护）：
  - `map1.prefab`：MeshFilter / MeshRenderer / MeshCollider + `MapManager`（`cellSize` 1；`spawnPoints` = [(1,1), (2,2)]；
    `entities` 是 2 个**空槽**——跨对象引用进不了预制体，由 `BattleManager` 运行时填）
  - `Melee.prefab`：Entity(uuid 2, 先攻 44, moveSteps 4) + AutoPilot(靠近, speed 5) + SkillManager(skillTypes = [Melee, AttackBuff]) + Health(team 0, 50) + DeathEffect(fadeTime 0.6) + BuffManager
  - `Ranger.prefab`：Entity(uuid 1, 先攻 10, moveSteps 3) + AutoPilot(远离, speed 5) + SkillManager(skillTypes = [Ranged, HealBuff]) + Health(team 1, 50) + DeathEffect(fadeTime 0.6) + BuffManager
  - `turn1.prefab`：`TurnManager`（`totalRounds` 5、`turnDelay` 0.2、**`autoStart` 关**——改由场景里的 `StartButton` 调 `BattleManager.StartBattle()` 开打；`actors` 也是 2 个空槽，由 `BattleManager` 填）
  - `HealthBar.prefab`：世界空间 Canvas（scale 0.01 → 1.0×0.12 世界单位）+ `Back` / `Fill` 两张 Image（内置 UISprite，白图染色）+ `HealthBar` 组件（`fill` 接前景、`fullWidth` 100）
  - `DamagePopup.prefab`：世界空间 Canvas（scale 0.01 → 1.2×0.4 世界单位）+ `Text (TMP)`（TMP 文字，40 号、居中、偏黄）+ `DamagePopup` 组件（`label` 接文字，上飘 1 / 存活 0.9 秒）
  - `BuffBar.prefab`：世界空间 Canvas（scale 0.01 → 2.4×0.24 世界单位）+ `Text (TMP)`（20 号、居中、白色，局部 y +18 = 血条上方 0.18 世界单位）+ `BuffBar` 组件（`label` 接文字）
  - 四个预制体里的 `map` 字段全是空的：运行时 `BattleManager` 把地图实例发给 `Entity`，`Entity.Init` 再转给 AutoPilot / Skills
- 编辑器工具 `Assets/Editor/SceneAutoReload.cs`：磁盘上的 `.unity` 一变就自动重新加载当前场景（内存里未保存的版本先另存到 `Temp/编辑器未保存版本_*.unity`）；菜单 `Tools/场景以磁盘为准` 开关（默认开）、`Tools/重新加载当前场景（以磁盘为准）` 手动触发；播放中不动场景

## MaterialPropertyBlock 批处理测试场（Assets/Scenes/MPBTestScene.unity）

**结论（2026-09-17 核实，都有出处）**：项目是 **Built-in RP**（没有 URP / HDRP 包）→ **没有 SRP Batcher**，「MPB 破坏 SRP Batcher」不适用；
Built-in 下 MPB 是喂 GPU Instancing 逐实例数据的正道，但属性必须在着色器里声明成逐实例（`UNITY_DEFINE_INSTANCED_PROP`），
**本机 2022.3.62 的内置 Standard 就是反例**（`_Color` 是普通 uniform）→ **`DeathEffect` 现在这种「MPB 改 Standard 的 `_Color`」写法，只要材质勾了 Enable GPU Instancing 就会把渲染器踢出 instancing**；
项目现在三个材质都没勾 Enable GPU Instancing（静态批处理开、动态批处理关），所以**战斗单位一个都没走 instancing**。

**怎么用**：打开 `MPBTestScene` 按播放，左上角 IMGUI 列 6 种模式与实时读数（Batches / SetPass / Draw Calls，走 `ProfilerRecorder`，等同 Stats）。
按 1→6 走一遍攒出「同数量下 batches / setpass / 材质数」对照表；`[` `]` 换数量档（100 / 500 / 2000 / 5000）、`空格` 切每帧写 MPB 的动画、`S` 切静态批处理、`R` 重建；配合 Frame Debugger 看为什么（没）合批。

| 模式 | 做法 | 该看到什么 |
|---|---|---|
| 1 | Standard + 开 GPU Instancing，不写 MPB | 基线：几百个方块压成 1 个 draw call |
| 2 | Standard + 开 GPU Instancing + MPB 逐实例 `_Color` | `_Color` 不是逐实例属性 → instancing 被禁用，批次数 ≈ 方块数 |
| 3 | `MPBTint`（`_Color` 声明在 `UNITY_INSTANCING_BUFFER`）+ MPB | **正道**：逐实例颜色 + 仍然 1 个 draw call |
| 4 | 同 3，但材质关掉 Enable GPU Instancing | 没有 instancing 兜底时，MPB 换不来合批 |
| 5 | 每物体一份 `renderer.material` | 反面教材：材质数 = 方块数，SetPass 暴涨 |
| 6 | `Graphics.DrawMeshInstanced` + MPB 数组 | 1 次调用画 1023 个，绕开 GameObject / Renderer |

- 文件：`Assets/Scripts/MPBTest/MPBTestManager.cs`（自己 `Awake → Build()` 起，没挂进战斗的 Init 链）、`Assets/MPBTest/MPBTint.shader`、`Assets/Scenes/MPBTestScene.unity`（只有相机 / 灯光 / `MPBTest` 一个物体，方块运行期造，灯光阴影关掉）。
- 有意留下的缺口：着色器靠 `Shader.Find` 找，**打包会被剔除**（要打包就给它建个材质资产）；HUD 是英文 IMGUI；跑分要自己按 1→6。
- 提交这个场景改动前的自检：`python _validate_scene.py Assets/Scenes/MPBTestScene.unity`（脚本支持传场景路径，默认查主场景）。

## 约定

- 注释、Tooltip 用中文；字段名用英文。
- **成员顺序**（每个类都照这个来，四段各自内部按调用顺序排）：
  1. **类属性**：字段与属性（序列化字段 → 常量 → 运行期状态字段 → 属性）
  2. **生命周期**：`Awake` / `Start` / `OnDisable` / `OnDrawGizmosSelected` 等 Unity 回调
  3. **公开方法**
  4. **私有方法**（含 `[ContextMenu]` 的调试方法）
  纯数据类（`EntityInitData` / `TurnInitData`）和静态工厂只有属性/方法，按同样的先后即可。
- **初始化统一走 `Init()`，由上级组件调用**：`BattleManager.Awake()`（唯一的入口）→ `MapManager.Init()` / `Entity.Init()` / `TurnManager.Init(TurnInitData)`；`Entity.Init()` 再把初始化发给身上的组件：`Health.Init()` → `DeathEffect.Init()`（收集渲染器）→ `Buffs.Init()`（记主人 + 清队列）→ `AutoPilot.Init()`（找 Health + 装配三段 + `ApplySteps`）→ `Skills.Init()`（找 Entity + 按配置造技能并装配三段）。**子组件一律不写 `Awake` / `Start` 做初始化**（`AutoPilot.OnDisable` 只是收尾停手，不算初始化）；地图等依赖由上级先写进字段再调 `Init()`。谁忘了调 `Init()` 谁就不工作。
  **清理同样由上级发起**：每个组件都暴露 `Clear()`（TurnManager / MapManager / Entity / AutoPilot / SkillManager / BuffManager / DeathEffect / Health），`BattleManager.ClearBattle()` 按 回合 → 实体（Entity.Clear → Skills → AutoPilot → Buffs → DeathEffect → Health）→ 地图 的顺序统一调，之后才销毁对象；组件自己不做收尾：`TurnManager.StartBattle()` 只管开始（已在跑就返回），不负责停。
- **可见性从紧**：只有**现在就有外部调用**的成员才写 `public`（其它类调用 / Unity 生命周期 / 编辑器菜单 `[ContextMenu]` / `[MenuItem]`）；只在自己类里用的收成 `private`（`[ContextMenu]` 能调私有方法，不用为了菜单放开）；暂时没人用的直接删，等外部真要用时再加一个包装方法向外公开。**例外**（用户点名要的对外入口，保持 `public`）：`BattleManager.BuildBattle` / `StartBattle` / `RebuildBattle` / `ClearBattle`、`Health.Die`、`EventPipeline` 的 `Send` / `Subscribe` / `Unsubscribe` / `Clear`。
- 只写被要求的功能：不加接口/工厂/配置项，不加脚手架。故意砍掉的东西在回复里说明"跳过了 X，需要 Y 时再加"，不预先实现。
- 用 `#` 对 `Vector2Int` 的格子坐标：`.x` = 列（世界 x 方向），`.y` = 行（世界 z 方向），**不是世界高度**。
- 世界坐标用 `Vector3`，地面用 `Vector2` 存 `(x, z)`；不要用 Vector2 直接赋给 `transform.position`（会把 y/z 清 0）。
- 管线三段（移动：`ITargetSource` / `IPathPlanner` / `IPathExecutor`；技能：`ICastCheck` / `ITargetFinder` / `ISkillCaster`）的依赖都走**构造函数注入**，接口只收"这次要处理什么"（起点/终点/路径；施法者/目标列表/地图）；不要往接口里塞 AutoPilot / MapManager / Entity。技能这边的零件**不存地图**：阶段二的地图由 `SkillManager` 每次调用时当参数传进去（`TryFindTargets(caster, map)`）。
- **技能 = 三段零件拼起来的组合**：一条技能在 `Combination/` 里把 `castChecks`（判断，**与**）/ `targetFinders`（目标，**或**）/ `skillCasters`（释放，**与**）拼起来，技能名 / 数值是组合类自己的常量；`ISkill` = 三段接口 + `Type` + `AddDamage`，`SkillManager` 只管按名单造、按顺序跑。
  **释放条件一律写成 `CastCheck`**（活着 / 冷却 / 血量没满 / 场次额度），不存在"管理器再补一条判断"，检查之间也**不互相继承**（`WoundedCastCheck` 只管血量），要哪几条就在组合类的 `castChecks` 列表里与起来；判据是**使用次数缓存**：阶段三的零件放成一次就 `UsedCount++`（`ISkillCaster`），组合技能按零件求和暴露成 `ISkill.UsedCount`，`SkillManager` 放成后按技能名记进 `skillUseCount`。检查**内部缓存技能名**，判断时通过施法者取数（`caster.Skills.UsedCount(skillType)`）——`OnlyCastOnceCastCheck` 直接看有没有放过，`CooldownCastCheck` 看次数比上次多不多（多就是刚放成、进冷却），冷却每大回合收 `TurnChangedEvent` 减一。**冷却 ≥ 1 就等于"每回合最多放一次"**（一个单位一大回合只行动一次），所以没有单独的"每回合一次"检查。
- **Buff 也照技能那套拼**：`Buffs/` 的布局和 `Pipeline/Skill/` 一样——管理器（`BuffManager`，`BuffType` 枚举与"名字 → 组合 buff"的映射也在它里面）+ 接口文件（`BuffPipeline.cs`）+ 零件目录（`BuffEffect/`，一个效果一个文件，数值走构造函数）+ 组合目录（`Combination/`，一个 buff 一个类，数值与持续回合是它自己的常量，目标与已结算次数是它的状态）。`IBuff` 的三步（`Add` / `Trigger` / `Remove`）就是"对每个目标、每个效果零件跑一遍对应动作"；没有 `Buff` / `BuffCfg` / `BuffFactory` 那套基础结构。
- **地图数据只有一份**：`MapManager` 的 `int[,] cells`（格子→实体 uuid 二维图），外部只读；改数据只能走 `TryMove`（走一格，裁决 + 挪 uuid）或 `SyncOccupied`（按坐标整体重建），阵亡放开格子由内部的 `ReleaseEntity` 负责。类内部也不再散着 `cells[x, y]` 下标操作：读写一律走私有包装 `UuidAt(col,row)` / `UuidAt(cell)` / `SetUuid(cell,uuid)` / `ClearCells()`，判空走 `HasGrid`（裸下标只出现在这两个方法里），以后换存储方式只改这一组方法。要"某个 uuid 在哪格 / 是哪个实体"目前没有需求——**需要时再加一份索引或包装方法**，别提前留着两份真相。
- **数据所有权**：一份数据只有一个所有者。身份(uuid) / 先攻 / 回合步数 / 管线类型 归 `Entity`；生命值 + 阵营归 `Health`；本回合可走步数（`AutoPilot.ApplySteps`，只有"远离"挑落点用）、动画中（`AutoPilot.IsFollowing`）属于各自组件的运行期状态；**使用次数**归各自的释放零件（`ISkillCaster.UsedCount`），`SkillManager.skillUseCount` 是按技能名的缓存，**冷却剩余**归各自的 `CooldownCastCheck`。`Entity` 是对外唯一门面（`Hp` / `MaxHp` / `IsDead` / `Team` / `TakeDamage()` 转发），**不要把组件自己的属性搬进 Entity**（会变成两份真相 + 组件不能单独工作）。
- `Health` 是 2D/3D 无关的，其余脚本的维度假设见上表。
- 私有字段**不加下划线前缀**（`routine` / `cells` / `mapManager`，不是 `_routine`）。
- **属性标签横排一行**：同一个字段上的多个特性写在同一行，例如 `[Tooltip("技能类型：近战 / 远程 / 施放 buff")] public SkillType type = SkillType.Melee;`（`[Header(...)]` 也照样接在同一行）。
- **代码分块用 `#region` / `#endregion`**：每个类里四段各一个 region（`属性` / `生命周期` / `公开方法` / `私有方法`），构造函数单独一个 `构造` region；`公开方法` 内部的语义子块（`查询` / `移动裁决` / `寻路` / `自检`）用**嵌套** region；region 与它包住的成员同缩进。**类不到 80 行就不分块**（成员照四段顺序直接写下来，套 region 反而更花）；只有一组公开静态方法的工厂类（`*PipelineFactory`）也不用分块。方法体内部的注释块仍用 `// ---------- xxx ----------`（方法里不套 region）。
- **事件走泛型 `EventPipeline`**：`Subscribe<T>(处理函数)` / `Unsubscribe<T>(...)` / `Send(new XxxEvent(...))`，按类型分发（`Dictionary<Type, Delegate>`），**加新事件只要在 `BattleEvents.cs` 加一个数据类，管线本身一行都不用改**。现在的收发关系：
  - `TurnChangedEvent`（TurnManager 每回合开头发）：BattleUIManager 刷回合数 + 各技能的 `CooldownCastCheck` 冷却减一
  - `DamageEvent`（Health.TakeDamage）/ `HealthChangedEvent`（Health.Init / TakeDamage）：UI 飘伤害数字 / 血条刷新
  - `EntityDiedEvent`（Health.Die）：BattleManager 判胜负 + MapManager 放开格子；`EntitySpawnedEvent`（InitBattle）：UI 挂血条
  - `BuffChangedEvent`（BuffManager 挂上 / 到期 / 清场）：BuffBar 重拼文字；`BattleEndedEvent`（EndGame）：UI 显示胜方
  订阅在各自的 `Init()` 里订、`Clear()` 里退订（`MapManager.Init` 是先退订再订，重复 Init 也不会订两遍），`BattleManager.ClearBattle()` 末尾再用 `EventPipeline.Clear()` 兜一道。
- 需要可视化的逻辑（如网格划分）用 `OnDrawGizmosSelected` 画出来核对，不写单元测试。

## 已知缺口（用户明确跳过的）

- 移动执行（原 `ObjectMover`，现已并入阶段三 `MoverPathExecutor`）：格子移动是四方向直线插值——无斜向、无转向、无绕障碍；**只按 `speed` 插值、不拦步数**（行动约束由阶段一/二决定，见 `AutoPilot.ApplySteps`）；`TryMove` 通过时起点的占用就放开了，所以动画中起点是空的（要"动画中两侧都占住"就加个 `Arrive(from)`，到达时再放开起点）；走到一半被 `AutoPilot.Clear()` / `OnDisable` 打断时，靠 `map.SyncOccupied()` 按坐标重建占用数据对齐（不是精确回退到起点）；`MapManager.TryMove` 要求**起点也在网格内**（走到地图外的物体会拒绝移动）
- `MapManager`：地图数据只按 `entities` 列表重建（不在列表里的实体会走、格子记 `Unknown`）；自检 `SelfCheck` 现在查的是"每个登记了 uuid 的单位在二维图里恰好占一格"；`entities` 被搬动/销毁后要自己调 `SyncOccupied()` 对齐；寻路是 `MapManager.FindPath`（四方向、每格等权 BFS，`BfsPathPlanner` 只是薄壳；A* / 加权代价没做）；无地形/障碍数据
- `AutoPilot`：管线只在被调用时跑一次（不会周期性重算/自动追人）、路径算完不重算（中途被挡就放弃）、BFS 的目标格必须可进入（站着人的格子不能当终点）；三段装配由 `AutoPilot.Build()` 自己完成（`Entity.Init` 只把地图发下去）
- `TargetSources`：目标从 `MapManager.entities`（地图就是单位注册表）里找、按曼哈顿距离挑最近——**不在 map 实体列表里的单位不会被当成目标**；"靠近"落地成"站到敌人四周离自己最近的空格"（敌人那格进不去，**不按步数裁剪**——目标在预算外就这回合走一段、下回合接着走），曼哈顿距离 ≤ 1 视为已贴身、这次不产生目标；"远离"只在本回合步数可达的菱形里挑最远格（地图没有障碍时等价于可达，以后有地形要换成按步数上限做 BFS 洪泛）
- `Health.team`：阵营就是个 int，没有仇恨表/友军保护；`Die()` 只做「停用自己（挂了 `DeathEffect` 就交给它淡出）+ 发消息」这件事，**放开格子交给 `MapManager`**——它收到死亡消息会 `ReleaseEntity()`（按坐标清掉二维图里的格子 + 把物体从 `entities` 里摘掉），所以阵亡单位不再挡路、也不会再被当成目标；尸体物体本身还在 `BattleManager` 的 `entities` 列表里（结算统计要用），清场时才销毁
- `DeathEffect`（阵亡淡出）：只淡 **Init 时收集到的**渲染器，Init 之后新建的子物体不管——所以血条**不在**淡出范围内（它会一直挂到物体被停用为止）；淡出那 `fadeTime` 秒里物体是启用的，于是**尸体仍然会行动**（`TurnManager` 出栈不看 `IsDead`、死亡时也没停 `AutoPilot`，见上面几条），这是这次**有意先不做的**；`AutoPilot.OnDisable → Clear()` 也因此晚 `fadeTime` 秒才触发；材质现在走透明混合（`ZWrite 0`），要前后遮挡更正确可以改成 `ZWrite 1`（半透明处会出现硬边）；`Play()` 由 `Health.Die()` 直接调（没走事件），所以 `Health` 知道表现层的存在
- **Buff**：数值 / 持续回合是各自组合 buff 里的常量（不能在 Inspector 调，要配表再说）；结算时机是**自己行动时**（`Entity.TakeTurnRoutine` 开头），不是"每大回合"，要改就挪到 `TurnChangedEvent` 那边；队列里**同一类型可以叠**（挂两次就两份，各自按自己的回合数过期），没有层数上限 / 驱散 / 免疫；"移除增加的移动步数 / 攻击力"是同一个效果零件的 `Revert`，**不是单独的 BuffType**（拆开就得各记一份数值，容易和实际加成对不上）；目标列表存在组合 buff 里（`Add(type, targets)` 可以挂给别人），现在只用到"挂给自己"；尸体上的 buff 仍会随它自己的回合结算（`TurnManager` 不看 `IsDead`），但治疗对它无效（`Health.Heal` 遇到已阵亡直接返回）；清场时 `BuffManager.Clear()` 会把加成撤掉，所以别在外面直接改 `moveSteps` / `damage` 记"临时加成"，走 buff；三个组合 buff 的三步生命周期循环是**重复的**（和技能那边一个情况，要不要抽个基类再说）
- **Buff 界面**：buff 条显示的是"类型名 + 剩余结算次数"的文字（`Heal(3) Move(2)`），因为默认字体没有中文字形，只能英文 / 数字；没有图标、层数徽章、进度条，一个类型的多条会各占一段（队列里有几条就显示几段）；显示样式（字号 / 颜色 / 偏移）全在 `BuffBar.prefab` 里调，别在代码里改；一帧内多次变动（例如一次挂两条）会刷多次，现在没做合并；buff 条和血条都挂在实体的 UI 点位下，位置靠各自预制体里的局部坐标错开
- `TurnManager`：行动内容写死在 `Entity.TakeTurnRoutine()` 里（固定 放技能-移动-再放技能，没做成可配置的行动序列；换顺序就改它）；状态只能轮询 `State` / `IsFinished`，没有回合开始/结束事件；行动栈每回合重建一次，**回合中不重排**（先攻变了要等下回合）；先攻相同时按列表顺序而不掷骰；没有"跳过/延后/守卫"这类规则
- `Entity` / `PathPipelineFactory`：先攻只有 `Entity.initiative` 一份（`TurnManager` 的排序和行动都走 Entity）；`moveSteps` 仍是 Entity 的数据，靠 `AutoPilot.ApplySteps(moveSteps)` 存进 AutoPilot（只有"远离"挑落点时当预算，阶段三不扣步数）；移动管线的装配在 `AutoPilot.Build()`（`Entity.Init` 只把地图发下去再调它），每调一次就重建三段（正常运行中重复调不会打断正在走的协程）
- `SkillManager` / 技能：已接进回合（放技能 → 移动 → 再放技能，第二遍只补放走完才够得着的那次）。**额度 / 冷却全在释放判断里**（冷却 ≥ 1 顺带就是每回合一次、一场一次、血量没满），判据是**使用次数缓存**，管理器一条判断都不补；"刚放成过"是判断在 `CanCast` 里轮询次数涨没涨看出来的，**没有"放成了"这条消息**；没有"驱散 / 免疫 / 沉默"拦截；次数按**技能名**记账（同一实体配两条同名技能会共用一个次数）。治疗只看施法者自己的血量、只判"不满就放"（溢出由 `Health.Heal` 截断），不限场次 → **只要还伤着就每回合续一条治疗 buff**（同名会叠、无上限）。攻击目标来自 `MapManager.entities`（非己方且活着，按曼哈顿距离排序），阶段二每次**新造一个目标列表**；上 buff 的技能目标是自己，给队友 / 敌人的 buff 技能没做；远程没有视线 / 弹道判定（只看格子距离）；范围 / 伤害 / 冷却 / buff 种类都是各自零件与组合类的常量，**预制体上只配技能名**；`CooldownCastCheck` 在构造函数里订 `TurnChangedEvent` 且**不退订**（靠 ClearBattle 末尾的 `EventPipeline.Clear()` 兜，同一场里重复 `Build()` 会订两遍、冷却一次减二）；组合类的聚合循环是重复的（要不要抽基类待定）；界面上没有技能额度 / 冷却显示，只能靠 `[ContextMenu]` 打印或看日志
- 预制体：`Assets/prefab/*.prefab` 都是运行时实例化的（战斗对象由 `BattleManager` 加载，血条 / 伤害数字由 `BattleUIManager` 按事件加载；场景里只有 BattleManager / map_pos / BattleCanvas）；跨对象引用进不了预制体，所以 `map` / `entities` / `actors` 这些槽由 `BattleManager` 填（见「场景」一节）；预制体目前是**手工维护**的，改了结构要自己存（原来那个按名字导出的 `BattlePrefabExporter` 已经删掉）
- `Entity.uuid`：在预制体上手填（`Ranger.prefab` uuid 1、`Melee.prefab` uuid 2）；`SyncOccupied()` 现在会查重但**只警告不修正**（重复时后者这次被跳过），uuid 分配器还没做
- `BattleManager`：预制体靠 Inspector 里手填的**路径字符串**加载——`LoadPrefab` 先按文件名走 `Resources.Load`，编辑器里再退回 `AssetDatabase.LoadAssetAtPath`，所以现在不改目录就能跑，但**打包后必须把预制体放进某个 `Resources` 目录**；清场用 `Destroy`（当帧末尾才真销毁，重开那一帧新旧对象并存，靠显式发地图引用避开旧地图）；实体初始位置完全依赖地图预制体上的 `spawnPoints`（数量不够的实体留在被实例化的位置）；`BuildBattle()` 装完会看 turn 预制体的 `autoStart` 决定要不要立刻开打（**现在 turn1.prefab 里是关的**，所以等场景里的 `StartButton` 点一下）；`StartBattle()` / `RebuildBattle()` / `ClearBattle()` 都可以外部随时调——目前 `StartButton` 接 `StartBattle()`、`RebuildButton` 接 `RebuildBattle()`；**组件都不再有 `Awake` / `Start` 初始化，只有 `BattleManager.Awake` 是入口**——谁没被 `Init()` 调到谁就不工作
- `EventPipeline`：静态总线（全局单例语义）——按类型分发、同一类型可订多个、按订阅顺序回调，没有优先级 / 取消 / 一次性订阅；`Clear()` 会清掉**所有类型**的订阅（`BattleManager.ClearBattle` 末尾调一次兜底，谁在它之后才订阅就会被误清，加订阅者时注意）；订阅用方法组（`Subscribe<T>(OnXxx)`），退订必须传同一个方法组——**别用 lambda 订阅**（lambda 退不掉）
- 战斗结束：`BattleManager.EndGame()` 直接调 `TurnManager.Clear()` 让回合停手，所以结束后 `TurnState` 是 `Idle`（不是 `Finished`）、参战列表也空了；要区分"打完了"可以再给 `TurnManager` 一个结束态
- **界面（UI）**：文字全用 TMP（HUD 两个文字 + 两个按钮标签 + 伤害数字 + buff 条）；**默认字体 `LiberationSans SDF` 没有中文字形，界面文字只能英文 / 数字**（`Round x/y` / `Team 0 Wins` / `Draw` / `Start` / `Rebuild`），要中文得自己做带中文字形的 TMP 字体并在 TMP Settings 设回退；TMP Essential Resources 已入库，缺了文字不显示；血条 / buff 条挂在实体 UI 点位下（世界空间 Canvas + 内置 UISprite），每帧转向 `Camera.main`（**场景里没主相机就看不到**）；血条没有缓动 / 数字，伤害数字不合并同帧多次、不加暴击前缀；开始 / 重开按钮点完不会自己隐藏（重复点是安全的）
- 脚本都还没在播放模式下跑过（两个角色都没有 Rigidbody，移动是直接写 `transform.position`）

## 重构现状

- 各层都是一个套路：**管理器 + 三段零件 + 组合类**（实体 / 地图 / 回合 / 移动管线 / 技能管线 / Buff / UI / 事件总线都已摆好）。
  每一步怎么改的、为什么这么改，看 `git log` 与提交信息（本项目约定一个改动一个提交）。
- **未做**：Map 配置对象（用户说暂时不用）。
- 待定（问过用户、还没定）：技能组合类与 Buff 组合类的装配 / 生命周期循环是各自抄一遍的，要不要抽基类；见「已知缺口」。

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
