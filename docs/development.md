# 开发环境

## 已发现环境（2026-10-20）

- .NET SDK：9.0.319；项目目标 net9.0。
- Godot：工作区父目录提供 Godot 4.5.1 mono。生产 C# 项目使用 Microsoft.NET.Sdk；资源项目 `src/Resources/` 独立导入并导出 PCK，无需为没有 Godot C# 场景脚本的生产程序集切换 SDK。独立无界面测试项目使用 Godot.NET.Sdk 4.5.1。
- 游戏：`E:/SteamLibrary/steamapps/common/Slay the Spire 2`。
- DLL：游戏下 `data_sts2_windows_x86_64/`，含 sts2.dll、0Harmony.dll、GodotSharp.dll。
- 本机 release_info.json：v0.111.0；Steam appmanifest 的 BetaKey 为 public-beta，Build ID 为 24724944。
- [Steam 分支查询](https://api.steamcmd.net/v1/info/2868840)：public Build ID 23811903；public-beta Build ID 24724944。公开版 v0.107.1 的实际 DLL 已从 GitHub 开发仓库获取，并用其 ProductVersion 的构建提交号与同仓库 release_info.json 交叉核对。文件位于 `.local/api/public-github-0.107.1/`，仅作本地开发参考；尚无完整 public 游戏安装。另已准备两版 NuGet API 引用包。
- RitsuLib：0.6.7；完整 variant-pack 位于 `.local/dependencies/RitsuLib-0.6.7/`，牧师实现已加入对应目标引用和 manifest 依赖。`RitsuLibDir` 可覆盖此默认路径；编译引用 compat/0.107.1 或 compat/0.111.0，运行时安装完整包。

分支信息会变化。每次准备发布时重查，记录实际测试版本。两目标骨架均已构建通过；这不能证明玩法兼容或游戏内加载通过。详见 [API 调查与证据](research/dual-branch-api.md)。

## 构建

在仓库根目录运行：

```powershell
Copy-Item Paths.example.props Paths.local.props
./tools/Build.ps1 -Branch public-beta
./tools/Build.ps1 -Branch public -GameDataDir 'D:/STS2-public/data_sts2_windows_x86_64'
```

Paths.local.props 不提交 Git；PublicGameDataDir 与 BetaGameDataDir 分别指向两套 DLL。本工作区前者指向从 GitHub 核对后下载的 0.107.1 实际 DLL，后者指向本机 Steam beta 安装；复制示例 props 会覆盖这些本地设置，请按实际路径填写。也可直接运行 `dotnet build -p:GameBranch=public-beta`。引用游戏程序集时 Private=false，避免把游戏 DLL 打包进 Mod。

输出按分支隔离到 `dist/<branch>/AllMatters/`；构建不会自动写游戏目录。现在包含 DLL、manifest、PCK、GPLv3 LICENSE 和来源声明。构建脚本先导入 `src/Resources/`，再用 `src/Tooling/PackResources.gd` 的原生 `PCKPacker` 打包纹理导入文件及原样场景/Spine 资源，不依赖 Windows 导出模板。`tools/ExportResources.ps1` 可单独运行；`tools/Test.ps1` 也会先导出最新 PCK。

需要本机验证时，由用户将开发包复制到游戏 `mods/AllMatters/`，将完整 RitsuLib variant-pack 另装到 `mods/STS2-RitsuLib/`，再通过 Steam 启动游戏。不要只复制 compat 下的单个 DLL。Agent 的安装与实测边界见根目录 `AGENTS.md`。独立测试项目不参与 Mod 打包。牧师的手动验收及环境准备见 [牧师验证记录](testing/cleric.md)；你好世界既有用户验收见 [原验证记录](testing/hello-world.md)。

默认公开版完整运行环境的准备：可通过 Steam 切换至默认分支，退出游戏后准备独立开发副本，再切回 public-beta。GitHub 下载的 DLL 足以进行本轮方法体检查和编译，但没有 PCK、原生库等完整环境，不能用于启动游戏验收。本次未切换 Steam 分支或修改存档；已更新 mods 中的 AllMatters 并安装 RitsuLib，原 AllMatters 已备份。

你好世界的无界面测试使用目标分支真实游戏程序集、Godot 原生运行环境及本机 beta 资源包，测试命令和替代的宿主边界见 [验证记录](testing/hello-world.md)。两个分支测试按顺序运行，不能并行使用同一测试输出目录。

## 测试

测试时在 Paths.local.props 中配置 `GodotPath`（mono 可执行文件）及 `TestDotNetRoot`（例如 `.local/tools/dotnet9`），保留现有两分支游戏路径。工具路径可以相对仓库根目录填写；显式命令参数优先于配置。`global.json` 只控制构建 SDK，Godot 的运行时由 TestDotNetRoot 选择。

```powershell
./tools/PrepareTestRuntime.ps1  # 一次性：用本机已安装的 .NET 9 创建运行目录，不下载运行时
./tools/Test.ps1               # 默认 public-beta，无需重复填写路径
./tools/Test.ps1 -Branch All   # 顺序运行 public-beta 与 public
./tools/Test.ps1 -Filter 'highlights only'
./tools/Test.ps1 -Filter 'Trash heap' -SkipResourceExport # 仅 C# 改动，显式复用上次资源包
```

测试脚本恢复调用前的 DOTNET_ROOT，不修改系统配置；两个分支不能并行使用同一测试输出目录。CLI 仍可传 `-GodotPath`、`-DotNetRoot` 临时覆盖配置。

独立 `Test.ps1` 先编译，再导出一次资源并顺序执行目标；编译失败不启动资源导入。`-SkipResourceExport` 仅用于已成功导出且 `src/Resources/`、`src/Tooling/PackResources.gd` 未变的迭代，资源包缺失会报错；资源改动后使用默认命令重新导出。`Build.ps1` 支持相同选项。完整 `Check.ps1` 每次重新导出一次资源，供双目标测试和打包共用，不依赖资源缓存判定。

新增原生 API 调用后先用真实 DLL 编译两个目标，再运行专项行为测试：

```powershell
dotnet build src/Tests/AllMatters.Tests.csproj -p:GameBranch=public-beta --nologo
dotnet build src/Tests/AllMatters.Tests.csproj -p:GameBranch=public --nologo
```

测试准备复用 `src/Tests/HeadlessCombatHost.cs` 中的 `HeadlessTestSetup`：`PlayerWithRun` 返回已绑定测试局的玩家，`Run` 创建指定角色的多人测试局，`Combat` 为局内玩家准备真实战斗状态；已有 `NativeEventTests.Player/Combat` 继续代理该入口。原生资源测试用 `using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas")` 加载并自动撤销资源加载器；需要遗物袋时调用原生 `player.RelicGrabBag.Populate(player, player.RunState.Rng.UpFront)`。比较单目标前后 RNG 是否变化时复用 `RngPosition`，其数值不用于跨版本比较。事件原生文案的变量绑定沿实际 UI 使用事件 DynamicVars；既有框架初始化和生命周期替代顺序保持不变。

## 自动检查与 PR 范围

```powershell
./tools/Check.ps1                  # 语法、空白、两分支完整行为检查及开发包
./tools/Tests/Workflow.Tests.ps1   # 独立运行脚本流程测试，不需要游戏依赖
./tools/InstallHooks.ps1           # 为当前仓库安装提示型 pre-push 自动入口
./tools/ReviewScope.ps1            # 相对 origin/main 的提交、变更统计与未提交文件
./tools/ReviewScope.ps1 -ReviewedFrom '<已审查起点>'
./tools/GitRemote.ps1 -Operation fetch
```

`Check.ps1` 分步骤汇总 PASS/FAIL/SKIP，任一步失败或跳过均返回非零，详细日志在 `.local/checks/last-run.log`。空白问题不阻止其他独立检查，单目标测试失败也继续另一目标及开发包构建；新资源导出失败则明确跳过依赖它的测试和打包，避免使用旧包。脚本流程测试使用隔离临时目录和命令替身验证执行顺序、资源复用与失败传播，不启动游戏。pre-push 自动调用同一命令，将输出留在 `.local/checks/hook-console.log`，失败会警示但仍允许推送。它不是强制合并条件，也不启动正式游戏或安装产物。安装器遇到已有 hook 或 hooksPath 时保留它们并停止，避免覆盖安全措施。其他 clone 需要自行运行安装器；没有依赖的公共 CI runner 不代跑本机测试。

开始新工单前先刷新目标分支并查看 `ReviewScope.ps1`，选择相对 main 的独立分支或明确已有提交的处理方式。准备 PR 时再核对同一范围；审查起点覆盖不到的额外提交由 reviewer 补审或拆分。工具只展示差异，不替代范围判断。

CLI 遇到连接问题时按 [GitHub 操作约定](agents/issue-tracker.md) 处理。`GitRemote.ps1` 尊重既有 Git/环境代理，否则为当前命令采用系统代理，不修改全局配置。

## 实机验收交付

Agent 完成适当的自动化测试和两目标构建后，向用户提供 `dist/<branch>/AllMatters/` 的绝对路径、实际验证版本、独立依赖来源及完整目录要求、安装/更新步骤，以及本票具体的操作与预期结果。产物保留在开发输出目录，由用户安装并通过 Steam 完成游戏实测。

用户反馈写回对应功能验证记录，分别标明自动化、资源加载和用户实测的证据；本票完成条件按 issue 验收范围判断，完整 public、真实联机或回放等集成工作仅在其确为本票要求时阻塞完成。历史记录中的 Agent 部署只描述当时行为，不作为后续操作流程。

测试宿主接入 RitsuLib 时，沿用 `src/Tests/HelloWorldTests.cs` 的已验证初始化顺序；变更宿主后先复验受影响的既有行为。牧师接入中曾因宿主生命周期替代安装晚于框架预备方法而导致 public 回归失败，证据见 [牧师验证记录](testing/cleric.md)。

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

脚本读取 props 中的字面值，不展开 MSBuild 表达式；工具路径可相对仓库根目录填写，游戏程序集目录建议使用绝对路径。配置仅保存在本机忽略文件。

交接实现任务时，先记录目标版本、已核实机制及源码入口，再列尚需用户决定的行为。已有决定直接沿用；在开始 TDD 前一次约定公开接口、验收例子、宿主替代边界和审查起点。机制入口见 [阅读地图](game-mechanics.md)，展示和证据范围见 [审查说明](agents/review.md)。查询时先找类型和成员，再按需导出方法体；保留大文件于本地，交接只给出来源、成员和必要证据。

工作区父目录可能不是 Git 仓库，先定位含 `AGENTS.md`、项目文件和 `.git` 的仓库根目录，再执行 Git/工具命令。文件查找先 `rg --files <目录> -g '<模式>'`，确认文件后按章节或成员读取；PowerShell 下使用 `rg '<内容>' src/Tests -g '*.cs'`，不将 `src/Tests/*.cs` 当成已展开路径。长日志保留本地，只返回退出码、通过数、首个相关错误及来源行；遇到输出截断时缩小范围。

## 文档与参考目录

本工作区 `AllMatters/docs` 是指向 `../AllMatters_docs` 的 junction，Git 索引保存其中的普通文档文件。重新 clone 时 docs 是普通目录，不依赖原机器路径。参考源码保存在 .local/references，由 .gitignore 排除。编译只枚举 src/**/*.cs，避免误编译参考仓库。

`.local/` 保存依赖、API 资料、运行工具与日志，路径不经过 `docs/`。本机旧 `docs/local/`、`docs/references/` 保留兼容入口，新的默认值和 Paths.local.props 均使用 `.local/`。历史测试日志中的旧路径不重写。切换分支后若 docs junction 被 Git 替换为普通目录，先保留文档改动再恢复 junction；本机依赖仍可直接从 `.local/` 使用。

不运行上游项目，避免其自动部署、更新 API stub 等构建副作用。升级 RitsuLib 时需先验证版本与两分支 API，再调整引用及对应 manifest，不直接照搬上游的浮动包版本和自动部署脚本。
