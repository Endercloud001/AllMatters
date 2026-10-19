# 开发环境

## 已发现环境（2026-10-20）

- .NET SDK：9.0.319；项目目标 net9.0。
- Godot：工作区父目录提供 Godot 4.5.1 mono。初始化是纯 DLL Mod；实际导入资源时再启用 Godot SDK 和 PCK 导出。
- 游戏：`E:/SteamLibrary/steamapps/common/Slay the Spire 2`。
- DLL：游戏下 `data_sts2_windows_x86_64/`，含 sts2.dll、0Harmony.dll、GodotSharp.dll。
- 本机 release_info.json：v0.111.0；Steam appmanifest 的 BetaKey 为 public-beta，Build ID 为 24724944。
- [Steam 分支查询](https://api.steamcmd.net/v1/info/2868840)：public Build ID 23811903；public-beta Build ID 24724944。公开版 v0.107.1 的实际 DLL 已从 GitHub 开发仓库获取，并用其 ProductVersion 的构建提交号与同仓库 release_info.json 交叉核对。文件位于 `docs/local/api/public-github-0.107.1/`，仅作本地开发参考；尚无完整 public 游戏安装。另已准备两版 NuGet API 引用包。
- RitsuLib 候选版本：0.6.7；完整 variant-pack 位于 `docs/local/dependencies/RitsuLib-0.6.7/`，未安装进游戏，未加入生产项目。双目标接入 API 编译探针已通过。

分支信息会变化。每次准备发布时重查，记录实际测试版本。两目标骨架均已构建通过；这不能证明玩法兼容或游戏内加载通过。详见 [API 调查与证据](research/dual-branch-api.md)。

## 构建

在仓库根目录运行：

```powershell
Copy-Item Paths.example.props Paths.local.props
./tools/Build.ps1 -Branch public-beta
./tools/Build.ps1 -Branch public -GameDataDir 'D:/STS2-public/data_sts2_windows_x86_64'
```

Paths.local.props 不提交 Git；PublicGameDataDir 与 BetaGameDataDir 分别指向两套 DLL。本工作区前者指向从 GitHub 核对后下载的 0.107.1 实际 DLL，后者指向本机 Steam beta 安装；复制示例 props 会覆盖这些本地设置，请按实际路径填写。也可直接运行 `dotnet build -p:GameBranch=public-beta`。引用游戏程序集时 Private=false，避免把游戏 DLL 打包进 Mod。

输出按分支隔离到 `dist/<branch>/AllMatters/`；构建不会自动写游戏目录。需要本机验证时，将该目录复制到游戏 `mods/AllMatters/` 后启动游戏，查看日志中的 AllMatters 加载信息。目前只产出 DLL 和 manifest；has_pck=false 与实际产物一致。骨架尚无玩法，也尚未进行游戏内验证。

默认公开版完整运行环境的准备：可通过 Steam 切换至默认分支，退出游戏后准备独立开发副本，再切回 public-beta。GitHub 下载的 DLL 足以进行本轮方法体检查和编译，但没有 PCK、原生库等完整环境，不能用于启动游戏验收。本次没有改动 Steam 分支、安装或存档。

## 文档与参考目录

本工作区 `AllMatters/docs` 是指向 `../AllMatters_docs` 的 junction，Git 索引保存其中的普通文档文件。重新 clone 时 docs 是普通目录，不依赖原机器路径。参考源码保存在 docs/references，由 .gitignore 排除。编译只枚举 src/**/*.cs，避免误编译参考仓库。

初始化不运行上游项目，避免其自动部署、更新 API stub 等构建副作用。后续选用 RitsuLib 时需先验证版本与两分支 API，再显式加入依赖及对应 manifest，而不是直接照搬上游的浮动包版本和自动部署脚本。
