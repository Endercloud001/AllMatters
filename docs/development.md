# 开发环境

## 已发现环境（2026-10-20）

- .NET SDK：9.0.319；项目目标 net9.0。
- Godot：工作区父目录提供 Godot 4.5.1 mono。生产 C# 项目使用 Microsoft.NET.Sdk；资源项目 `src/Resources/` 独立导入并导出 PCK，无需为没有 Godot C# 场景脚本的生产程序集切换 SDK。独立无界面测试项目使用 Godot.NET.Sdk 4.5.1。
- 游戏：`E:/SteamLibrary/steamapps/common/Slay the Spire 2`。
- DLL：游戏下 `data_sts2_windows_x86_64/`，含 sts2.dll、0Harmony.dll、GodotSharp.dll。
- 本机 release_info.json：v0.111.0；Steam appmanifest 的 BetaKey 为 public-beta，Build ID 为 24724944。
- [Steam 分支查询](https://api.steamcmd.net/v1/info/2868840)：public Build ID 23811903；public-beta Build ID 24724944。公开版 v0.107.1 的实际 DLL 已从 GitHub 开发仓库获取，并用其 ProductVersion 的构建提交号与同仓库 release_info.json 交叉核对。文件位于 `docs/local/api/public-github-0.107.1/`，仅作本地开发参考；尚无完整 public 游戏安装。另已准备两版 NuGet API 引用包。
- RitsuLib：0.6.7；完整 variant-pack 位于 `docs/local/dependencies/RitsuLib-0.6.7/`，牧师实现已加入对应目标引用和 manifest 依赖。`RitsuLibDir` 可覆盖此默认路径；编译引用 compat/0.107.1 或 compat/0.111.0，运行时安装完整包。

分支信息会变化。每次准备发布时重查，记录实际测试版本。两目标骨架均已构建通过；这不能证明玩法兼容或游戏内加载通过。详见 [API 调查与证据](research/dual-branch-api.md)。

## 构建

在仓库根目录运行：

```powershell
Copy-Item Paths.example.props Paths.local.props
./tools/Build.ps1 -Branch public-beta
./tools/Build.ps1 -Branch public -GameDataDir 'D:/STS2-public/data_sts2_windows_x86_64'
```

Paths.local.props 不提交 Git；PublicGameDataDir 与 BetaGameDataDir 分别指向两套 DLL。本工作区前者指向从 GitHub 核对后下载的 0.107.1 实际 DLL，后者指向本机 Steam beta 安装；复制示例 props 会覆盖这些本地设置，请按实际路径填写。也可直接运行 `dotnet build -p:GameBranch=public-beta`。引用游戏程序集时 Private=false，避免把游戏 DLL 打包进 Mod。

输出按分支隔离到 `dist/<branch>/AllMatters/`；构建不会自动写游戏目录。现在包含 DLL、manifest、PCK、GPLv3 LICENSE 和来源声明。构建脚本先导入 `src/Resources/`，再用 `--export-pack Resources` 生成资源包，不依赖 Windows 导出模板。`tools/ExportResources.ps1` 可单独运行；`tools/Test.ps1` 也会先导出最新 PCK。

需要本机验证时，将开发包复制到游戏 `mods/AllMatters/`，将完整 RitsuLib variant-pack 另装到 `mods/STS2-RitsuLib/`，再由用户通过 Steam 启动游戏。不要只复制 compat 下的单个 DLL。独立测试项目不参与 Mod 打包。牧师的手动验收及环境准备见 [牧师验证记录](testing/cleric.md)；你好世界既有用户验收见 [原验证记录](testing/hello-world.md)。

默认公开版完整运行环境的准备：可通过 Steam 切换至默认分支，退出游戏后准备独立开发副本，再切回 public-beta。GitHub 下载的 DLL 足以进行本轮方法体检查和编译，但没有 PCK、原生库等完整环境，不能用于启动游戏验收。本次未切换 Steam 分支或修改存档；已更新 mods 中的 AllMatters 并安装 RitsuLib，原 AllMatters 已备份。

你好世界的无界面测试使用目标分支真实游戏程序集、Godot 原生运行环境及本机 beta 资源包，测试命令和替代的宿主边界见 [验证记录](testing/hello-world.md)。两个分支测试按顺序运行，不能并行使用同一测试输出目录。

## 测试

测试时在 Paths.local.props 中配置 `GodotPath`（mono 可执行文件）及 `TestDotNetRoot`（例如 `docs/local/tools/dotnet9`），保留现有两分支游戏路径。工具路径可以相对仓库根目录填写；显式命令参数优先于配置。`global.json` 只控制构建 SDK，Godot 的运行时由 TestDotNetRoot 选择。

```powershell
./tools/PrepareTestRuntime.ps1  # 一次性：用本机已安装的 .NET 9 创建运行目录，不下载运行时
./tools/Test.ps1               # 默认 public-beta，无需重复填写路径
./tools/Test.ps1 -Branch All   # 顺序运行 public-beta 与 public
./tools/Test.ps1 -Filter 'highlights only'
```

测试脚本恢复调用前的 DOTNET_ROOT，不修改系统配置；两个分支不能并行使用同一测试输出目录。CLI 仍可传 `-GodotPath`、`-DotNetRoot` 临时覆盖配置。

## API 查询

在 Paths.local.props 中配置 `IlspyPath`，使用现有两分支真实 DLL 路径。先找类型，再看成员摘要，需要方法体时才导出：

```powershell
./tools/ExportApiReference.ps1 -Find CardPreviewStyle
./tools/ExportApiReference.ps1 -Branch public -Types MegaCrit.Sts2.Core.Random.Rng -Signatures -Member Counter
./tools/ExportApiReference.ps1 -Types MegaCrit.Sts2.Core.Combat.CombatManager -Signatures -Member CheckForEmptyHand
./tools/ExportApiReference.ps1 -Types MegaCrit.Sts2.Core.Models.Cards.HelloWorld -Member OnPlay
```

默认目标为 public-beta；每次输出实际程序集路径和版本来源。`-Find` 模糊查找类型全名，`-Signatures` 只读取元数据，不执行游戏初始化；`-Member` 按名称片段筛选，`-Limit` 控制返回数量。成员摘要只包含当前类型声明的 public/protected 成员，不展开可空引用注解及所有修饰信息；查继承成员时查询对应父类，精确调用和时序依据完整导出源码。

不传 `-Find` 或 `-Signatures` 时按原行为导出方法体，省略 OutputDir 则写入忽略目录 `docs/local/api/queries/<目标与版本>/`，返回来源文件及成员匹配行。原有 `-AssemblyPath -OutputDir -IlspyPath -Types` 用法仍支持；显式程序集不会被配置覆盖。API 查询工具仅用于开发，不参与 Mod 编译/打包，也不需要 Godot 进程。

## 游戏日志与交接

```powershell
./tools/ReadGameLog.ps1                       # 最新 .log 中最后 40 条 AllMatters 匹配行
./tools/ReadGameLog.ps1 -IncludeErrors -Tail 20 # 同时包含全局 ERROR/FATAL/Exception
```

日志目录读取 `Paths.local.props` 的 `GameLogDir`，未填写时使用 `%APPDATA%/SlayTheSpire2/logs`；可用 `-LogDir` 临时覆盖，`-ModId` 指定其他模组。脚本只读，按文件修改时间选择最新 `.log`，输出来源和原始行号；无匹配不代表加载失败。需要排查更早的启动过程时，直接读取输出中的来源文件。分享日志前检查其中的本机路径和个人信息。

脚本读取 props 中的字面值，不展开 MSBuild 表达式；工具路径可相对仓库根目录填写，游戏程序集目录建议使用绝对路径。配置仅保存在本机忽略文件。

交接实现任务时，先记录目标版本、已核实机制及源码入口，再列尚需用户决定的行为。已有决定直接沿用；在开始 TDD 前一次约定公开接口、验收例子、宿主替代边界和审查起点。机制入口见 [阅读地图](game-mechanics.md)，展示和证据范围见 [审查说明](agents/review.md)。查询时先找类型和成员，再按需导出方法体；保留大文件于本地，交接只给出来源、成员和必要证据。

## 文档与参考目录

本工作区 `AllMatters/docs` 是指向 `../AllMatters_docs` 的 junction，Git 索引保存其中的普通文档文件。重新 clone 时 docs 是普通目录，不依赖原机器路径。参考源码保存在 docs/references，由 .gitignore 排除。编译只枚举 src/**/*.cs，避免误编译参考仓库。

初始化不运行上游项目，避免其自动部署、更新 API stub 等构建副作用。后续选用 RitsuLib 时需先验证版本与两分支 API，再显式加入依赖及对应 manifest，而不是直接照搬上游的浮动包版本和自动部署脚本。
