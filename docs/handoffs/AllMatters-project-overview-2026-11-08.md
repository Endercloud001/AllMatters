# AllMatters：项目基本介绍与接手入口

交接日期：2026-11-08（Asia/Shanghai）。面向首次接触该仓库的 Agent；具体 #80 续作见同目录 [开发进度交接](AllMatters-issue80-handoff-2026-11-08.md)。本文件位于仓库 docs/handoffs/，是接手导航，不是权威规格。

## 先定位正确的仓库

- 实际工作仓库：`E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters`。
- GitHub：[Endercloud001/AllMatters](https://github.com/Endercloud001/AllMatters)。任务跟踪采用 GitHub Issues 及原生 sub-issue/dependency 关系。
- 外层 `sts2mod` 不是 Git 仓库；旁边有多份研究、原型和 worktree，不能误当成正式生产目录。
- 本机 `AllMatters/docs` 是到同级 `AllMatters_docs` 的 Windows junction。Git 跟踪其中普通文档；新 clone 可使用普通 docs 目录。依赖在 `.local/`，不要通过 junction 清理或搬动它们。

## 项目是什么

这是《杀戮尖塔 2》的 C# / Godot 模组。README 介绍的 v0.1.1 内容以二代事件扩展、一代事件/遗物/卡牌回归为主；当前另在实施 v0.1.2 拼图跑局。发布范围、设计规则和未验承诺分别以各自权威资料为准，不把 README 版本号当作当前全部开发状态。

新 Agent 建议按以下顺序读取，而非通读整个仓库：

| 目的 | 一手入口（相对仓库根目录） |
|---|---|
| 操作约束、文件组织 | `AGENTS.md` |
| 开发、双版 DLL 查询、测试宿主、打包和人工交付 | `docs/development.md` |
| 现有功能及历史范围 | `README.md`、`docs/v0.1-scope.md`、`docs/v0.1.1-scope.md` |
| 术语与领域上下文 | `GLOSSARY.md`、`docs/agents/domain.md`；相关 ADR 若存在再读 |
| 游戏机制调用链 | `docs/game-mechanics.md` |
| GitHub 操作、分批人工验收状态政策 | `docs/agents/issue-tracker.md` |
| 审查约定 | `docs/agents/review.md` |
| 依赖来源、移植参考 | `docs/references.md` |
| 拼图权威规格 | [Issue #73](https://github.com/Endercloud001/AllMatters/issues/73) 正文、明确修订及其引用的规范性来源 |

`docs/specs/`、`docs/research/` 等可能有本地留存资料，但部分不随仓库分发；缺失时从 Issue 获取。旧原型和本地草稿不能覆盖正式规格。

## 代码导航

| 目录/文件 | 用途 |
|---|---|
| `src/ModEntry.cs` | 模组注册与初始化入口 |
| `src/Jigsaw/` | 拼图领域规则、生成、原生跑局接入、保存与恢复 |
| `src/Jigsaw/Presentation/` | 正式 Godot 节点、输入、布局与呈现 |
| `src/Tests/` | 使用真实目标 DLL / Godot / Ritsu 的自动宿主和行为测试 |
| `src/Relics/` 等内容目录 | 已有模组内容；接手 #80 不要顺手纳入其他遗物修改 |
| `src/Resources/` | 模组资源；变更后需要重新导出资源包 |
| `tools/` | PowerShell 构建、测试、检查、API 查询与 Git 辅助入口 |
| `.local/` | 忽略的本机依赖、API 导出、只读参考源码、日志和研究工作树 |
| `dist/`、`bin/`、`obj/` | 开发输出，不等于已经安装或发布 |

拼图职责及事实来源见 #73；不要将传输/UI 变成另一份合法性、随机或扣步规则。临时协作状态、原队员、当前在局玩家和持久完成记录需要区分。

## 环境与常用命令

当前目标是 **public 0.107.1** 和 **public-beta 0.111.0**；本机使用 .NET 9、Godot 4.5.1 mono、Ritsu Runtime 0.6.7。路径配置读现有 `Paths.local.props`，不要覆盖、打印敏感配置或安装/升级依赖。后续若环境改变，应重新核对实际 DLL 产品版本。

在实际仓库根目录用 PowerShell：

```powershell
git rev-parse HEAD
git status --short
./tools/ReviewScope.ps1
dotnet build src/Tests/AllMatters.Tests.csproj -p:GameBranch=public-beta --nologo
dotnet build src/Tests/AllMatters.Tests.csproj -p:GameBranch=public --nologo
./tools/Test.ps1 -Branch All -Filter '<相关用例名称>' -SkipResourceExport
./tools/Check.ps1
```

`-SkipResourceExport` 只在既有资源包可用且资源输入未变时使用。完整 Check 会统一导出资源、顺序验证两目标并打包。双版测试共用输出目录，**不能并发运行测试/构建进程**。新的外部 API 调用先用 `tools/ExportApiReference.ps1` 分别核对两个真实 DLL；详见开发指南，别猜 namespace/签名。

复用宿主：`HelloWorldTests` 的框架初始化顺序、`HeadlessUiHost` 的入树和引擎输入、`JigsawNativeTests.FirstRoomFlow` 的真实奖励/隔离保存、`JigsawNetworkTests` 的独立 ENet 进程。编译或宿主准备失败不是 TDD 行为 red。

## 必须延续的约束

- 不启动游戏或 Steam，不安装/复制到游戏 mods，不改真实存档，不替用户做实机验收。
- 保留全部无关未提交修改；只暂存本任务文件或代码块。不要执行清理工作树、强制重置等破坏性操作。
- 默认不新增 hash、冻结 contract、baseline 或 gate；保留现有认证、兼容性、存档安全和发布边界。
- 原生网络、Godot 自动宿主、正式游戏多端、真实设备是不同证据。public 自动宿主目前共享 beta 资源，不能代签完整 public 正式资源运行。
- 正常源码/测试任务不自动授权发布 Release、推送、安装或向他人发消息。
- 长任务遵循用户工具约定：空 `write_stdin` 轮询至少 180000ms，优先 300000ms；`functions.wait` 至少 180000ms；嵌套工具长等待时 `functions.exec` 外层 yield 至少再加 30000ms。过程提前结束会提前返回，避免只为报“仍在运行”而唤醒。

## Suggested skills

下一 Agent 应按实际阶段调用对应 Skill（若宿主没有 Skill 工具，则读取确切 SKILL.md）：

- `ask-matt`：路由已有工作，避免重做已经完成的决策流程。
- `implement` + `tdd`：按规格与已确认原生/Godot seam 逐片实现；读本机 `C:/Users/Endercloud/.agents/skills/<name>/SKILL.md`。
- `diagnosing-bugs`：复现间歇性、宿主或原生生命周期问题。
- `code-review`：最终 Standards / Spec 两轴独立审查。
- `grill-with-docs`：只有确实需要改变既定职责、规则或依赖时才澄清；不能用它反复要求已授权事项确认。
- `pr`：仅在实际需要撰写 PR 描述时使用。

不要因技能可用就启动新规划、重建规格或开始尚未解除阻塞的其他整票。
