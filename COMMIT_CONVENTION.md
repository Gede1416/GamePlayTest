# 提交规范

格式：

```
<type>(<scope>): <描述>
```

对应关系：`type` 说清**改动的性质**，`scope` 说清**改动落在哪**，描述说清**改了什么**。

## type（必填，小写）

| type | 用途 | 例子 |
|---|---|---|
| `feat` | 新增功能 | `feat(login): 新增用户登录功能` |
| `fix` | 修复缺陷 | `fix(api): 修复数据接口返回异常` |
| `docs` | 只改文档 | `docs: 更新接口文档` |
| `style` | 只改格式（缩进、空格、空行），不改逻辑 | `style: 优化代码缩进` |
| `refactor` | 重构（既不加功能也不修缺陷） | `refactor(user): 重构用户数据结构` |
| `perf` | 性能优化 | `perf: 提升列表渲染性能` |
| `test` | 增删测试 | `test: 增加登录模块测试用例` |
| `build` | 构建、依赖、打包相关 | `build: 升级 webpack 至 5.x` |
| `ci` | CI / 自动化配置 | `ci: 优化 GitHub Actions 配置` |
| `chore` | 杂项（不属于以上分类的零碎改动） | `chore: 删除无用文件` |
| `revert` | 回滚 | `revert: 回滚 feat(login) 的实现` |

判断顺序：先问"这是加功能还是修 bug"，都不是再往下（格式 → style，结构 → refactor，速度 → perf，文档 → docs，工具链 → build/ci，其他 → chore）。

## scope（可选）

括号里写影响范围，本项目的建议取值：

| scope | 对应 |
|---|---|
| `move` | `ObjectMover` |
| `path` | `AutoPilot` / `PathPipeline` / `TargetSources` |
| `map` | `MapManager` |
| `health` | `Health` |
| `attack` | `Attack` |
| `skill` | `SkillManager` |
| `ui` | `NavTest` / Canvas 测试按钮 |
| `scene` | `SampleScene.unity` 里的对象/组件接线 |
| `repo` | 仓库配置、git、工具脚本（如 `.gitignore`、`_validate_scene.py`） |

纯文档、跨模块或说不清归属的改动**不写 scope**（`docs: 更新接口文档`）。

## 描述（必填）

- 中文，动词开头，说清"改了什么"，结尾不加句号
- 一行写完，尽量 ≤ 30 字
- 不要写「更新」「修改一下」「若干优化」这种看不出内容的

## 正文与脚注（可选）

标题后空一行写正文，回答"**为什么**这样改"（怎么改看 diff 就够了）；需要时再空一行写脚注：

```
feat(path): 管线三段改为构造函数注入

[SerializeReference] 反序列化时只会调无参构造，注入进去的依赖会丢，
所以改成构造函数注入 + 属性装配。

Refs: #3
BREAKING CHANGE: ITargetSource.TryGetTarget 不再接收 AutoPilot 参数
```

- 破坏性变更：type 后加 `!`（`feat(api)!: ...`），并/或在脚注写 `BREAKING CHANGE: ...`
- 关联 issue：脚注写 `Refs: #12` / `Closes: #12`

## 其他

- **一个提交只做一件事**：功能改动别和格式化混在一起，重构别顺手改行为
- 场景改动和它依赖的脚本改动可以放同一个提交（避免中间状态在 Unity 里打不开），但无关的改动不要混进来
- 提交前先自检：脚本改动跑 `dotnet build Assembly-CSharp.csproj`，场景改动跑 `python _validate_scene.py`
- 没跑过、编不过、或者只是半成品，不要提交
