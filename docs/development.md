# 开发环境

## 当前开发目标（2026-11-25）

当前目标为 **public-beta v0.114.0**（`a7b9a9c0`）与 **public v0.107.1**。Beta 独立开发副本位于主仓 `.local/beta-resource-recovery-20261125/beta1140-content/`，含匹配的 DLL、PCK 和 Spine。旧 Beta 测试记录继续按各自版本读取。

使用完整 RitsuLib **0.6.16 variant-pack**，解压到 `.local/dependencies/RitsuLib-0.6.16/` 或在 `Paths.local.props` 设置 `RitsuLibDir`。Public 引用 `compat/0.107.1`，Beta 引用 `compat/0.114.0`。v0.114.0 给原生卡牌升降级方法加入参数，误用 `compat/0.113.0` 会导致 RitsuLib 的两个关键补丁失败并回滚。

API 反编译按实际版本分目录，本轮使用 `.local/api/queries/public-v0.107.1/` 与 `.local/api/queries/public-beta-v0.114.0/`。匹配 Public 资源及所需辅助依赖位于主仓 `.local/public-resource-recovery-20261113/public107-content/`。测试宿主按所选目标加载 DLL、PCK 和 Spine，日志记录实际程序集版本与资源路径。

## 已发现环境（2026-10-20）

- .NET SDK：9.0.319；项目目标 net9.0。
- Godot：工作区父目录提供 Godot 4.5.1 mono。生产 C# 项目使用 Microsoft.NET.Sdk；资源项目 `src/Resources/` 独立导入并导出 PCK，无需为没有 Godot C# 场景脚本的生产程序集切换 SDK。独立无界面测试项目使用 Godot.NET.Sdk 4.5.1。
- 游戏：`E:/SteamLibrary/steamapps/common/Slay the Spire 2`。
- DLL：游戏下 `data_sts2_windows_x86_64/`，含 sts2.dll、0Harmony.dll、GodotSharp.dll。
- 本机 release_info.json：v0.111.0；Steam appmanifest 的 BetaKey 为 public-beta，Build ID 为 24724944。
- [Steam 分支查询](https://api.steamcmd.net/v1/info/2868840)：public Build ID 23811903；public-beta Build ID 24724944。公开版 v0.107.1 的实际 DLL 已从 GitHub 开发仓库获取，并用其 ProductVersion 的构建提交号与同仓库 release_info.json 交叉核对。文件位于 `.local/api/public-github-0.107.1/`，仅作本地开发参考；尚无完整 public 游戏安装。另已准备两版 NuGet API 引用包。
- RitsuLib：0.6.7；完整 variant-pack 位于 `.local/dependencies/RitsuLib-0.6.7/`，牧师实现已加入对应目标引用和 manifest 依赖。`RitsuLibDir` 可覆盖此默认路径；编译引用 compat/0.107.1 或 compat/0.111.0，运行时安装完整包。

分支信息会变化。每次准备发布时重查，记录实际测试版本。两目标骨架均已构建通过；这不能证明玩法兼容或游戏内加载通过。详见 API 调查与证据（本地记录：`docs/research/dual-branch-api.md`，不随仓库分发）。

## 构建

在仓库根目录运行：

```powershell
Copy-Item Paths.example.props Paths.local.props
pwsh -NoProfile -File ./tools/Build.ps1 -Branch public-beta
pwsh -NoProfile -File ./tools/Build.ps1 -Branch public -GameDataDir 'D:/STS2-public/data_sts2_windows_x86_64'
```

Paths.local.props 不提交 Git；PublicGameDataDir 与 BetaGameDataDir 分别指向两套 `data_sts2_windows_x86_64` DLL 目录。本工作区前者使用独立 public 开发资源，后者使用本机 Steam beta 安装。无界面测试还要求所选数据目录的父目录含匹配的 `SlayTheSpire2.pck` 与 Spine 原生库，以及该版本所需辅助 DLL；不再以 beta 目录替 public 补依赖。保留本地配置后再按实际路径填写示例，也可直接运行 `dotnet build -p:GameBranch=public-beta`。引用游戏程序集时 Private=false，避免把游戏 DLL 打包进 Mod。

构建将 DLL、模组清单和 PCK 写入 `dist/<branch>/AllMatters/`。构建脚本先导入 `src/Resources/`，再用 `src/Tooling/PackResources.gd` 的原生 `PCKPacker` 打包纹理导入文件及原样场景/Spine 资源，不依赖 Windows 导出模板。`tools/ExportResources.ps1` 可单独运行；`tools/Test.ps1` 也会先导出最新 PCK。

### 事件插画的导入和装包

1. 将源 PNG 保存在 `src/Resources/AllMatters/images/events/`，在 Godot 的 Import 面板将 **Compress → Mode** 设为 **Lossy**，质量设为 **0.85**，保持 **Size Limit** 为 **0**，然后重新导入。提交原图及对应的 `.png.import`，压缩设置应为 `compress/mode=1`、`compress/lossy_quality=0.85` 和 `process/size_limit=0`。
2. 对只通过 `ResourceLoader` 读取的插画，将完整 `res://` 路径加入 `src/Tooling/PackResources.gd` 的 `EVENT_PORTRAIT_SOURCES`。打包脚本会先核对 `.png.import` 的源文件、重映射和 `.ctex`，再从 PCK 略过原始 PNG。直接通过 `FileAccess` 读取的文件和 Spine 图集纹理继续按原流程打包。这个打包入口使用 `PCKPacker`，过滤规则应写在打包脚本里。
3. 用 `tools/ExportResources.ps1` 重新生成 PCK。将它挂载到没有项目源图和导入缓存的 Godot 项目，核对原路径可通过 `ResourceLoader.load()` 加载为 `Texture2D`、尺寸与源图一致，且 `FileAccess.file_exists()` 对原 PNG 返回 false。检查 ZIP 只含 DLL、JSON 和 PCK。调整质量后，用相同显示设置做无损与有损的实机对照，重点看细线、渐变和透明边缘。

`v0.1.2a` 的十张事件插画已通过实机画质验收。public 和 public-beta 的 ZIP 均约 13.34 MiB，发布标签区分这次包体修订，模组清单沿用 `v0.1.2`。

需要本机验证时，由用户将开发包复制到游戏 `mods/AllMatters/`，将完整 RitsuLib variant-pack 另装到 `mods/STS2-RitsuLib/`，再通过 Steam 启动游戏。不要只复制 compat 下的单个 DLL。Agent 的安装与实测边界见根目录 `AGENTS.md`。独立测试项目不参与 Mod 打包。牧师的手动验收及环境准备见 牧师验证记录（本地记录：`docs/testing/cleric.md`，不随仓库分发）；你好世界既有用户验收见 原验证记录（本地记录：`docs/testing/hello-world.md`，不随仓库分发）。

默认公开版完整运行环境的准备：可通过 Steam 切换至默认分支，退出游戏后准备独立开发副本，再切回 public-beta。GitHub 下载的 DLL 足以进行本轮方法体检查和编译，但没有 PCK、原生库等完整环境，不能用于启动游戏验收。本次未切换 Steam 分支或修改存档；已更新 mods 中的 AllMatters 并安装 RitsuLib，原 AllMatters 已备份。

无界面测试使用所选分支真实游戏程序集、匹配的 PCK / Spine 及 Godot 原生运行环境。旧记录中 public 共用 beta PCK 的结果只代表当时范围，不能替代匹配 public 资源后的回归。测试宿主替代的边界仍见 验证记录（本地记录：`docs/testing/hello-world.md`，不随仓库分发）。两个分支测试按顺序运行，不能并行使用同一测试输出目录。

## 测试

测试时在 Paths.local.props 中配置 `GodotPath`（mono 可执行文件）及 `TestDotNetRoot`（例如 `.local/tools/dotnet9`），保留现有两分支游戏路径。工具路径可以相对仓库根目录填写；显式命令参数优先于配置。`global.json` 只控制构建 SDK，Godot 的运行时由 TestDotNetRoot 选择。

```powershell
pwsh -NoProfile -File ./tools/PrepareTestRuntime.ps1  # 一次性：用本机已安装的 .NET 9 创建运行目录，不下载运行时
pwsh -NoProfile -File ./tools/Test.ps1               # 默认 public-beta，无需重复填写路径
pwsh -NoProfile -File ./tools/Test.ps1 -Branch All   # 顺序运行 public-beta 与 public
pwsh -NoProfile -File ./tools/Test.ps1 -Filter 'highlights only'
pwsh -NoProfile -File ./tools/Test.ps1 -Filter 'Trash heap' -SkipResourceExport # 仅 C# 改动，显式复用上次资源包
pwsh -NoProfile -File ./tools/Test.ps1 -Branch All -Filter 'highlights only' -SkipResourceExport -ArchiveName host-smoke -ArchivePath .local/tests/host-review -StrictDiagnostics
```

`Test.ps1` 与 `Check.ps1` 要求 PowerShell 7，版本不足时脚本宿主拒绝执行。5.1 会把 Godot 写到 stderr 的行当作终止错误，从而把提示误判为失败。

测试脚本恢复调用前的 DOTNET_ROOT，不修改系统配置；两个分支不能并行使用同一测试输出目录。CLI 仍可传 `-GodotPath`、`-DotNetRoot` 临时覆盖配置。

独立 `Test.ps1` 先编译，再导出一次资源并顺序执行目标；编译失败不启动资源导入。`-SkipResourceExport` 仅用于已成功导出且 `src/Resources/`、`src/Tooling/PackResources.gd` 未变的迭代，资源包缺失会报错；资源改动后使用默认命令重新导出。`Build.ps1` 支持相同选项。完整 `Check.ps1` 每次重新导出一次资源，供双目标测试和打包共用，不依赖资源缓存判定。

`-ArchiveName` 是归档标签（1–80 个字母、数字、点、下划线或连字符，以字母/数字开头且不能以点结尾）；`-ArchivePath` 是归档父目录，相对路径按仓库根目录解析。每次创建带时间和随机后缀的独立目录，同名重跑不覆盖旧证据。默认父目录为 `.local/tests/archives`，标签为 `run`。`All` 在首个失败目标处停止；摘要仅包含本轮已尝试目标，不复制未执行目标的旧 `last.log`。

默认模式保留故障注入测试的引擎诊断输出，仍要求有 Passed 行、编译/宿主没有非零退出或调用异常，且 Metadata 提示不超过 1 条。新宿主专项使用 `-StrictDiagnostics`：额外要求 Build/Host 均明确为 0、Passed 大于 0、EngineErrors/ModMetadataErrors 均为 0；不满足时 Test 入口返回非零。单独解析旧日志也可用 `ReadTestLog.ps1 -Path <日志> -StrictDiagnostics`。默认解析继续兼容未记录 Build/Host 退出码的历史日志。

新增原生 API 调用后先用真实 DLL 编译两个目标，再运行专项行为测试：

```powershell
dotnet build src/Tests/AllMatters.Tests.csproj -p:GameBranch=public-beta --nologo
dotnet build src/Tests/AllMatters.Tests.csproj -p:GameBranch=public --nologo
```

测试准备复用 `src/Tests/HeadlessCombatHost.cs` 中的 `HeadlessTestSetup`：`PlayerWithRun` 返回已绑定测试局的玩家，`Run` 创建指定角色的多人测试局，`Combat` 为局内玩家准备真实战斗状态；已有 `NativeEventTests.Player/Combat` 继续代理该入口。原生资源测试用 `using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas")` 加载并自动撤销资源加载器；需要遗物袋时调用原生 `player.RelicGrabBag.Populate(player, player.RunState.Rng.UpFront)`。比较单目标前后 RNG 是否变化时复用 `RngPosition`，其数值不用于跨版本比较。事件原生文案的变量绑定沿实际 UI 使用事件 DynamicVars；既有框架初始化和生命周期替代顺序保持不变。

### 开工前冒烟检查

每个批次开始前，运行一次测试宿主健康检查以确保 TDD 信号完整：

```powershell
pwsh -NoProfile -File ./tools/Test.ps1 -Branch All
pwsh -NoProfile -File ./tools/ReadTestLog.ps1 -Path .local/tests/public-beta-last.log
pwsh -NoProfile -File ./tools/ReadTestLog.ps1 -Path .local/tests/public-last.log
```

`ReadTestLog.ps1` 本身就是冒烟门：通过数为空、编译/宿主非零退出、准备/调用异常，或「未关联 mod」提示（`ModMetadataErrors`）超过 1 条时退出码为 1；其余情况为 0。两条命令都以退出码 0 结束，批次才能开工；新宿主专项同时传入 `-StrictDiagnostics`，不能拿旧 `last.log` 代签本轮未执行目标。

宿主 red（`ReadTestLog.ps1` 退出码非零）时：
1. 立即修复宿主问题，不开始批次实施工单
2. 将宿主修复记录为独立工单，在批次工单中标记为 blocked by
3. 复验修复后的两目标输出，两条 `ReadTestLog.ps1` 命令均以退出码 0 结束

## 自动检查与 PR 范围

```powershell
pwsh -NoProfile -File ./tools/Check.ps1                  # 语法、空白、占位代码、两分支完整行为检查及开发包
pwsh -NoProfile -File ./tools/Tests/Workflow.Tests.ps1   # 独立运行脚本流程测试，不需要游戏依赖
pwsh -NoProfile -File ./tools/InstallHooks.ps1           # 为当前仓库安装提示型 pre-push 自动入口
pwsh -NoProfile -File ./tools/ReviewScope.ps1            # 相对 origin/main 的提交、变更统计与未提交文件
pwsh -NoProfile -File ./tools/ReviewScope.ps1 -ReviewedFrom '<已审查起点>'
pwsh -NoProfile -File ./tools/GitRemote.ps1 -Operation fetch
```

`Check.ps1` 的 Placeholder scan 检索 `src/**/*.cs`（排除 `src/Tests/`），命中 `Placeholder`、`In a real implementation` 或 `TODO: Implement` 即判该步失败。

pre-commit 的 Husky 任务以 `filteringRule: staged` 匹配 C#；docs/tools-only 暂存跳过 formatter 和 C# 回暂存，C# 格式化失败仍阻止提交。Husky 0.9.1 从 `core.hooksPath` 读取任务配置；若该值是另一棵树的绝对目录，当前树的配置不会自动生效，按下节设置本树路径。`git hook run pre-commit` 可单独验证正常提交入口。

`Check.ps1` 分步骤汇总 PASS/FAIL/SKIP，任一步失败或跳过均返回非零，详细日志在 `.local/checks/last-run.log`。空白问题不阻止其他独立检查，单目标测试失败也继续另一目标及开发包构建；新资源导出失败则明确跳过依赖它的测试和打包，避免使用旧包。脚本流程测试使用隔离临时目录和命令替身验证执行顺序、资源复用与失败传播，不启动游戏。pre-push 自动调用同一命令，将输出留在 `.local/checks/hook-console.log`，失败会警示但仍允许推送。它不是强制合并条件，也不启动正式游戏或安装产物。安装器遇到已有 hook 或 hooksPath 时保留它们并停止，避免覆盖安全措施。其他 clone 需要自行运行安装器；没有依赖的公共 CI runner 不代跑本机测试。

开始新工单前先刷新目标分支并查看 `ReviewScope.ps1`，选择相对 main 的独立分支或明确已有提交的处理方式。准备 PR 时再核对同一范围；审查起点覆盖不到的额外提交由 reviewer 补审或拆分。工具只展示差异，不替代范围判断。

CLI 遇到连接问题时按 [GitHub 操作约定](agents/issue-tracker.md) 处理。`GitRemote.ps1` 尊重既有 Git/环境代理，否则为当前命令采用系统代理，不修改全局配置。

## 并行实施与 worktree

多issue拆片、WIP/角色配置、派发顺序和测试复用见[批次流程](agents/batch-flow.md)。先确定行为切片和所有权，再按本节创建/复用worktree，完成一片后继续拉取就绪项。

创建或复用树后核对 `git config --show-origin --get core.hooksPath`。本机已启用 `extensions.worktreeConfig`；确认当前树的 `.husky/pre-commit`、`.husky/pre-push`、`.husky/_/husky.sh` 和 `task-runner.json` 完整后，在该树运行 `git config --worktree core.hooksPath .husky`，让正常提交读取自己的配置。旧绝对主仓路径仍保留在 common 配置，供缺少 hooks 的历史树继承；不把它全局改成相对路径，否则那些树会悄悄失去 hooks。其他 clone 先核实 worktree 配置扩展和既有 hooks，再配置对应入口。

### Worktree 创建前验证

**每次创建 worktree 前，必须确认上下文和目标**：

1. **验证当前在主仓库根目录**（不在既有 worktree 中）：
   ```powershell
   git rev-parse --git-dir
   # 预期输出：.git （不是 .git/worktrees/<name>）
   
   # 如在 worktree 中，先返回主仓库根目录
   cd /path/to/main/repo
   ```

2. **检查目标工单的 worktree 是否已存在**：
   ```powershell
   git worktree list
   # 查看输出，确认目标分支/工单（如 #92、issue-92）是否已有 worktree
   ```
   
   - 若已存在：**复用既有 worktree**（`cd` 进入），或通过 `SendMessage` 向既有会话发送任务
   - 若不存在：继续创建新 worktree

3. **指定正确的源分支与目标分支**：
   - 源分支：通常为集成分支（如 `integration/batch-N`），不是 `main`
   - 目标分支名：遵循工单命名（如 `issue-92`、`issue-93-feature`）
   - 避免嵌套：新 worktree 必须指向 `.claude/worktrees/<name>` 或类似隔离位置

**错误示例**（曾在 Batch 2 复盘中发现）：
- ❌ 在既有 worktree 中创建子 worktree，导致嵌套路径 `.claude/.claude/.claude/worktrees/`
- ❌ 未检查目标工单是否已有 worktree，导致重复创建
- ❌ 从 `main` 分支切出 worktree，导致开发基线不一致

**验证脚本**：使用 `tools/ValidateWorktreeContext.ps1` 自动检查当前上下文

1. **并行实施一律独立 worktree**：派发两个及以上实施代理时，每个都设 `use_worktree: true`，并显式给出 `source_branch`（集成分支，如 `integration/batch-N`，**不是** `main`）和各自的 `branch_name`。普通 checkout 下 `use_worktree` 默认共享当前目录，不能依赖默认值。
2. **子代理 brief 写明起点**：修复 feature/集成分支上的 bug 时，brief 中写「worktree 必须从 `integration/batch-N` 切出」，并在 `start_session` 传 `source_branch`。
3. **热点文件串行化**：多张票都要改 `src/Jigsaw/JigsawNativeSession.cs` 等集成热点时，先排定合并顺序；后合并者在前者合入后 merge 新基线，再开始改该文件。
4. **集成分支由主会话独占**：子代理只在自己的分支提交；合入 `integration/batch-N` 由主会话逐个执行，每次合入后构建并跑测试。
5. **派发前确认起点包含最新基线**：基线修复（编译参数、宿主初始化等）合入集成分支后，先用 `git merge-base --is-ancestor <基线修复 SHA> <起点>` 确认子代理的起点包含该提交，再派发；brief 中写明起点 SHA。
6. **brief 以完成条件收尾**：每份实施 brief 的最后一条固定写明完成条件：子代理的最后一条消息报告其最终提交 SHA、变更文件清单、运行的测试命令，以及实际通过数与失败数。

### worktree 清理

按 [Git 最佳实践](git-best-practices.md#worktree-清理安全规范强制执行) 先检查、备份并报告，再按仓库授权要求处理。遇到Windows文件句柄占用时，结束持有该树的进程后重试；以当前客户端可用工具为准，不使用旧会话管理工具名。

## 实机验收交付

### 交付流程与顺序

批次完成的固定顺序（缺一即未完成）：

```
实施 (代理开发)
    ↓
自动化测试 (两目标同时) + 初步代码审查
    ↓
[代码审查返还修改意见]
    ↓
Agent 自行修正审查发现的问题 (如有)
    ↓
复测 (两目标) + 复审 (确认修正有效)
    ↓
生成测试包：dist/batch-N-integration/AllMatters-<branch>/
    ↓
写测试文档：docs/batch-N-test-guide.md
    ↓
交付给用户实机验收
```

**关键约定**：
- 打包与文档必须对应审查通过后的同一提交 SHA；交付消息明确该 SHA
- 用户实机反馈后如需修复，循环走「修复 → 复测 → 复审 → 重新打包 → 更新文档」
- 测试文档中添加「修复后验证项」小节，列出对应这轮 bug 的验证步骤

### 测试包内容

mod 包白名单（仅包含以下文件，不含 `.pdb`、`.xml` 或其他调试符号）：

```
AllMatters/
  ├─ AllMatters.dll
  ├─ AllMatters.pck
  └─ AllMatters.json (manifest)
```

生成位置：`dist/batch-N-integration/AllMatters-public-beta/` 和 `.../AllMatters-public/`

构建脚本只复制上述三项，并清理同一输出目录中旧构建遗留的 `LICENSE` 和 `THIRD_PARTY_NOTICES.md`。制作 GitHub Release ZIP 时只归档这三项，许可证与第三方署名作为独立附件随 Release 提供。

### 测试文档

`docs/batch-N-test-guide.md` 内容：

- 安装步骤（复制到 `mods/AllMatters/`，RitsuLib 完整包位置）
- 每项工单的操作序列与预期结果
- 已知限制与未验证范围
- [如有修复轮] 修复后验证项小节

### 交付消息

向用户提供：
- 包的**绝对路径**
- 验证版本（`ReleaseInfo.json` 版本号）  
- 文档位置
- 本批次依赖（如 RitsuLib 版本号）
- 尚待用户操作的清单（是否需逐票安装）

Agent 完成适当的自动化测试和两目标构建后，向用户提供 `dist/<branch>/AllMatters/` 的绝对路径、实际验证版本、独立依赖来源及完整目录要求、安装/更新步骤，以及本票具体的操作与预期结果。产物保留在开发输出目录，由用户安装并通过 Steam 完成游戏实测。

用户反馈写回对应功能验证记录，分别标明自动化、资源加载和用户实测的证据；本票完成条件按 issue 验收范围判断，完整 public、真实联机或回放等集成工作仅在其确为本票要求时阻塞完成。历史记录中的 Agent 部署只描述当时行为，不作为后续操作流程。

分批人工验收按 [tracker 状态约定](agents/issue-tracker.md) 收尾。交付时明确本票所属批次及成员、统一包是否就绪、用户现在是否需要操作和尚未执行的场景；包未就绪时提供后续清单，不要求逐票安装。拼图第 1 批的成员与当前交接见 [单人拼图基本流程](https://github.com/Endercloud001/AllMatters/blob/ca5520bc456bebfb2500753f4fa6f7ad02b3cda4/docs/jigsaw-batch-1.md)。

测试宿主接入 RitsuLib 时，沿用 `src/Tests/HelloWorldTests.cs` 的已验证初始化顺序；变更宿主后先复验受影响的既有行为。牧师接入中曾因宿主生命周期替代安装晚于框架预备方法而导致 public 回归失败，证据见 牧师验证记录（本地记录：`docs/testing/cleric.md`，不随仓库分发）。

### 正式 UI 与原生流程的最小切片

新增正式 UI 时，先打通生产节点入树、引擎派发输入、可见状态变化和下层输入隔离，再扩展布局与操作。复用 `src/Tests/HeadlessUiHost.cs` 准备缺失的原生 action 名称、等待真实节点树；它不模拟回调或设备绑定。示例为 `JigsawPresentationTests.RoutedInput`，通过 `Input.ParseInputEvent` 进入生产节点。直接调用 `_Input` 的用例只证明局部逻辑；生产程序集的 Godot 回调生成由真实路由回归覆盖。

接入奖励、进入房间或保存时，复用 `JigsawNativeTests.FirstRoomFlow` 中的真实原生命令、隔离保存管理器和加载生命周期。该用例覆盖 Ancient 正常选项、首房实际就绪、原生奖励身份与 RNG 保留，以及只读操作不覆盖进入前保存点；完整备份恢复示例为同文件的 `BackupSource` 用例。选择其中所需的最小场景，不把整套拼图流程复制为通用宿主。新增模式身份也要观察原生分支：保存标记不能无意改变运行时 modifiers 或奖励条件。

局部查询用 `./tools/Test.ps1 -Branch All -Filter 'Jigsaw presentation routed input' -SkipResourceExport`；原生首房用例名称从 `src/Tests/JigsawNativeTests.cs` 的 `Cases` 查询。测试共享 beta 资源、缺少正式 Mod 元数据及真实设备的范围仍按用例说明记录，不把宿主通过解释为正式加载或设备验收。

## API 查询

在 Paths.local.props 中配置 `IlspyPath`，使用现有两分支真实 DLL 路径。先找类型，再看成员摘要，需要方法体时才导出：

```powershell
./tools/ExportApiReference.ps1 -Find CardPreviewStyle
./tools/ExportApiReference.ps1 -Branch public -Types MegaCrit.Sts2.Core.Random.Rng -Signatures -Member Counter
./tools/ExportApiReference.ps1 -Types MegaCrit.Sts2.Core.Combat.CombatManager -Signatures -Member CheckForEmptyHand
./tools/ExportApiReference.ps1 -Types MegaCrit.Sts2.Core.Models.Cards.HelloWorld -Member OnPlay
```

默认目标为 public-beta；每次输出实际程序集路径和版本来源。`-Find` 模糊查找类型全名，`-Signatures` 只读取元数据，不执行游戏初始化；`-Member` 按名称片段筛选，`-Limit` 控制返回数量。成员摘要只包含当前类型声明的 public/protected 成员，不展开可空引用注解及所有修饰信息；查继承成员时查询对应父类，精确调用和时序依据完整导出源码。

新增调用先在两目标分别查签名、返回类型及所属类，已有同版本方法体只读取相关成员。两版 Rng 观察方式不同，测试直接复用上述宿主帮助方法；`CardCmd.Upgrade` 返回 void。编译错误或宿主初始化失败属于准备问题，TDD 的红阶段应由目标行为断言失败证明。

不传 `-Find` 或 `-Signatures` 时按原行为导出方法体，省略 OutputDir 则写入忽略目录 `.local/api/queries/<目标与版本>/`，返回来源文件及成员匹配行。原有 `-AssemblyPath -OutputDir -IlspyPath -Types` 用法仍支持；显式程序集不会被配置覆盖。API 查询工具仅用于开发，不参与 Mod 编译/打包，也不需要 Godot 进程。

## 游戏日志与交接

```powershell
./tools/ReadGameLog.ps1                       # 最新 .log 中最后 40 条 AllMatters 匹配行
./tools/ReadGameLog.ps1 -IncludeErrors -Tail 20 # 同时包含全局 ERROR/FATAL/Exception
```

日志目录读取 `Paths.local.props` 的 `GameLogDir`，未填写时使用 `%APPDATA%/SlayTheSpire2/logs`；可用 `-LogDir` 临时覆盖，`-ModId` 指定其他模组。脚本只读，按文件修改时间选择最新 `.log`，输出来源和原始行号；无匹配不代表加载失败。需要排查更早的启动过程时，直接读取输出中的来源文件。分享日志前检查其中的本机路径和个人信息。

`Test.ps1` 直接把原始输出写入本轮归档目录的 `<目标>.log`，并保留 `.local/tests/<目标>-last.log` 兼容副本。目录中的 `summary.json` 统一记录实际入口命令、请求目标、严格模式、成功/失败及原因；每目标记录实际命令、版本、Build/Host/Passed、Engine/Metadata、HostStarted 和原始日志路径。缺失可执行文件、缺少复用包、编译、导出或宿主异常也归档；宿主未启动时 HostExit 为空，不能当作行为 red。归档路径无效或不可写会直接报错，无法在该位置生成证据。

从本轮 `Test.ps1` 输出或 `summary.json` 的 Source 取实际日志路径，再用 `./tools/ReadTestLog.ps1 -Path <实际路径>` 获取首个异常位置及诊断数量；已知 Mod 关联提示单独计数，其余引擎错误仍报告。`-NoExit` 供 runner 共享解析，只设置 LASTEXITCODE 并返回对象，避免解析器退出当前调用链；runner 自己保留原始失败并返回非零。摘要不替代原始日志。进一步读取具体位置使用 `ReadTextSlice.ps1 -Path <日志> -StartLine <行号> -Count 12`，长行可用 `-Pattern` 定位，不重复打印整份日志。

脚本读取 props 中的字面值，不展开 MSBuild 表达式；工具路径可相对仓库根目录填写，游戏程序集目录建议使用绝对路径。配置仅保存在本机忽略文件。

交接实现任务时，先记录目标版本、已核实机制及源码入口，再列尚需用户决定的行为。已有决定直接沿用；在开始 TDD 前一次约定公开接口、验收例子、宿主替代边界和审查起点。机制入口见 [阅读地图](game-mechanics.md)，展示和证据范围见 [审查说明](agents/review.md)。查询时先找类型和成员，再按需导出方法体；保留大文件于本地，交接只给出来源、成员和必要证据。

工作区父目录可能不是 Git 仓库，先定位含 `AGENTS.md`、项目文件和 `.git` 的仓库根目录，再执行 Git/工具命令。文件查找先 `rg --files <目录> -g '<模式>'`，确认文件后按章节或成员读取；PowerShell 下使用 `rg '<内容>' src/Tests -g '*.cs'`，不将 `src/Tests/*.cs` 当成已展开路径。长日志保留本地，只返回退出码、通过数、首个相关错误及来源行；遇到输出截断时缩小范围。

## Git 清理

PR合并后的分支同步、worktree清理及stash操作统一见 [Git 最佳实践](git-best-practices.md)。执行前保留目标树的未提交内容，并遵守目标树AGENTS.md的安全要求。

## 文档与参考目录

先检查各树 `docs` 的实际目录属性和 junction 目标，再决定文档是否共享。2026-11-13 接续时主仓、集成树、兼容树的 `docs` 均为普通目录，`AllMatters_docs` 中的本机交接不会自动出现在各树内。文档由主控统一修改和集成，不依赖本机链接参与构建。参考源码保存在 .local/references，由 .gitignore 排除。编译只枚举 src/**/*.cs，避免误编译参考仓库。

`.local/` 保存依赖、API 资料、运行工具与日志，路径不经过 `docs/`。本机旧 `docs/local/`、`docs/references/` 保留兼容入口，新的默认值和 Paths.local.props 均使用 `.local/`。历史测试日志中的旧路径不重写。切换分支后先保留文档改动、核对实际目录关系，再处理链接；不自动替换已有普通目录。本机依赖仍可直接从 `.local/` 使用。

不运行上游项目，避免其自动部署、更新 API stub 等构建副作用。升级 RitsuLib 时需先验证版本与两分支 API，再调整引用及对应 manifest，不直接照搬上游的浮动包版本和自动部署脚本。

## Git 同步范围

仓库保留源码、自动化测试、资源及其 Godot 导入配置、构建工具、使用与构建文档、玩法范围、许可证和 Agent 安全约定。`src/Tests/` 与 `tools/Tests/` 是可执行测试，继续纳入版本控制；`.import` 与 `.uid` 是资源配置和身份信息，继续保留。

`docs/specs/`、`docs/tickets/`、`docs/research/`、`docs/testing/` 保存本地规格、工单草稿、调查和手动验收材料，不随仓库分发。共享任务以 GitHub Issues 为入口。新 clone 不包含这些本地材料，构建和自动化测试不依赖它们；Agent 查不到本地规格时从对应 Issue 和玩法范围获取需求。已有验收结论不因取消跟踪而扩大，README 中尚待验证的范围仍然有效。

`.local/`、`.scratch/`、本机路径配置、构建产物、缓存和环境密钥文件由 `.gitignore` 排除。已经跟踪的文件仅添加忽略规则不会停止同步，需使用 `git rm --cached` 取消跟踪并提交；本地文件继续保留。这类调整仅改变后续提交，不清理历史提交、其他分支或既有发布附件。
